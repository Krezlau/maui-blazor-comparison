# ---------------------------------------------------------------------------
# run-matrix.ps1 — MgrCode Step-7 harness (Windows leg)
#
# Run this on a Windows machine with .NET 10 MAUI workloads installed, from a
# checkout of the repo. Drives the same 3x3 matrix against both apps on the
# unpackaged WinUI target and copies the CSVs to harness/results.
#
# Output: harness/results/<app>/windows/<tickers>t-<ups>ups.csv
#
# CSV location note: on Windows the app writes into FileSystem.AppDataDirectory
# (unpackaged). The script locates the file by name under %LOCALAPPDATA% so the
# exact AppDataDirectory variant doesn't matter.
# ---------------------------------------------------------------------------
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Results = Join-Path $Root 'harness\results'
$Logs = Join-Path $Root 'harness\logs'
$MockProj = Join-Path $Root 'MgrCode.MockServer\MgrCode.MockServer.csproj'
$Port = 5010

$TickersList = @(50, 100, 200)
$UpsList = @(1, 50, 100)
$Duration = 60
$NoBuild = $false
$Cells = ''

# --- arg parsing ---
for ($i = 0; $i -lt $args.Count; $i++) {
    switch ($args[$i]) {
        '--cells' { $Cells = $args[++$i] }
        '--duration' { $Duration = [int]$args[++$i] }
        '--no-build' { $NoBuild = $true }
        default { throw "Unknown option: $($args[$i])" }
    }
}

New-Item -ItemType Directory -Force -Path $Results, $Logs | Out-Null

function ForEach-AppName($App) {
    switch ($App) {
        'xaml'   { return 'MgrCode.XamlApp' }
        'blazor' { return 'MgrCode.BlazorApp' }
        default  { throw "Unknown app: $App" }
    }
}
function ForEach-AppHuman($App) {
    switch ($App) {
        'xaml'   { return 'XAML' }
        'blazor' { return 'BLAZOR' }
        default  { throw "Unknown app: $App" }
    }
}

function Stop-Server {
    Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique |
        ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 1
}

function Wait-Server {
    for ($i = 0; $i -lt 60; $i++) {
        try {
            $r = Invoke-WebRequest -Uri "http://localhost:$Port/v5/market/tickers`?category=spot" -UseBasicParsing -TimeoutSec 2
            if ($r.StatusCode -eq 200) { return $true }
        } catch { }
        Start-Sleep -Seconds 1
    }
    return $false
}

function Start-Server([int]$Tickers, [int]$Interval) {
    Stop-Server
    $proc = Start-Process dotnet -ArgumentList @(
        'run', '--project', $MockProj,
        '--', "--MockServer:TickerCount=$Tickers", "--MockServer:UpdateIntervalMs=$Interval"
    ) -RedirectStandardOutput (Join-Path $Logs 'mockserver.log') -PassThru
    if (-not (Wait-Server)) {
        Write-Host "ERROR: mock server did not become ready"
        return $false
    }
    return $true
}

function Build-App([string]$App) {
    $name = ForEach-AppName $App
    Write-Host "build $name [net10.0-windows10.0.19041.0] Release"
    $out = Join-Path $Logs "build-$App-windows.log"
    & dotnet build (Join-Path $Root "$name\$name.csproj") -f 'net10.0-windows10.0.19041.0' -c Release *> $out
    if ($LASTEXITCODE -ne 0) { return $false }
    return $true
}

function WindowsCell([string]$App, [int]$Tickers, [int]$Ups, [string]$Label, [string]$Out) {
    $name = ForEach-AppName $App
    $exe = Get-ChildItem -Path (Join-Path $Root "$name\bin\Release") -Filter '*.exe' -Recurse |
        Where-Object { $_.FullName -like '*windows*' } | Select-Object -First 1
    if (-not $exe) { Write-Warning "exe not found for $name; skipping $Label"; return $false }

    $durMs = $Duration * 1000 + 5000
    Write-Host "launch $Label (duration=${durMs}ms)"
    $p = Start-Process -FilePath $exe.FullName -ArgumentList @(
        "--runLabel=$Label", '--autostart=1', "--durationMs=$durMs"
    ) -PassThru -WorkingDirectory (Split-Path $exe.FullName)

    Write-Host "wait $Duration s + 15s margin for $Label"
    Start-Sleep -Seconds ($Duration + 15)
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue

    $src = Get-ChildItem -Path $env:LOCALAPPDATA -Recurse -Filter "$Label.csv" -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $src) { Write-Warning "collect failed for $Label"; return $false }
    Copy-Item $src.FullName $Out -Force

    if (-not (Select-String -Path $Out -Pattern '^timestamp,' -Quiet)) {
        Write-Warning "no valid CSV rows for $Label"; return $false
    }
    return $true
}

$fails = 0
$runs = 0
foreach ($ticks in $TickersList) {
    foreach ($ups in $UpsList) {
        if ($Cells -ne '') {
            $cellsArr = $Cells -split ','
            if ($cellsArr -notcontains "$ticks-$ups") { continue }
        }
        $interval = $ticks * 1000 / $ups
        foreach ($app in @('xaml', 'blazor')) {
            $runs++
            $cellOut = Join-Path $Results "$app\windows\${ticks}t-${ups}ups.csv"
            $label = "$app-windows-$ticks-$ups"
            New-Item -ItemType Directory -Force -Path (Split-Path $cellOut) | Out-Null

            $human = ForEach-AppHuman $app
            $winName = ForEach-AppName $app
            Write-Host "=== [$runs] $human $winName @ $ticks tickers, $ups up/s, ${interval}ms interval (windows) ==="

            if (-not (Start-Server $ticks $interval)) { $fails++; continue }
            if (-not $NoBuild) {
                if (-not (Build-App $app)) { $fails++; continue }
            }
            if (-not (WindowsCell $app $ticks $ups $label $cellOut)) { $fails++; continue }
            Write-Host "ok: $cellOut"
        }
    }
}

Stop-Server
Write-Host "done: $runs runs, $fails failures -> $Results"
exit $fails