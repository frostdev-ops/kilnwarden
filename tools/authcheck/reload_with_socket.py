#!/usr/bin/env python3
"""reload-test x5 with a signed-in KAP socket open and read continuously (as a browser does): the socket gets 1001, the
reloads leak nothing, the session survives (sessions.json) and a new socket opens after.
    reload_with_socket.py <tools/authcheck dir> <panel url> <lab name> <tools/kctl.sh>  (owner password: authcheck.py's)
"""
import json, subprocess, sys, time
sys.path.insert(0, sys.argv[1])
import authcheck as a
panel, lab, kctl = sys.argv[2], sys.argv[3], sys.argv[4]
b = a.Browser(panel)
st, js, _, _ = b.request("POST", "/api/auth/login", {"name": "owner", "password": "correct horse battery"})
print("login", st)
w = a.Ws(b)
w.until(lambda f: f["t"] == "ready")
w.send({"t": "sub", "id": 1, "topic": "metrics"})
w.until(lambda f: f["t"] == "res" and f.get("id") == 1)
m = w.until(lambda f: f["t"] == "push" and f["topic"] == "metrics", 10)
print("metrics push", json.dumps({k: m["data"].get(k) for k in ("stepsPerS", "tickMs", "players")}))
import threading
frames = []
def reader():
    try:
        while True:
            f = w.recv(60)
            if f is None: break
            frames.append(f.get("t"))
    except Exception as e:
        frames.append("end:" + type(e).__name__)
t = threading.Thread(target=reader, daemon=True)
t.start()
r = subprocess.run([kctl, lab, "rt3", "reload-test", "kilnwarden", "5"], capture_output=True, text=True, timeout=300)
print("reload-test", json.loads(r.stdout)["message"])
t.join(10)
print("socket close code", w.close_code, "frames before close", frames[-3:])
time.sleep(1)
b2 = a.Browser(panel)
b2.cookies = dict(b.cookies)
st, js, _, _ = b2.request("GET", "/api/auth/me")
print("session survives the reloads (sessions.json)", st, (js or {}).get("name"))
w2 = a.Ws(b2)
print("new socket after reload", w2.until(lambda f: f["t"] == "ready")["t"])
