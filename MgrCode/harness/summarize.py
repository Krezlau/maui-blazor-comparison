#!/usr/bin/env python3
"""Aggregate MgrCode harness CSVs into a compact per-cell perf table.

Usage:
    summarize.py [--dir RESULTS_DIR] [--warmup N] [--format md|text]

Each cell file is named <tickers>t-<ups>ups.csv and uses the fixed schema:
    timestamp,tickers,latMs,fps,droppedFrames,uiBusyPct,memMb,gc0,gc1,gc2,gcPauseMs,uptimeMs
Footer lines start with '#' (metadata). Startup ramp is skipped via --warmup
(samples; the default 10 covers the first ~5s).
"""
import argparse
import csv
import statistics
import sys
from pathlib import Path


def p95(values):
    if not values:
        return 0.0
    s = sorted(values)
    idx = min(len(s) - 1, int(0.95 * len(s)))
    return s[idx]


def mean(values):
    return statistics.fmean(values) if values else 0.0


def parse_file(path, warmup):
    header = None
    rows = []
    meta = {}
    with path.open() as fh:
        for raw in fh:
            line = raw.rstrip("\n")
            if line.startswith("#"):
                if line.startswith("# ") and "=" in line:
                    k, v = line[2:].split("=", 1)
                    meta[k.strip()] = v.strip()
                continue
            if header is None:
                header = line.split(",")
                continue
            cells = line.split(",")
            if len(cells) != len(header):
                continue
            rows.append(dict(zip(header, cells)))
    if not rows:
        return None
    data = rows[warmup:] if warmup else rows
    if not data:
        return None
    fps = [float(r["fps"]) for r in data]
    lat = [float(r["latMs"]) for r in data]
    busy = [float(r["uiBusyPct"]) for r in data]
    mem = [float(r["memMb"]) for r in data]
    dropped = [int(r["droppedFrames"]) for r in data]
    d0 = float(data[0]["uptimeMs"]); d1 = float(data[-1]["uptimeMs"])
    dur = max(d1 - d0, 1.0) / 1000.0
    return {
        "n": len(data),
        "dur": dur,
        "fps_mean": mean(fps), "fps_p95": p95(fps),
        "lat_mean": mean(lat), "lat_p95": p95(lat),
        "lat_nz": 100.0 * sum(1 for v in lat if v > 0) / len(lat) if lat else 0.0,
        "busy_mean": mean(busy), "busy_p95": p95(busy),
        "mem_mean": mean(mem), "mem_max": max(mem) if mem else 0.0,
        "drop_per_s": sum(dropped) / dur,
        "refresh": meta.get("refreshHz", "?"),
        "config": meta.get("config", "?"),
    }


def collect(results_dir, warmup):
    cells = []
    for app_dir in sorted(results_dir.iterdir()):
        if not app_dir.is_dir():
            continue
        for plat_dir in sorted(app_dir.iterdir()):
            if not plat_dir.is_dir():
                continue
            for csv in sorted(plat_dir.glob("*.csv")):
                stats = parse_file(csv, warmup)
                if stats is None:
                    print(f"# skip (no data rows): {csv}", file=sys.stderr)
                    continue
                cell = csv.stem
                tick, _, ups = cell.partition("t-")
                ups = ups[:-3] if ups.endswith("ups") else "?"
                cells.append((app_dir.name, plat_dir.name, tick, ups, csv.name, stats))
    return cells


def render(cells, fmt):
    headers = ["app", "platform", "tickers", "ups", "n", "durS",
               "fpsMean", "fpsP95", "latMean", "latP95", "latNZ%",
               "busy%", "memMB", "memMax", "drop/s", "Hz", "cfg"]
    if fmt == "md":
        sep = " | ".join(["---"] * len(headers))
        print("| " + " | ".join(headers) + " |")
        print("| " + sep + " |")
        for app, plat, tick, ups, _name, s in cells:
            print("| {} | {} | {} | {} | {} | {:.0f} | {:.1f} | {:.1f} | "
                  "{:.2f} | {:.2f} | {:.0f} | {:.1f} | {:.1f} | {:.1f} | "
                  "{:.1f} | {} | {} |".format(
                app, plat, tick, ups, s["n"], s["dur"], s["fps_mean"], s["fps_p95"],
                s["lat_mean"], s["lat_p95"], s["lat_nz"], s["busy_mean"],
                s["mem_mean"], s["mem_max"], s["drop_per_s"], s["refresh"], s["config"]))
    else:
        print("\t".join(headers))
        for app, plat, tick, ups, _name, s in cells:
            print("{}\t{}\t{}\t{}\t{}\t{:.0f}\t{:.1f}\t{:.1f}\t{:.2f}\t"
                  "{:.2f}\t{:.0f}\t{:.1f}\t{:.1f}\t{:.1f}\t{:.1f}\t{}\t{}".format(
                app, plat, tick, ups, s["n"], s["dur"], s["fps_mean"], s["fps_p95"],
                s["lat_mean"], s["lat_p95"], s["lat_nz"], s["busy_mean"],
                s["mem_mean"], s["mem_max"], s["drop_per_s"], s["refresh"], s["config"]))


def main():
    ap = argparse.ArgumentParser()
    root = Path(__file__).resolve().parent / "results"
    ap.add_argument("--dir", type=Path, default=root)
    ap.add_argument("--warmup", type=int, default=10,
                    help="skip first N samples (~0.5s each) of each cell")
    ap.add_argument("--format", choices=["md", "text"], default="md")
    args = ap.parse_args()

    if not args.dir.is_dir():
        print(f"results dir not found: {args.dir}", file=sys.stderr)
        return 1
    cells = collect(args.dir, args.warmup)
    if not cells:
        print("no cells found", file=sys.stderr)
        return 1
    render(cells, args.format)
    return 0


if __name__ == "__main__":
    sys.exit(main())