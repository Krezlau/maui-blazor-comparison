#!/bin/bash
# ---------------------------------------------------------------------------
# run-matrix.sh — MgrCode Step-7 harness (macOS side)
#
# Drives the 3x3 matrix (tickers {50,100,200} x ups {1,50,100}, duration each)
# against both apps (MgrCode.XamlApp, MgrCode.BlazorApp) on two targets:
#   - MacCatalyst (local, needs a logged-in GUI session so the window paints)
#   - Android physical device (via adb)
#
# Output: harness/results/<app>/<platform>/<tickers>t-<ups>ups.csv
# ---------------------------------------------------------------------------
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HARNESS="$ROOT/harness"
RESULTS="$HARNESS/results"
LOGS="$HARNESS/logs"
MOCK_PROJ="$ROOT/MgrCode.MockServer/MgrCode.MockServer.csproj"
PORT=5010
ADB="${ANDROID_HOME:-$HOME/Library/Android/sdk}/platform-tools/adb"

TICKERS_LIST=(50 100 200)
UPS_LIST=(1 50 100)
APPS_LIST=(xaml blazor)
PLATFORMS_LIST=(mac android)
DURATION=60
NO_BUILD=0
CELLS=""

# LAN IP of this Mac, used as the Android device's server address
# (localhost on the phone is the phone itself). Empty on headless/no LAN.
MAC_IP=""
for iface in en0 en1; do
  ip=$(ipconfig getifaddr "$iface" 2>/dev/null || true)
  if [[ -n "$ip" ]]; then MAC_IP="$ip"; break; fi
done

app_name()   { case "$1" in xaml) echo "MgrCode.XamlApp" ;; blazor) echo "MgrCode.BlazorApp" ;; esac; }
app_pkg()    { case "$1" in xaml) echo "com.companyname.mgrcode.xamlapp" ;; blazor) echo "com.companyname.mgrcode.blazorapp" ;; esac; }
app_human()  { case "$1" in xaml) echo "XAML" ;; blazor) echo "BLAZOR" ;; esac; }

usage() {
  echo "Usage: run-matrix.sh [--platform mac|android|all] [--apps xaml|blazor|all]"
  echo "                      [--cells 50-1,100-50,...] [--duration SECONDS] [--no-build]"
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --platform)
      PLATFORMS_LIST=("$2"); shift 2 ;;
    --apps)
      APPS_LIST=("$2"); shift 2 ;;
    --cells)
      CELLS="$2"; shift 2 ;;
    --duration)
      DURATION="$2"; shift 2 ;;
    --no-build)
      NO_BUILD=1; shift ;;
    *) echo "Unknown option: $1"; usage ;;
  esac
done

if [[ " ${PLATFORMS_LIST[*]} " == *" all "* ]]; then PLATFORMS_LIST=(mac android); fi
if [[ " ${APPS_LIST[*]} " == *" all "* ]]; then APPS_LIST=(xaml blazor); fi

mkdir -p "$RESULTS" "$LOGS"

log()  { printf '[%s] %s\n' "$(date +%H:%M:%S)" "$*"; }
warn() { printf '[WARN] %s\n' "$*"; }

# ---------------- server helpers ----------------
stop_server() {
  lsof -ti:"$PORT" 2>/dev/null | xargs kill 2>/dev/null || true
  sleep 1
}

wait_server() {
  for _ in $(seq 1 60); do
    code=$(curl -s -o /dev/null -w '%{http_code}' "http://localhost:$PORT/v5/market/tickers?category=spot" 2>/dev/null || true)
    if [[ "$code" == "200" ]]; then return 0; fi
    sleep 1
  done
  return 1
}

start_server() {
  local tickers=$1 interval=$2
  stop_server
  nohup dotnet run --project "$MOCK_PROJ" \
      -- --MockServer:TickerCount="$tickers" --MockServer:UpdateIntervalMs="$interval" \
      > "$LOGS/mockserver.log" 2>&1 &
  if ! wait_server; then
    log "ERROR: mock server did not become ready"
    return 1
  fi
  return 0
}

# ---------------- build ----------------
build_app() {
  local app=$1 platform=$2
  local name=$(app_name "$app")
  local tfm
  case "$platform" in
    android) tfm="net10.0-android" ;;
    mac)     tfm="net10.0-maccatalyst" ;;
  esac
  log "build $(app_human "$app") [$tfm] Release"
  dotnet build "$ROOT/$name/$name.csproj" -f "$tfm" -c Release > "$LOGS/build-$app-$platform.log" 2>&1
}

