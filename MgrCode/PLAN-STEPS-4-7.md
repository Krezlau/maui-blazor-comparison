# Plan — Steps 4 & 7 (Data Collection + Device Harness)

Status: agreed plan, not yet implemented.
Targets: **Android (physical device) + MacCatalyst (this Mac) + Windows (separate box/VM)**.
Same 9-cell matrix everywhere. No iOS artifacts (no iPhone available).

---

## Step 4 — Data collection

### `MgrCode.Backend/Services/CsvPerformanceSink.cs` (new; pure .NET)
- ctor(`outputDirectory`, `runLabel`, metadata dict).
- Writes `<runLabel>.csv` with fixed header = `PerformanceSnapshot` columns:
  `timestamp,tickers,latMs,fps,droppedFrames,uiBusyPct,memMb,gc0,gc1,gc2,gcPauseMs,uptimeMs`.
- Rows queued to a `Channel<PerformanceSnapshot>`, drained by a **background writer** — zero I/O on the UI thread.
- `Complete()` flushes and appends a **metadata footer** (run label, app, platform, device model, OS, Release config, refresh rate, tickers, target ups, duration).

### `PerformanceViewModel` edits
- `SampleStats()`: derive
  - `UiThreadBusyPct` = clamp((measuredInterval − 500) / 500, 0..1)
  - `DroppedFrames` = max(0, ⌈refreshRate × seconds⌉ − renders)
- Add `SetRefreshRate(double)` — called once per app from `DeviceDisplay.MainDisplayInfo.RefreshRate`.
- `GcPauseMs` stays a documented stub (no clean zero-overhead measurement).

### Both csproj (`MgrCode.XamlApp`, `MgrCode.BlazorApp`)
- Add `net10.0-windows10.0.19041.0` to `TargetFrameworks` (props already exist).
- Harness always builds with `-f`, so the Mac never attempts the Windows TFM.

### Both apps — mirrored wiring
- New tiny per-app `RunConfig` helper: reads `MGR_RUN_LABEL` / `MGR_AUTOSTART` / `MGR_COLLECT`
  - Windows + MacCatalyst: `Environment.GetCommandLineArgs()` / env
  - Android: `Intent` extras via `Platform.CurrentActivity`
- `MauiProgram`: register sink singleton at `Path.Combine(FileSystem.AppDataDirectory, "results")` + metadata from `DeviceInfo`.
- Page sample loop (500 ms): one line → `_sink.Append(_performance.Capture(label, now, tickerCount))`.
- Auto-fire Init→Start when `MGR_AUTOSTART=1`; `Complete()` on page disappear.

---

## Step 7 — Device harness

### Matrix
9 cells — tickers {50, 100, 200} × ups {1, 50, 100} × 60 s per cell.
Server pacing: `intervalMs = tickerCount * 1000 / ups` (non-burst) so the client sees the target ups.
One CSV per cell → `harness/results/<app>/<platform>/<cell>.csv`.

### `harness/run-matrix.sh` (on this Mac; flags `--android`, `--mac`, `--cells`)
Per cell:
1. Kill prior MockServer on :5010; start with computed args.
2. Release build: `-f net10.0-android` / `-f net10.0-maccatalyst`.
3. Deploy + run:
   - Android: `adb install -r` then `am start -e runLabel … -e autostart 1`
   - MacCatalyst: `MGR_RUN_LABEL=… MGR_AUTOSTART=1 open <app>`
4. Wait 60 s + 10 s margin.
5. Pull CSV: `adb pull` (Android); direct `cp` from `~/Library/Containers/<bundleId>/…/results/` (Mac).

### `harness/run-matrix.ps1` (run on the Windows box)
- Same logic: Release build `-f net10.0-windows*`, launch exe with args, copy CSV from `%LOCALAPPDATA%`.
- First Windows run must confirm exact AppDataDirectory location (unpackaged `WindowsPackageType=None`).

### `harness/summarize.py` (optional)
- avg/max per cell across both apps → quick table.

---

## Verification

- Release-build both apps for `net10.0-android` + `net10.0-maccatalyst` on this Mac
  (proves Windows TFM doesn't break Mac builds).
- One live cell (50 tickers / 50 ups): expect ~120 rows over 60 s, sane LAT/FPS,
  low `uiBusyPct`, footer present — on both the Android device and MacCatalyst.

## Assumptions

- Android package names: `com.companyname.mgrcode.xamlapp`, `com.companyname.mgrcode.blazorapp`.
- `adb devices` shows the physical phone with USB debugging enabled.
- First Windows run pins the CSV path.

## Notes

- MacCatalyst is the "real hardware desktop" leg — CSV is local, no device-copy step.
- Windows leg runs on the separate box/VM; `run-matrix.ps1` is the artifact for it.
