# MgrCode

Compares rendering performance of **one identical live trading dashboard** built twice in .NET MAUI: native **XAML** vs **Blazor hybrid** (HTML/CSS in a WebView). Both apps share the same backend, data feed and instrumentation, so the only difference measured is the UI framework. Targets: Android, MacCatalyst, Windows.

## Projects

| Project | What it is | Responsibility |
|---|---|---|
| `MgrCode.MockServer` | Kestrel console app mimicking **Bybit** | Serves the fixed ticker workload — `GET /v5/market/tickers` (snapshot) and `WS /v5/public/spot` (`ticker.{SYMBOL}` channels). Realistic OHLC prices via `MockDataGenerator`; configurable ticker count, update interval, latency, burst mode. |
| `MgrCode.Backend` | Shared class library (`net10.0`, no MAUI dependency) | Everything both UIs consume: ticker models, `IBybitService` (`Mock`/`Real` interchangeable), WebSocket `TickerStream`, `CsvPerformanceSink`, both view models, t0/t1 metric hooks. |
| `MgrCode.XamlApp` | MAUI app, **native XAML** dashboard | `CryptoDashboardPage` + `TickerRow`: CollectionView, flash animation, `GraphicsView` sparkline, perf HUD. Render hook (`SparklineDrawable.Draw`) is the native t1 metric. |
| `MgrCode.BlazorApp` | MAUI app, **BlazorWebView** dashboard | Same dashboard in HTML/CSS: `CryptoDashboard.razor` + `TickerRow.razor` (Virtualize, CSS flash, SVG sparkline), perf HUD. `OnAfterRenderAsync` is the t1 metric. |
| `harness/` | Test tooling | `run-matrix.sh` (MacCatalyst + Android), `run-matrix.ps1` (Windows), `summarize.py` (CSV aggregation). See `harness/README.md`. |
| `run-android.sh` | Convenience script | Boots the Android emulator and launches an app. |

## Flow

```
MockServer (Bybit mock, fixed pacing)
   ├─► MgrCode.XamlApp  ─┐
   └─► MgrCode.BlazorApp ─┴─► MgrCode.Backend (shared VM + t0/t1 hooks + CSV sink)
                                     ▼
                      harness/* → results/*.csv → summarize.py
```

Both apps run the same `CryptoDashboardViewModel`: t0 is stamped before an update is dispatched, t1 when the UI framework reports the render, and ~every 500 ms a row is written to CSV. Only the rendering technology differs.

## Quick start (local)

```bash
dotnet run --project MgrCode.MockServer -- --MockServer:TickerCount=200
dotnet build MgrCode.XamlApp/MgrCode.XamlApp.csproj -t:Run -f net10.0-maccatalyst    # or MgrCode.BlazorApp
```

Full matrix + aggregation: `harness/README.md`.
Design/methodology notes: `UI-HARNESS.md`.