# ---------------- android single cell ----------------
android_cell() {
  local app=$1 tickers=$2 ups=$3 label=$4 out=$5
  local name=$(app_name "$app") pkg=$(app_pkg "$app")
  local apk
  apk=$(ls "$ROOT/$name/bin/Release/net10.0-android/"*Signed.apk 2>/dev/null | head -n1)

  "$ADB" devices 2>/dev/null | grep -q "device$" || { warn "no android device attached; skipping $label"; return 1; }

  [[ -n "$apk" && -f "$apk" ]] || { warn "apk not found for $name; skipping $label"; return 1; }

  log "android install $name"
  "$ADB" install -r "$apk" > /dev/null 2>&1 || { warn "adb install failed for $label"; return 1; }

  local activity
  activity=$("$ADB" shell cmd package resolve-activity --brief "$pkg" | tail -n1 | tr -d '\r')
  [[ -n "$activity" ]] || { warn "cannot resolve launcher activity for $pkg; skipping $label"; return 1; }

  "$ADB" shell am force-stop "$pkg" > /dev/null 2>&1

  local dur_ms=$(( DURATION * 1000 + 5000 ))
  log "android start $label (activity=$activity, duration=${dur_ms}ms, server=http://$MAC_IP:$PORT)"
  local extras=()
  if [[ -n "$MAC_IP" ]]; then
    extras+=(--es baseUrl "http://$MAC_IP:$PORT" --es baseWsUrl "ws://$MAC_IP:$PORT/v5/public/spot")
  fi
  "$ADB" shell am start -n "$activity" \
      --es runLabel "$label" --es autostart 1 --es durationMs "$dur_ms" \
      "${extras[@]}" > /dev/null 2>&1 || { warn "am start failed for $label"; return 1; }

  log "wait ${DURATION}s + 15s margin for $label"
  sleep $(( DURATION + 15 ))

  local src="/sdcard/Android/data/$pkg/files/results/$label.csv"
  if ! "$ADB" pull "$src" "$out" > /dev/null 2>&1; then
    src="/storage/emulated/0/Android/data/$pkg/files/results/$label.csv"
    "$ADB" pull "$src" "$out" > /dev/null 2>&1 || { warn "pull failed for $label"; return 1; }
  fi

  if ! grep -q "^timestamp," "$out" 2>/dev/null; then
    warn "no valid CSV rows for $label"; return 1
  fi
  return 0
}

# ---------------- mac single cell ----------------
mac_cell() {
  local app=$1 tickers=$2 ups=$3 label=$4 out=$5
  local name=$(app_name "$app") pkg=$(app_pkg "$app")
  local bundle
  # Prefer the canonical non-RID bundle: Release Catalyst packs wwwroot only there.
  bundle=$(ls -d "$ROOT/$name/bin/Release/net10.0-maccatalyst/$name.app" 2>/dev/null | head -n1)
  [[ -n "$bundle" && -d "$bundle" ]] || {
    bundle=$(ls -d "$ROOT/$name/bin/Release/net10.0-maccatalyst/maccatalyst-arm64/$name.app" 2>/dev/null | head -n1)
  }
  [[ -d "$bundle" ]] || { warn "app bundle not found for $name; skipping $label"; return 1; }

  pkill -f "$bundle/Contents/MacOS/$name" 2>/dev/null || true

  local dur_ms=$(( DURATION * 1000 + 5000 ))
  log "mac open $label (duration=${dur_ms}ms)"
  open "$bundle" --args --runLabel="$label" --autostart=1 --durationMs="$dur_ms"
  osascript -e "tell application \"$name\" to activate" > /dev/null 2>&1 || true

  log "wait ${DURATION}s + 15s margin for $label"
  sleep $(( DURATION + 15 ))

  local base="$HOME/Library"
  local src=""
  for c in \
      "$base/Containers/$pkg/Data/Library/results/$label.csv" \
      "$base/Containers/$pkg/Data/Library/Application Support/results/$label.csv" \
      "$base/results/$label.csv"
  do
    if [[ -f "$c" ]]; then src="$c"; break; fi
  done

  if [[ -z "$src" ]]; then
    src=$(find "$base" -maxdepth 6 -name "$label.csv" 2>/dev/null | head -n1)
  fi

  if [[ -z "$src" || ! -f "$src" ]]; then
    warn "collect failed for $label (searched under $base)"; return 1
  fi
  cp "$src" "$out"

  if ! grep -q "^timestamp," "$out" 2>/dev/null; then
    warn "no valid CSV rows for $label"; return 1
  fi
  pkill -f "$bundle/Contents/MacOS/$name" 2>/dev/null || true
  return 0
}

# ---------------- matrix driver ----------------
run_matrix() {
  local fails=0 runs=0
  for platform in "${PLATFORMS_LIST[@]}"; do
    for ticks in "${TICKERS_LIST[@]}"; do
      for ups in "${UPS_LIST[@]}"; do
        if [[ -n "$CELLS" ]]; then
          [[ ",$CELLS," == *",${ticks}-${ups},"* ]] || continue
        fi
        local interval=$(( ticks * 1000 / ups ))
        for app in "${APPS_LIST[@]}"; do
          ((runs++))
          local cell_out="$RESULTS/$app/$platform/${ticks}t-${ups}ups.csv"
          local label="${app}-${platform}-${ticks}-${ups}"
          mkdir -p "$(dirname "$cell_out")"

          log "=== [$runs] $(app_human "$app") $(app_name "$app") @ $ticks tickers, $ups up/s, ${interval}ms interval (${platform}) ==="

          start_server "$ticks" "$interval" || { ((fails++)); continue; }

          if [[ "$NO_BUILD" -eq 0 ]]; then
            build_app "$app" "$platform" || { ((fails++)); continue; }
          fi

          if [[ "$platform" == "android" ]]; then
            android_cell "$app" "$ticks" "$ups" "$label" "$cell_out" || { ((fails++)); continue; }
          else
            mac_cell "$app" "$ticks" "$ups" "$label" "$cell_out" || { ((fails++)); continue; }
          fi

          log "ok: $cell_out"
        done
      done
    done
  done

  stop_server
  log "done: $runs runs, $fails failures -> $RESULTS"
}

run_matrix