#!/usr/bin/env python3
"""Load generator for a lab build of the mod (from the step 0 spike; routes under /api/lab, LabProbe).

Runs phases one after another, e.g. --plan baseline:60,max:60,baseline:30,panel:60,churn:30
  baseline  no client sockets (the server idles)
  max       --http keep-alive sockets looping GET /api/ping and /blob?n=16384 as fast as they can, and --ws WebSocket
            sockets doing 1 KiB ping-pong echoes as fast as they can (server pushes stats every second on each)
  panel     the same sockets at a panel's pace: each HTTP socket 2 requests/s, each WebSocket 10 messages/s + the push
  churn     --http sockets each doing connect + (TLS handshake) + one request + close, in a loop
Writes one JSON line per phase to --out, and (with --pid) one line a second of the server process seen from Linux
(/proc: RSS, CPU, threads) to --proc-out.
"""
import argparse, asyncio, json, os, ssl, struct, time, base64

def pct(v, q):
    if not v: return None
    v = sorted(v)
    return round(v[min(len(v) - 1, max(0, int(-(-q * len(v) // 1)) - 1))], 3)

def summary(v):
    if not v: return None
    return {"n": len(v), "mean": round(sum(v) / len(v), 3), "p50": pct(v, .5), "p95": pct(v, .95), "p99": pct(v, .99), "max": round(max(v), 3)}

class Phase:
    def __init__(self, name):
        self.name = name; self.http = []; self.ws = []; self.connect = []; self.pushes = 0; self.errors = 0; self.reconnects = 0
        self.http_bytes = 0; self.err_samples = []
    def err(self, e):
        self.errors += 1
        if len(self.err_samples) < 5: self.err_samples.append(repr(e)[:200])

def ctx_for(tls):
    if not tls: return None
    c = ssl.create_default_context(); c.check_hostname = False; c.verify_mode = ssl.CERT_NONE
    c.set_alpn_protocols(["http/1.1"]); return c

async def connect(a, ph):
    t0 = time.perf_counter()
    r, w = await asyncio.wait_for(asyncio.open_connection(a.host, a.port, ssl=ctx_for(a.tls),
                                                          server_hostname="localhost" if a.tls else None), 15)
    ph.connect.append((time.perf_counter() - t0) * 1000)
    return r, w

async def http_req(r, w, path, host, close=False):
    w.write(f"GET {path} HTTP/1.1\r\nHost: {host}\r\nUser-Agent: spike-load\r\nConnection: {'close' if close else 'keep-alive'}\r\n\r\n".encode())
    await w.drain()
    status = await r.readline()
    if not status.startswith(b"HTTP/1.1 200"): raise IOError(f"status {status!r}")
    n = 0
    while True:
        line = await r.readline()
        if line in (b"\r\n", b"\n", b""): break
        k, _, v = line.decode().partition(":")
        if k.strip().lower() == "content-length": n = int(v)
    await r.readexactly(n)
    return n

async def http_worker(a, i, state, rate):
    ph = state["phase"]; r = w = None
    paths = ["/api/lab/ping", "/api/lab/blob?n=16384"]
    k = 0
    while not state["stop"].is_set():
        try:
            if w is None:
                r, w = await connect(a, ph)
            t0 = time.perf_counter()
            n = await asyncio.wait_for(http_req(r, w, paths[k % 2], a.host), 15)
            ph.http.append((time.perf_counter() - t0) * 1000); ph.http_bytes += n; k += 1
            if rate: await asyncio.sleep(1 / rate)
        except Exception as e:
            ph.err(e); ph.reconnects += 1
            if w: w.close()
            w = None
            await asyncio.sleep(0.2)
    if w:
        w.close()

def ws_frame(opcode, payload):
    n = len(payload)
    head = bytes([0x80 | opcode])
    if n < 126: head += bytes([0x80 | n])
    elif n < 65536: head += bytes([0x80 | 126]) + struct.pack(">H", n)
    else: head += bytes([0x80 | 127]) + struct.pack(">Q", n)
    return head + b"\0\0\0\0" + payload   # an all-zero mask key: valid, and the payload stays as is

async def ws_read(r):
    b0, b1 = await r.readexactly(2)
    n = b1 & 0x7F
    if n == 126: n = struct.unpack(">H", await r.readexactly(2))[0]
    elif n == 127: n = struct.unpack(">Q", await r.readexactly(8))[0]
    return b0 & 0x0F, await r.readexactly(n)

async def ws_open(a, ph, push_ms):
    r, w = await connect(a, ph)
    key = base64.b64encode(os.urandom(16)).decode()
    w.write(f"GET /api/lab/ws?push={push_ms} HTTP/1.1\r\nHost: {a.host}:{a.port}\r\nOrigin: {'https' if a.tls else 'http'}://{a.host}:{a.port}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
            f"Sec-WebSocket-Key: {key}\r\nSec-WebSocket-Version: 13\r\n\r\n".encode())
    await w.drain()
    status = await r.readline()
    if b" 101 " not in status: raise IOError(f"upgrade {status!r}")
    while (await r.readline()) not in (b"\r\n", b""): pass
    return r, w

async def ws_worker(a, i, state, rate):
    ph = state["phase"]; r = w = None
    payload_tail = b"x" * 1000
    seq = 0
    while not state["stop"].is_set():
        try:
            if w is None:
                r, w = await ws_open(a, ph, 1000)
            seq += 1
            msg = f"m{seq:08d}".encode() + payload_tail
            t0 = time.perf_counter()
            w.write(ws_frame(1, msg)); await w.drain()
            while True:
                op, data = await asyncio.wait_for(ws_read(r), 15)
                if op == 9: w.write(ws_frame(10, data)); continue
                if op == 10: continue
                if op == 8: raise IOError("server closed: " + data[2:].decode(errors="replace"))
                if data[:9] == msg[:9]: break
                ph.pushes += 1
            ph.ws.append((time.perf_counter() - t0) * 1000)
            if rate: await asyncio.sleep(1 / rate)
        except Exception as e:
            ph.err(e); ph.reconnects += 1
            if w: w.close()
            w = None
            await asyncio.sleep(0.2)
    if w:
        try:
            w.write(ws_frame(8, struct.pack(">H", 1000) + b"done")); await w.drain()
            await asyncio.wait_for(ws_read(r), 2)
        except Exception:
            pass
        w.close()

async def churn_worker(a, i, state):
    ph = state["phase"]
    while not state["stop"].is_set():
        try:
            r, w = await connect(a, ph)
            t0 = time.perf_counter()
            await asyncio.wait_for(http_req(r, w, "/api/lab/ping", a.host, close=True), 15)
            ph.http.append((time.perf_counter() - t0) * 1000)
            w.close()
        except Exception as e:
            ph.err(e); await asyncio.sleep(0.2)

async def proc_sampler(pid, out, stop):
    hz = os.sysconf("SC_CLK_TCK"); last = None
    with open(out, "a") as f:
        while not stop.is_set():
            try:
                st = open(f"/proc/{pid}/stat").read().rsplit(")", 1)[1].split()
                cpu = (int(st[11]) + int(st[12])) / hz
                status = dict(l.split(":", 1) for l in open(f"/proc/{pid}/status").read().splitlines() if ":" in l)
                now = time.time()
                rec = {"t": round(now, 3), "rssMB": round(int(status["VmRSS"].split()[0]) / 1024, 1),
                       "threads": int(status["Threads"]), "load1": os.getloadavg()[0]}
                if last: rec["cpuPct"] = round((cpu - last[1]) / (now - last[0]) * 100, 1)
                last = (now, cpu)
                f.write(json.dumps(rec) + "\n"); f.flush()
            except FileNotFoundError:
                return
            await asyncio.sleep(1)

async def run(a):
    sampler_stop = asyncio.Event()
    sampler = asyncio.create_task(proc_sampler(a.pid, a.proc_out, sampler_stop)) if a.pid else None
    with open(a.out, "a") as out:
        for step in a.plan.split(","):
            name, secs = step.split(":"); secs = float(secs)
            ph = Phase(name); state = {"phase": ph, "stop": asyncio.Event()}
            tasks = []
            if name == "max":
                tasks += [asyncio.create_task(http_worker(a, i, state, 0)) for i in range(a.http)]
                tasks += [asyncio.create_task(ws_worker(a, i, state, 0)) for i in range(a.ws)]
            elif name == "panel":
                tasks += [asyncio.create_task(http_worker(a, i, state, 2)) for i in range(a.http)]
                tasks += [asyncio.create_task(ws_worker(a, i, state, 10)) for i in range(a.ws)]
            elif name == "churn":
                tasks += [asyncio.create_task(churn_worker(a, i, state)) for i in range(a.http)]
            elif name != "baseline":
                raise SystemExit(f"unknown phase {name}")
            t0 = time.time()
            print(f"{time.strftime('%H:%M:%S')} phase {name} {secs:.0f}s ({len(tasks)} sockets, {'TLS' if a.tls else 'plain'} :{a.port})", flush=True)
            await asyncio.sleep(secs)
            state["stop"].set()
            await asyncio.wait(tasks, timeout=10) if tasks else None
            t1 = time.time(); dur = t1 - t0
            rec = {"phase": name, "label": a.label, "tls": a.tls, "port": a.port, "start": round(t0, 3), "end": round(t1, 3),
                   "sockets": len(tasks), "httpRequests": len(ph.http), "httpPerSec": round(len(ph.http) / dur, 1),
                   "httpMs": summary(ph.http), "httpMB": round(ph.http_bytes / 1048576, 1), "wsEchoes": len(ph.ws),
                   "wsPerSec": round(len(ph.ws) / dur, 1), "wsMs": summary(ph.ws), "wsPushes": ph.pushes,
                   "connectMs": summary(ph.connect), "errors": ph.errors, "reconnects": ph.reconnects, "errorSamples": ph.err_samples}
            out.write(json.dumps(rec) + "\n"); out.flush()
            print("   ", json.dumps({k: rec[k] for k in ("httpPerSec", "wsPerSec", "errors")}), "http", rec["httpMs"] and rec["httpMs"]["p95"], "ws", rec["wsMs"] and rec["wsMs"]["p95"], flush=True)
    sampler_stop.set()
    if sampler: await sampler

if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--host", default="127.0.0.1"); p.add_argument("--port", type=int, required=True)
    p.add_argument("--tls", action="store_true"); p.add_argument("--http", type=int, default=10); p.add_argument("--ws", type=int, default=10)
    p.add_argument("--plan", required=True); p.add_argument("--out", required=True); p.add_argument("--label", default="")
    p.add_argument("--pid", type=int); p.add_argument("--proc-out", default="/dev/null")
    asyncio.run(run(p.parse_args()))
