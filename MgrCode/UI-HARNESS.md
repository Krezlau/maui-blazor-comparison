# UI Harness Progress (Steps 5/6 UI) — MgrCode

Both placeholder dashboards are now identical, render-heavy trading UIs so that
**per-row cost** and **per-frame churn** are the measurable bottleneck. This is the
UI half of Steps 5/6; the full Step 4 `PerformanceMonitor` + CSV sink is a
separate follow-up that reuses the t0/t1 hooks and HUD state built here.

> Date: Aug 2026 · Everything builds green on `net10.0-ios` / `net10.0-maccatalyst` /
> `net10.0-android` + Backend + MockServer.

---

## 1. Backend (shared — `MgrCode.Backend`)

### `Models/Ticker.cs`
- Fixed **24-sample ring buffer** of `decimal` prices (`SparklineCapacity = 24`).
- `long SparklineVersion` — bumped in every `Apply(...)` (even flat updates).
- `IReadOnlyList<decimal> SparklineSamples` — chronological snapshot (oldest → newest),
  returns a fresh copy each access (deliberate per-row allocation churn).

### `Services/Formatting.cs` (new, static)
Shared by both apps so text/colors are byte-identical:
- `FormatPrice(price, symbol)` — magnitude-based decimals: `≥1000 → 2`, `≥1 → 4`, else `6`.
- `FormatCompact(decimal)` — `B / M / K` suffixes (volume).
- `FormatPct(decimal)` — signed percent string (`+1.23%` / `-0.45%`).
- `ColorFromSymbol(string)` — deterministic FNV-1a hash → HSL → hex `#RRGGBB`.

### `ViewModels/CryptoDashboardViewModel.cs`
- `ObservableCollection<Ticker> FilteredTickers` — **replaced by a new instance on every
  search term change** (Blazor `Virtualize` only re-reads `Items` on reference change;
  XAML `CollectionView` also rebinds cleanly). New-ticker adds during streaming go through
  the same filter.
- `SearchText` (observable) → `partial void OnSearchTextChanged` → `RebuildFiltered()`.
- Market strip recomputed **on every UI-thread apply** — `TotalTurnoverText`,
  `TopGainerText`, `TopLoserText` (O(n) scan per applied ticker = intentional churn).
- `event Action? UpdateApplied` raised after each apply batch (Blazor page drives
  `StateHasChanged`; XAML ignores it — native bindings fire automatically).
- `public PerformanceViewModel Performance { get; }` for clean UI binding.

### `ViewModels/PerformanceViewModel.cs`
- `bool ShowHud` — HUD overlay toggle (default on).
- `double UpdatesPerSecond` — derived from the apply counter in `SampleStats()`.
- `RecordUpdateStart()` — **t0 hook** (`System.Diagnostics.Stopwatch`), stamped before the
  update is dispatched (already existed).
- `RecordRenderEnd()` — **t1 hook**; increments the render counter and, once per pending
  update, computes `UpdateLatencyMs`. Second hook for the same update is a no-op.
- `SampleStats()` — called by a ~500 ms timer in each app; computes `Fps` and
  `UpdatesPerSecond` from the hook counters and samples `MemoryMb` / `GcGen0/1/2` /
  `UptimeMs`. `DroppedFrames` / `UiThreadBusyPct` / real GC-pause measurement = Step 4.

---

## 2. XAML app (`MgrCode.XamlApp`)

- `Views/CryptoDashboardPage.xaml(.cs)` — **replaces `MainPage`** (files deleted).
  - Layout: title/actions row (Init/Start/Stop + HUD toggle bridge) → search `Entry` →
    market strip → **pinned header Grid row** (not sticky-scroll) → virtualized
    `CollectionView` → perf HUD `Border` overlay (`IsVisible` ← `Performance.ShowHud`).
  - Sampling loop: `Task.Delay(500)` → `_performance.SampleStats()` (no manual
    `StateHasChanged` needed, bindings fire automatically).
- `Views/TickerRow.xaml(.cs)` — `ContentView` item template, 7 columns:
  color-dot + Symbol, Last, Δ% badge, compact Volume, High/Low, Bid/Ask, sparkline.
  - `BindingContextChanged` subscribes/unsubscribes `Ticker.PropertyChanged`.
  - Text set in code-behind through `Formatting.*` (identical strings to Blazor).
  - **Flash**: on `LastPrice` change sets background green/red, resets to transparent after
    300 ms (generation-cancelled so recycled rows never leak a stale flash).
  - **Sparkline**: `GraphicsView` + custom `IDrawable` (`Views/SparklineDrawable.cs`),
    normalized polyline from `SparklineSamples`; `Invalidate()` on
    `SparklineVersion` change. The **Draw call is the XAML t1 hook**
    (`ResolvePerformance()?.RecordRenderEnd()`); per-second count = XAML FPS.
- `AppServices.cs` — tiny static locator so DataTemplate-created rows can reach the
  `PerformanceViewModel` singleton (set in `MauiProgram`).
- `AppShell.xaml` → `CryptoDashboardPage`; `MauiProgram.cs` registers it (transient via
  Shell), drops `MainPage`.

---

## 3. Blazor app (`MgrCode.BlazorApp`)

