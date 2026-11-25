#!/usr/bin/env python3
"""Lab check of the auth lane against a running lab server with Kiln.Admin (30.9 KAP) and kilnwarden.

    authcheck.py <server dir> <panel url> [--kiln-repo DIR]

Runs, in order, against the live panel (TLS certificate not verified: self-signed) and the real Kiln.Admin:
  first-run setup link from kiln.log -> owner; wrong/unknown sign-ins look the same; CSRF refusals; the
  launcher's one-time login (login_mint over KAP with the admin token, as the launcher does, then
  /api/auth/launcher); a moderator account; the KAP WebSocket as moderator and owner: ready filtered, ops gated,
  command_run reaching Kiln.Admin as web:<user> at the role's level (Kiln.Admin's own answer and kiln.log say so),
  a state push; sign-out closing the socket with 4401; the audit log. Prints one JSON line per check and a summary;
  exit 1 on any failure. Needs only the Python standard library.
"""
import base64, hashlib, hmac, http.client, json, os, re, socket, ssl, struct, subprocess, sys, time, urllib.parse

CTX = ssl.create_default_context()
CTX.check_hostname = False
CTX.verify_mode = ssl.CERT_NONE
results = []


def totp(secret_b32, step):
    key = base64.b32decode(secret_b32 + "=" * (-len(secret_b32) % 8))
    mac = hmac.new(key, struct.pack(">Q", step), hashlib.sha1).digest()
    off = mac[19] & 15
    return "%06d" % ((struct.unpack(">I", mac[off:off + 4])[0] & 0x7FFFFFFF) % 1000000)


def check(name, ok, **info):
    results.append(ok)
    print(json.dumps({"check": name, "ok": bool(ok), **info}), flush=True)
    return ok


class Browser:
    def __init__(self, panel):
        u = urllib.parse.urlparse(panel)
        self.host, self.port, self.origin = u.hostname, u.port, f"{u.scheme}://{u.hostname}:{u.port}"
        self.cookies = {}

    def request(self, method, path, body=None, origin=True, extra=None):
        c = http.client.HTTPSConnection(self.host, self.port, context=CTX, timeout=30)
        headers = {"Host": f"{self.host}:{self.port}"}
        if origin: headers["Origin"] = self.origin
        if self.cookies: headers["Cookie"] = "; ".join(f"{k}={v}" for k, v in self.cookies.items())
        data = None
        if body is not None:
            data = json.dumps(body).encode()
            headers["Content-Type"] = "application/json"
        headers.update(extra or {})
        c.request(method, path, body=data, headers=headers)
        r = c.getresponse()
        raw = r.read()
        for h, v in r.getheaders():
            if h.lower() == "set-cookie":
                name, _, rest = v.partition("=")
                value = rest.split(";")[0]
                if "Max-Age=0" in v: self.cookies.pop(name, None)
                else: self.cookies[name] = value
        try: js = json.loads(raw) if raw else None
        except ValueError: js = None
        return r.status, js, dict((h.lower(), v) for h, v in r.getheaders()), r.getheader("Set-Cookie") or ""


class Ws:
    """A minimal RFC 6455 client (text frames, masked, close codes)."""

    def __init__(self, b, path="/api/server/kap"):
        raw = socket.create_connection((b.host, b.port), timeout=15)
        self.s = CTX.wrap_socket(raw, server_hostname=b.host)
        key = base64.b64encode(os.urandom(16)).decode()
        cookie = "; ".join(f"{k}={v}" for k, v in b.cookies.items())
        req = (f"GET {path} HTTP/1.1\r\nHost: {b.host}:{b.port}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
               f"Sec-WebSocket-Key: {key}\r\nSec-WebSocket-Version: 13\r\nOrigin: {b.origin}\r\n" + (f"Cookie: {cookie}\r\n" if cookie else "") + "\r\n")
        self.s.sendall(req.encode())
        head = b""
        while b"\r\n\r\n" not in head:
            head += self.s.recv(1)
        self.status = int(head.split(b" ")[1])
        self.close_code = None
        self.next_id = 0

    def send(self, obj):
        payload = json.dumps(obj).encode()
        mask = os.urandom(4)
        n = len(payload)
        hdr = bytes([0x81]) + (bytes([0x80 | n]) if n < 126 else bytes([0x80 | 126]) + struct.pack(">H", n) if n < 65536 else bytes([0x80 | 127]) + struct.pack(">Q", n))
        self.s.sendall(hdr + mask + bytes(c ^ mask[i % 4] for i, c in enumerate(payload)))

    def _exact(self, n):
        buf = b""
        while len(buf) < n:
            chunk = self.s.recv(n - len(buf))
            if not chunk: raise EOFError
            buf += chunk
        return buf

    def recv(self, timeout=15):
        self.s.settimeout(timeout)
        while True:
            b0, b1 = self._exact(2)
            n = b1 & 0x7F
            if n == 126: n = struct.unpack(">H", self._exact(2))[0]
            elif n == 127: n = struct.unpack(">Q", self._exact(8))[0]
            data = self._exact(n)
            op = b0 & 0x0F
            if op == 8:
                self.close_code = struct.unpack(">H", data[:2])[0] if len(data) >= 2 else 1005
                try:  # echo the close, as a browser does (RFC 6455 5.5.1)
                    mask = os.urandom(4)
                    self.s.sendall(bytes([0x88, 0x80 | len(data[:2])]) + mask + bytes(c ^ mask[i % 4] for i, c in enumerate(data[:2])))
                except OSError:
                    pass
                return None
            if op == 9:  # ping -> pong
                mask = os.urandom(4)
                self.s.sendall(bytes([0x8A, 0x80 | len(data)]) + mask + bytes(c ^ mask[i % 4] for i, c in enumerate(data)))
                continue
            if op in (1, 2): return json.loads(data)

    def until(self, pred, timeout=15):
        end = time.time() + timeout
        while time.time() < end:
            f = self.recv(max(0.1, end - time.time()))
            if f is None: return None
            if pred(f): return f
        raise TimeoutError

    def call(self, op, args=None):
        self.next_id += 1
        i = self.next_id
        self.send({"t": "req", "id": i, "op": op, "args": args or {}})
        return self.until(lambda f: f.get("t") == "res" and f.get("id") == i, 60)


