# MgrCode Step-7 Harness

Runs the 3×3 matrix (tickers {50,100,200} × updates/s {1,50,100} × duration)
against both apps (XAML + Blazor) on the target platforms and collects the
fixed-schema CSV the Step-4 sink produces.

Each cell → `harness/results/<app>/<platform>/<tickers>t-<ups>ups.csv`.

## Matrix → MockServer pacing

The MockServer presents one update per subscribed symbol per interval, so to hit
a total `ups` for `tickers` symbols the interval is `intervalMs = tickers*1000/ups`:

| tickers | ups | intervalMs |
|--------:|----:|-----------:|
| 50 | 1 | 50000 |
| 50 | 50 | 1000 |
| 50 | 100 | 500 |
| 100 | 1 | 100000 |
| 100 | 50 | 2000 |
| 100 | 100 | 1000 |
| 200 | 1 | 200000 |
| 200 | 50 | 4000 |
| 200 | 100 | 2000 |

## Prerequisites

- .NET 10 SDK (this repo's MAUI apps) + platform workloads.
- **MacCatalyst** leg runs on macOS in a **logged-in GUI session** — the app must
  be able to paint its window (the t1 render hooks are native-paint/DOM-apply
  driven; a headless launch will legitimately report FPS ≈ 0).
- **Android** leg needs a physical device with USB debugging on (`adb devices`
  must list it). CSVs are written to app-external storage so `adb pull` works on
  Release builds. Phone and Mac must be on the same LAN: the harness
  auto-detects the Mac's IP and passes it to the app via Intent extras
  (`baseUrl`/`baseWsUrl`), so the phone reaches the MockServer (`localhost` on a
  phone is the phone itself). Android clears cleartext for these LAN endpoints
  (`usesCleartextTraffic`).
- **Windows** leg runs on a Windows box from the same repo checkout
  (`run-matrix.ps1`).

## macOS (`run-matrix.sh`)

```bash
# full 9-cell matrix, both apps, both platforms (~1h+):
harness/run-matrix.sh

# sanity pass: single app/small cells:
harness/run-matrix.sh --apps xaml --cells 50-1,50-50 --platform mac

# flags
  --platform mac|android|all     (default all)
  --apps     xaml|blazor|all     (default all)
  --cells    50-1,100-50,...     (default: full matrix)
  --duration SECONDS             (default 60)
  --no-build                     (skip dotnet build, reuse last output)
```

Each run: server on :5010 with cell pacing → app launched with
`--runLabel/--autostart/--durationMs` → waits `duration+15s` (app auto-stops and
writes its metadata footer at `duration+5s`) → pulls CSV.

## Windows (`run-matrix.ps1`)

```powershell
# from a repo checkout on the Windows box:
.\harness\run-matrix.ps1
.\harness\run-matrix.ps1 -Cells 50-1,100-50   # (see script for arg casing: --cells)
```

Locates each CSV by name under `$env:LOCALAPPDATA` (unpackaged AppDataDirectory),
so the exact location variant is irrelevant.

## Aggregation

```bash
harness/summarize.py                          # markdown table
harness/summarize.py --warmup 10              # skip first ~5s of each cell
harness/summarize.py --format text            # TSV for cut/paste
```

## Caveats

- **Clear `harness/results/` before a formal run** — cells are overwritten on
  re-run, but a partial/older run would otherwise mix vintages in the table.
- Desktop legs (MacCatalyst/Windows) are far above mobile capacity; the low-ups
  cells exist to keep the matrix comparable across platforms.
- `latMs`/FPS require a live rendering surface (foreground window / on-screen
  activity). Keep the app foregrounded during a run; a headless/occluded session
  legitimately reports FPS ≈ 0.
- Ticker count column reflects the filtered (visible) row count; with no search
  active that equals the full set.
- Release builds root `System.Net.*` assemblies (see csproj) — without that,
  `ClientWebSocket` under Catalyst trimming stalls after the first frame.