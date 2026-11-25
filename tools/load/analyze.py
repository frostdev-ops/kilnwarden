#!/usr/bin/env python3
"""Joins load phases (load.py --out) with the lab build's per-second metrics (<data>/lab-metrics.jsonl) and
the /proc samples (load.py --proc-out). One row per phase; the first --skip seconds of each phase are left out."""
import json, sys, argparse, statistics

def load(path):
    return [json.loads(l) for l in open(path) if l.strip()]

def main():
    p = argparse.ArgumentParser()
    p.add_argument("--phases", required=True); p.add_argument("--metrics", required=True); p.add_argument("--proc")
    p.add_argument("--skip", type=float, default=5); p.add_argument("--json", action="store_true")
    a = p.parse_args()
    phases = load(a.phases); metrics = load(a.metrics); proc = load(a.proc) if a.proc else []
    rows = []
    for ph in phases:
        t0, t1 = ph["start"] + a.skip, ph["end"]
        m = [x for x in metrics if t0 <= x["t"] <= t1 and x.get("interval")]
        allm = [x for x in metrics if ph["start"] <= x["t"] <= t1]
        pr = [x for x in proc if t0 <= x["t"] <= t1 and "cpuPct" in x]
        if not m: continue
        def wmean(key):
            n = sum(x[key]["n"] for x in m if x.get(key)); return round(sum(x[key]["mean"] * x[key]["n"] for x in m if x.get(key)) / n, 3) if n else None
        row = {
            "phase": ph["phase"], "label": ph.get("label"), "secs": round(t1 - t0), "sockets": ph["sockets"],
            "http/s": ph["httpPerSec"], "ws/s": ph["wsPerSec"], "http p95 ms": ph["httpMs"] and ph["httpMs"]["p95"],
            "ws p95 ms": ph["wsMs"] and ph["wsMs"]["p95"], "connect p50 ms": ph["connectMs"] and ph["connectMs"]["p50"],
            "errors": ph["errors"],
            "steps/s": round(statistics.mean(x["steps"] for x in m), 2),
            "interval mean": wmean("interval"), "interval p95 (median s)": round(statistics.median(x["interval"]["p95"] for x in m), 2),
            "interval p99 (max s)": round(max(x["interval"]["p99"] for x in m), 2), "interval max": round(max(x["interval"]["max"] for x in m), 2),
            "step mean": wmean("step"), "step p95 (median s)": round(statistics.median(x["step"]["p95"] for x in m if x.get("step")), 2),
            "step p99 (max s)": round(max(x["step"]["p99"] for x in m if x.get("step")), 2), "step max": round(max(x["step"]["max"] for x in m if x.get("step")), 2),
            "proc cpu% (server)": round(statistics.mean(x["cpuPct"] for x in m), 1),
            "proc cpu% (/proc)": round(statistics.mean(x["cpuPct"] for x in pr), 1) if pr else None,
            "rss MB": round(max(x["rssMB"] for x in pr), 1) if pr else None,
            "threads": max(x["threads"] for x in pr) if pr else max(x["threads"] for x in m),
            "pool threads": max(x["poolThreads"] for x in m),
            "gc heap MB max": max(x["gcHeapMB"] for x in m), "gc committed MB": max(x["gcCommittedMB"] for x in m),
            "gc alloc MB/s": round((allm[-1]["gcAllocatedMB"] - allm[0]["gcAllocatedMB"]) / max(1, allm[-1]["t"] - allm[0]["t"]), 1),
            "gc0/1/2": f'{allm[-1]["gc0"] - allm[0]["gc0"]}/{allm[-1]["gc1"] - allm[0]["gc1"]}/{allm[-1]["gc2"] - allm[0]["gc2"]}',
            "gc pause ms": round(allm[-1]["gcPauseMs"] - allm[0]["gcPauseMs"], 1),
            "load1": round(statistics.mean(x["load1"] for x in pr), 1) if pr else None,
        }
        rows.append(row)
    if a.json:
        for r in rows: print(json.dumps(r))
        return
    keys = list(rows[0].keys())
    print("| " + " | ".join(keys) + " |"); print("|" + "---|" * len(keys))
    for r in rows: print("| " + " | ".join("" if r[k] is None else str(r[k]) for k in keys) + " |")

main()