def main():
    server, panel = sys.argv[1], sys.argv[2]
    repo = sys.argv[sys.argv.index("--kiln-repo") + 1] if "--kiln-repo" in sys.argv else None
    log = os.path.join(server, "kiln", "logs", "kiln.log")
    # every step comes from one address: lift the per-address sign-in bucket (10/min) through auth.json's hot reload
    auth_json = os.path.join(server, "kiln", "data", "kilnwarden", "auth.json")
    cfg = json.load(open(auth_json))
    if cfg.get("signIn", {}).get("attemptsPerMinutePerIp") != 600:
        cfg.setdefault("signIn", {})["attemptsPerMinutePerIp"] = 600
        before = open(log, encoding="utf-8", errors="replace").read().count("auth.json: reloaded")
        json.dump(cfg, open(auth_json, "w"), indent=2)
        end = time.time() + 15
        while open(log, encoding="utf-8", errors="replace").read().count("auth.json: reloaded") == before and time.time() < end: time.sleep(0.5)
        check("auth.json hot reload (sign-in bucket raised for this script)", time.time() < end)
    text = open(log, encoding="utf-8", errors="replace").read()
    tokens = re.findall(r"setup#token=([A-Za-z0-9_-]{43})", text)
    full = re.findall(r"(https://[^ ]+/setup#token=[A-Za-z0-9_-]{43})", text)
    check("setup link in kiln.log with the panel address", bool(full), link=(full[-1].split("#")[0] + "#token=...") if full else None)
    owner = Browser(panel)
    st, js, _, _ = owner.request("GET", "/api/auth/status")
    check("status: no owner yet", st == 200 and js and js["ownerExists"] is False, status=st)
    st, js, h, _ = owner.request("POST", "/api/auth/setup", {"token": "A" * 43, "name": "owner", "password": "correct horse battery"})
    check("wrong setup token refused", st == 401, status=st)
    st, js, h, cookie = owner.request("POST", "/api/auth/setup", {"token": tokens[-1], "name": "owner", "password": "correct horse battery"})
    check("setup link creates the owner", st == 200 and "__Host-wa-session" in owner.cookies, status=st)
    check("session cookie is __Host-, Secure, HttpOnly, SameSite=Strict",
          all(x in cookie for x in ("__Host-wa-session=", "Secure", "HttpOnly", "SameSite=Strict", "Path=/")), cookie=re.sub(r"=[A-Za-z0-9_-]{43}", "=...", cookie))
    st, js, h, _ = owner.request("GET", "/api/auth/me")
    check("owner signed in, console level", st == 200 and js["owner"] and js["commandLevel"] == "console", status=st)
    check("security headers on API answers", "content-security-policy" in h and h.get("cache-control") == "no-store", csp=h.get("content-security-policy", "")[:40])
    st, js, _, _ = owner.request("POST", "/api/auth/setup", {"token": tokens[-1], "name": "owner2", "password": "correct horse battery"})
    check("setup link works once", st in (401, 409), status=st)

    # uniform sign-in refusals
    anon = Browser(panel)
    a = anon.request("POST", "/api/auth/login", {"name": "owner", "password": "wrong password!"})
    b = anon.request("POST", "/api/auth/login", {"name": "nobody", "password": "wrong password!"})
    check("wrong password and unknown name look the same", a[0] == b[0] == 401 and a[1] == b[1], body=a[1])
    st, js, _, _ = anon.request("POST", "/api/auth/login", {"name": "owner", "password": "correct horse battery"}, origin=False)
    check("POST without Origin refused (CSRF)", st == 403 and js["error"]["code"] == "csrf", status=st)
    st, js, _, _ = anon.request("POST", "/api/auth/login", {"name": "owner", "password": "correct horse battery"}, extra={"Origin": "https://evil.example"})
    check("POST from another origin refused (CSRF)", st == 403, status=st)

    # the launcher's one-time login: mint over KAP with the admin token (the launcher's part), redeem through the panel
    if repo:
        out = subprocess.run([sys.executable, os.path.join(repo, "tools/launcher/mock-admin.py"), "call", "--server", server, "login_mint",
                              json.dumps({"consumer": "kilnwarden", "label": "authcheck"})], capture_output=True, text=True, timeout=60)
        m = re.search(r'"secret":\s*"([0-9a-f]{64})"', out.stdout)
        check("login_mint over KAP (launcher side)", m is not None, out=out.stdout[-200:] if not m else "ok")
        if m:
            launcher = Browser(panel)
            st, js, _, _ = launcher.request("POST", "/api/auth/launcher", {"secret": m.group(1)})
            ok = st == 200 and (js or {}).get("user", {}).get("name") == "owner"
            check("launcher secret redeemed into an owner session", ok, status=st, **({} if ok else {"body": js}))
            st, js, _, _ = launcher.request("GET", "/api/auth/me")
            check("launcher session via=launcher", st == 200 and js["session"]["via"] == "launcher")
            st, js, _, _ = Browser(panel).request("POST", "/api/auth/launcher", {"secret": m.group(1)})
            check("a used launcher secret is refused", st == 401, status=st)

    # a moderator
    st, js, _, _ = owner.request("POST", "/api/users", {"name": "mod1", "password": "temporary pass 123", "roles": ["moderator"]})
    check("owner creates a moderator", st == 201, status=st)
    mod = Browser(panel)
    mod.request("POST", "/api/auth/login", {"name": "mod1", "password": "temporary pass 123"})
    st, js, _, _ = mod.request("GET", "/api/core/modules")
    check("a new account must change its password first", st == 403 and js["error"]["code"] == "password_change_required", status=st)
    st, js, _, _ = mod.request("POST", "/api/auth/password", {"current": "temporary pass 123", "password": "moderator pass 456"})
    check("password changed", st == 200, status=st)
    st, js, _, _ = mod.request("GET", "/api/users")
    check("moderator cannot list users", st == 403, status=st)

    # two-factor (TOTP is HMAC-SHA1: BCrypt under Wine too)
    owner.request("POST", "/api/users", {"name": "tfa1", "password": "temporary pass 123", "roles": ["admin"]})
    tfa = Browser(panel)
    tfa.request("POST", "/api/auth/login", {"name": "tfa1", "password": "temporary pass 123"})
    tfa.request("POST", "/api/auth/password", {"current": "temporary pass 123", "password": "second factor 789"})
    st, js, _, _ = tfa.request("POST", "/api/auth/2fa/setup", {"password": "second factor 789"})
    secret = js.get("secret") if js else None
    check("2fa setup returns a secret and an otpauth uri", st == 200 and secret and js["uri"].startswith("otpauth://totp/"), status=st)
    step = int(time.time()) // 30
    st, js, _, _ = tfa.request("POST", "/api/auth/2fa/enable", {"code": totp(secret, step)})
    codes = (js or {}).get("recoveryCodes") or []
    check("2fa enabled with a code from the secret (the server's HMAC-SHA1 agrees)", st == 200 and len(codes) == 10, status=st)
    tfa.request("POST", "/api/auth/logout")
    st, js, _, _ = tfa.request("POST", "/api/auth/login", {"name": "tfa1", "password": "second factor 789"})
    check("password alone asks for the code", st == 200 and js.get("next") == "2fa" and "__Host-wa-2fa" in tfa.cookies, status=st)
    st, js, _, _ = tfa.request("POST", "/api/auth/login/2fa", {"code": totp(secret, step)})
    check("the code used to enable is not accepted again", st == 401, status=st)
    st, js, _, _ = tfa.request("POST", "/api/auth/login/2fa", {"code": totp(secret, step + 1)})
    check("the next code signs in", st == 200 and js["user"]["name"] == "tfa1", status=st)

    # the KAP socket
    w = Ws(mod)
    hello = w.until(lambda f: f["t"] == "hello")
    ready = w.until(lambda f: f["t"] == "ready")
    ops = set(ready["ops"])
    check("moderator socket: hello + ready", w.status == 101 and hello["proto"] == "kap/1", ops=len(ops), topics=ready["topics"], kiln=hello.get("kiln"))
    check("ready hides ops the moderator may not use", not ops & {"kill", "login_mint", "login_redeem", "command", "admin_token_rotate", "mod_load", "config_set"}
          and {"players", "command_run", "tell"} <= ops)
    r = w.call("players")
    check("players (allowed) answered by Kiln.Admin", r.get("ok") is True, ms=r.get("ms"))
    r = w.call("kill", {"entity": 1})
    check("kill refused by the gate", r.get("ok") is False and r.get("code") == "forbidden", code=r.get("code"))
    r = w.call("login_mint", {"consumer": "kilnwarden"})
    check("login_mint never reachable from a page", r.get("ok") is False and r.get("code") == "forbidden")
    r = w.call("command_run", {"line": "help", "source": "dashboard", "level": "console"})
    check("command_run reaches Kiln.Admin as web:mod1 (its own answer)", r.get("ok") is True and r["data"].get("source") == "web:mod1",
          source=(r.get("data") or {}).get("source"), found=(r.get("data") or {}).get("found"))
    r = w.call("commands_describe")
    levels = sorted({c.get("permission") for c in (r.get("data") or [])})
    check("commands_describe as the moderator lists only everyone-level commands", r.get("ok") is True and levels in (["everyone"], []), levels=levels,
          count=len(r.get("data") or []))
    w.send({"t": "sub", "id": 900, "topic": "state"})
    res = w.until(lambda f: f["t"] == "res" and f.get("id") == 900)
    push = w.until(lambda f: f["t"] == "push" and f["topic"] == "state", 10)
    check("state topic pushed through", res.get("ok") and push is not None, keys=sorted(push["data"].keys())[:6] if push else None)
    w.send({"t": "sub", "id": 901, "topic": "log"})
    res = w.until(lambda f: f["t"] == "res" and f.get("id") == 901)
    check("log topic refused for a moderator", res.get("ok") is False and res.get("code") == "forbidden")

    wo = Ws(owner)
    wo.until(lambda f: f["t"] == "ready")
    r = wo.call("command_run", {"line": "help"})
    check("owner's command_run runs as web:owner", r.get("ok") is True and r["data"].get("source") == "web:owner")
    r = wo.call("server_restart_cancel", {"by": "spoofed"})
    check("server_restart_cancel answered (nothing counting)", r.get("op") == "server_restart_cancel", answered_ok=r.get("ok"), error=r.get("error"))

    # sign out closes the moderator's socket with 4401 (the periodic check)
    mod.request("POST", "/api/auth/logout")
    end = time.time() + 15
    while w.close_code is None and time.time() < end:
        try: w.recv(1)
        except (TimeoutError, socket.timeout): pass
        except (EOFError, OSError): break
    check("sign-out closes the open socket with 4401", w.close_code == 4401, code=w.close_code)
    anonws = Ws(Browser(panel))
    while anonws.recv() is not None: pass
    check("a socket without a session closes 4401", anonws.close_code == 4401, code=anonws.close_code)

    # Kiln.Admin's log names the web user and the level cap
    time.sleep(1)
    text = open(log, encoding="utf-8", errors="replace").read()
    check("kiln.log: Kiln.Admin ran the line as web:mod1 up to everyone", "web:mod1 command (up to everyone): help" in text)
    lines_with = [i for i, l in enumerate(text.splitlines()) if tokens[-1] in l]
    created = next((i for i, l in enumerate(text.splitlines()) if "owner account 'owner' created" in l), None)
    check("kiln.log: the setup token only before the owner exists", created is not None and all(i < created for i in lines_with), lines=len(lines_with))
    audit = os.path.join(server, "kiln", "data", "kilnwarden", "audit", "audit.jsonl")
    lines = [json.loads(l) for l in open(audit) if l.strip()]
    ops_seen = {l["op"] for l in lines}
    check("audit log has sign-ins, failures, setup, account changes, KAP writes and refusals",
          {"auth.setup", "auth.login", "users.create", "auth.password", "auth.2fa_enable", "auth.2fa", "kap.command_run", "kap.kill", "auth.launcher" if repo else "auth.setup"} <= ops_seen,
          entries=len(lines), ops=sorted(ops_seen))
    check("no secret in the audit log", not any(t in open(audit).read() for t in tokens) and "correct horse" not in open(audit).read())
    print(json.dumps({"summary": f"{sum(results)}/{len(results)} passed"}))
    return 0 if all(results) else 1


if __name__ == "__main__":
    sys.exit(main())