- `Components/Pages/CryptoDashboard.razor` `@page "/"` — **replaces `Home.razor`** (deleted).
  - Same chrome as XAML: actions + HUD toggle, search (`@bind` → `SearchText`), market strip,
    sticky-`thead` inside a scrolling wrapper, `<Virtualize>` over `Vm.FilteredTickers`,
    HUD overlay (`position: fixed`, shown/hidden by `Performance.ShowHud`).
  - `UpdateApplied += OnUpdateApplied` → `InvokeAsync(StateHasChanged)` per update (this
    per-update DOM diff **is** the measured Blazor cost). Disposed on unload.
  - `OnAfterRenderAsync` → `Vm.Performance.RecordRenderEnd()` — **Blazor t1 hook**;
    render count/s = Blazor FPS.
  - 500 ms sample loop also calls `StateHasChanged` so HUD numbers refresh.
- `Components/TickerRow.razor(.css)` — component keyed with `@key="@t.PriceTick"` so the
  CSS flash animation restarts every update and the element is re-created (DOM churn).
  Sparkline = inline SVG `<polyline points="…">` built from `SparklineSamples`.
- `Components/_Imports.razor` — added Backend `Models` / `Services` / `ViewModels` usings.

Column layout mirrors XAML exactly (star ratios 2.2/1.5/1.1/1.3/1.7/1.7/1.3 ↔ Blazor
widths 18.6/12.7/9.3/11/14.4/14.4/11).

---

## 4. Measurement hooks (current, minimal)

| Hook | Where |
|------|-------|
| **t0** `RecordUpdateStart` | `CryptoDashboardViewModel.Apply` before `Dispatcher.Dispatch` |
| **t1 XAML** `RecordRenderEnd` | `SparklineDrawable.Draw` (native paint) |
| **t1 Blazor** `RecordRenderEnd` | `CryptoDashboard.OnAfterRenderAsync` |
| HUD sampling (mem/GC/FPS/UPD-S) | ~500 ms timer in each page → `PerformanceViewModel.SampleStats()` |

---

## 5. How to run & verify

```bash
# 1) Mock server (200 tickers, 10 ms):
#    If "address already in use":   lsof -ti:5010 | xargs kill
dotnet run --project MgrCode.MockServer -- --MockServer:TickerCount=200 --MockServer:UpdateIntervalMs=10

# 2) XAML app on iOS simulator:
dotnet build MgrCode.XamlApp/MgrCode.XamlApp.csproj -f net10.0-ios
dotnet build MgrCode.XamlApp/MgrCode.XamlApp.csproj -t:Run -f net10.0-ios

# 3) Blazor app on iOS simulator:
dotnet build MgrCode.BlazorApp/MgrCode.BlazorApp.csproj -f net10.0-ios
dotnet build MgrCode.BlazorApp/MgrCode.BlazorApp.csproj -t:Run -f net10.0-ios
```

In each app: tap **Init** → **Start**. Watch HUD:
- **UPD/S** should match the server’s update rate (≈ 200–20k/s depending on interval/burst).
- **FPS** ≈ native / Blazor render rate under the stream.
- **LAT** = update→render latency (first visible render after each dispatched update).
- Search box filters rows live; HUD toggle hides/shows the overlay.

**Cancellation / Stop regression (the earlier harness):**
1. Start streaming, wait a few seconds.
2. Tap **Stop** → status should return to non-running, HUD **UPD/S** drops to 0, no crash.
3. Restart → streaming resumes cleanly (CTP is re-created per `StartAsync`).
4. Optionally force-kill the mock server during streaming — `TickerStream` reconnects,
   and Stop still cancels cleanly.

---

## 6. File inventory (new / changed)

**Backend**
- `Models/Ticker.cs` — sparkline ring buffer + version + snapshot
- `Services/Formatting.cs` — new shared formatting
- `ViewModels/CryptoDashboardViewModel.cs` — search, filter, market strip, `UpdateApplied`
- `ViewModels/PerformanceViewModel.cs` — `ShowHud`, `UpdatesPerSecond`, `SampleStats`

**XAML app**
- `Views/CryptoDashboardPage.xaml(.cs)` — dashboard page (replaced `MainPage`)
- `Views/TickerRow.xaml(.cs)` — row ContentView w/ flash + sparkline
- `Views/SparklineDrawable.cs` — `IDrawable`, XAML t1 hook
- `AppServices.cs` — service locator
- `AppShell.xaml`, `MauiProgram.cs` — wiring
- `MainPage.xaml(.cs)` — **deleted**

**Blazor app**
- `Components/Pages/CryptoDashboard.razor(.css)` — dashboard page (replaced `Home.razor`)
- `Components/TickerRow.razor(.css)` — row component w/ SVG sparkline + flash
- `Components/_Imports.razor` — backend usings
- `Components/Pages/Home.razor` — **deleted**

---

## 7. Known risks / intentional load

- XAML sparkline `GraphicsView` redraw + `CollectionView` virtualization under tens of
  thousands of updates/sec will hammer the UI thread — **that is the point**; Step 4’s CSV
  captures it.
- Blazor per-update `StateHasChanged` will drop frames at high rates — the honest Blazor
  number.
- Every `SparklineSamples` access allocates a fresh array copy per visible row per update;
  strings are rebuilt each frame in both UIs (deliberate per-frame churn).
- Blazor `Virtualize` re-reads `Items` only on reference change — solved by replacing the
  `FilteredTickers` instance on every filter change (also keeps XAML `CollectionView` in sync).

## 8. Next (Step 4 — separate pass)

Shared `PerformanceMonitor` + CSV sink + identical probe points, reusing the t0/t1 hooks and
the HUD state built here (`SampleStats`, `Capture`). `DroppedFrames`, `UiThreadBusyPct`, and
real GC-pause (`GC.RegisterForFullGCNotification` or a sampled stopwatch) are still stubs.