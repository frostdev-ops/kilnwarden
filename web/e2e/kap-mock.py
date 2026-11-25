#!/usr/bin/env python3
"""A Kiln.Admin 0.2.0-dev.30.9 for the web app's end-to-end tests: the kiln repo's KAP/1 mock
(tools/launcher/mock-admin.py: handshake, world, fixture) plus what 30.9 added (admin-protocol.md section 11): the
metrics topic and ready.restart, server_restart (+ cancel), login_mint/login_redeem, the update ops, and the command
ops' source and level (checked like Kiln.Admin: a level it does not know is refused, a source needs a level, a line
above the level is denied). Typed sample commands with the 30.9 form fields replace the fixture's catalog.

Every command op's source and level goes to <server>/kap-calls.jsonl, so the tests see what the panel sent.

    kap-mock.py serve --dir SERVER [--players N]     (KILN_UI_WORKTREE: the kiln worktree with mock-admin.py)
    kap-mock.py mint --dir SERVER [--consumer kilnwarden]   prints a one-time login secret (what the launcher does)
"""
from __future__ import annotations

import argparse
import asyncio
import importlib.util
import json
import math
import os
import random
import secrets
import sys
import time
from pathlib import Path

KILN = Path(os.environ.get("KILN_UI_WORKTREE") or sys.exit("KILN_UI_WORKTREE: a kiln checkout (tools/launcher/mock-admin.py)"))
_spec = importlib.util.spec_from_file_location("mock_admin", KILN / "tools/launcher/mock-admin.py")
ma = importlib.util.module_from_spec(_spec)
sys.modules["mock_admin"] = ma
_spec.loader.exec_module(ma)

LEVELS = {"everyone": 0, "admin": 1, "console": 2}
NEW_OPS = ["login_mint", "login_redeem", "server_restart", "server_restart_cancel", "update_list", "update_stage", "update_cancel", "update_rollback",
           "command_run", "command_complete", "commands_describe"]


def arg(name, type_, **kw):
    return {"kind": "argument", "name": name, "type": type_, **kw}


def lit(name, **kw):
    return {"kind": "literal", "name": name, **kw}


def cmd(name, owner, level, tree_children=None, executable=False, description="", aliases=None, typed=True, usage=""):
    c = {"name": name, "aliases": aliases or [], "usage": usage, "description": description, "permission": level,
         "node": f"{owner}.command.{name}", "owner": owner, "typed": typed, "usageLines": [f"/{name} {usage}".strip()]}
    if typed:
        c["tree"] = {"kind": "literal", "name": name, "executable": executable, "description": description, "permission": c["node"], "level": level,
                     "children": tree_children or []}
    return c


CATALOG = [
    cmd("help", "kiln", "everyone", [arg("command", "word", executable=True, optional=True, describe="one word",
                                         children=[arg("sub", "greedy", executable=True, optional=True, greedy=True, quotable=False, describe="text to the end of the line")])],
        executable=True, description="lists the commands you can use, or explains one", aliases=["?"], usage="[command] [sub]"),
    cmd("say", "kiln", "everyone", [arg("text", "greedy", executable=True, greedy=True, quotable=False)], description="says something to everyone", usage="<text>"),
    cmd("tp", "kiln", "admin", [arg("target", "player", executable=True, many=False, selectors=["@s", "@p", "@r"]),
                                arg("x", "double", children=[arg("y", "double", children=[arg("z", "double", executable=True)])])],
        description="teleports you to a player or a place", usage="<target> | <x> <y> <z>"),
    cmd("give", "kiln", "admin", [arg("player", "player", selectors=["@s", "@p", "@r"], children=[
        arg("item", "item", executable=True, children=[arg("count", "int", min=1, max=9999, optional=True, default=1, executable=True)])])],
        description="gives an item", usage="<player> <item> [count]"),
    cmd("kick", "kiln", "admin", [arg("player", "player", executable=True, selectors=["@s", "@p", "@r"],
                                      children=[arg("reason", "greedy", executable=True, optional=True, greedy=True)])],
        description="kicks a player", usage="<player> [reason]"),
    cmd("weather", "kiln", "admin", [arg("kind", "enum", options=["clear", "rain", "storm"], executable=True)], description="sets the weather", usage="<kind>"),
    cmd("time", "kiln", "admin", [lit("set", children=[arg("time", "time", unit="h", min=0, max=24, executable=True)]),
                                  lit("add", children=[arg("duration", "duration", unit="s", min=1, max=86400, executable=True)]),
                                  lit("query", executable=True, permission="kiln.command.time.query", level="everyone")],
        description="the time of day", usage="set <time> | add <duration> | query"),
    cmd("zone", "claims", "admin", [
        lit("create", children=[arg("name", "string", quotable=True, greedy=False, children=[arg("at", "position", relative=True, children=[
            arg("radius", "double", min=1, max=500, optional=True, default=10, executable=True)])])]),
        lit("delete", children=[arg("name", "word", executable=True)]),
        lit("list", executable=True, level="everyone")], description="protected areas", usage="create <name> <at> [radius] | delete <name> | list"),
    cmd("stop", "kiln", "console", [], executable=True, description="saves and stops the server"),
    cmd("claim", "claims", "everyone", typed=False, description="Protected areas (Claims sample)", usage="list | here <name> [radius]"),
]


class World30(ma.World):
    def __init__(self, fixture, players=2, calls: Path | None = None):
        super().__init__(fixture, players)
        for op in NEW_OPS:
            if op not in self.ops:
                self.ops.append(op)
        self.calls = calls
        self.minted: dict[str, dict] = {}
        self.restart = None

    def record(self, op, req):
        if self.calls:
            with self.calls.open("a") as f:
                f.write(json.dumps({"op": op, "source": req.get("source"), "level": req.get("level"), "line": req.get("line"),
                                    "text": req.get("text"), "steamId": req.get("steamId"), "at": time.time()}) + "\n")

    @staticmethod
    def cap(req):
        """Kiln.Admin's Ops.Level/SourceName: the level as a rank (None: the old full-admin dashboard)."""
        lv = req.get("level")
        src = req.get("source")
        if lv is None:
            if src is not None:
                raise ma.AdminError("source needs a level (everyone, admin or console)")
            return None
        if not isinstance(lv, str) or lv.lower() not in LEVELS:
            raise ma.AdminError("level: everyone, admin or console")
        if src is not None and (not isinstance(src, str) or not src or len(src) > 80 or "\n" in src):
            raise ma.AdminError("source: one line, at most 80 characters")
        return LEVELS[lv.lower()]

    def commands(self):
        return CATALOG

    def op_commands_describe(self, req):
        self.record("commands_describe", req)
        cap = self.cap(req)
        return [c for c in CATALOG if cap is None or LEVELS[c["permission"]] <= cap]

    def op_command_complete(self, req):
        self.record("command_complete", req)
        self.cap(req)
        return super().op_command_complete(req)

    def op_command_run(self, req):
        self.record("command_run", req)
        cap = self.cap(req)
        line = req.get("line")
        if not isinstance(line, str) or not line.strip():
            raise ma.AdminError("line required")
        name = line.strip().lstrip("/").split(" ")[0].lower()
        c = next((c for c in CATALOG if c["name"] == name or name in c["aliases"]), None)
        src = req.get("source") or "dashboard"
        lvl = req.get("level") or "admin"
        if c is None:
            return {"name": name, "found": False, "ok": False, "denied": False, "canceled": False, "parseError": None, "error": f"unknown command '{name}'", "output": [], "source": src}
        if cap is not None and LEVELS[c["permission"]] > cap:
            return {"name": c["name"], "found": True, "ok": False, "denied": True, "canceled": False, "parseError": None,
                    "error": f"you may not run /{c['name']} (needs {c['permission']})", "output": [], "source": src}
        self.say(f"command {line} by remote {src} (up to {lvl}): ok")
        return {"name": c["name"], "found": True, "ok": True, "denied": False, "canceled": False, "parseError": None, "error": None,
                "output": [f"(mock) ran {line.strip()} as {src} up to {lvl}"], "source": src}

    def op_login_mint(self, req):
        consumer = req.get("consumer")
        if not isinstance(consumer, str) or not consumer:
            raise ma.AdminError("consumer required")
        s = secrets.token_hex(32)
        self.minted[s] = {"consumer": consumer, "label": req.get("label"), "at": time.time()}
        return {"secret": s, "consumer": consumer, "expiresAt": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(time.time() + 60)), "ttlS": 60}

    def op_login_redeem(self, req):
        m = self.minted.pop(str(req.get("secret")), None)
        if not m or m["consumer"] != req.get("consumer") or time.time() - m["at"] > 60:
            raise ma.AdminError("that login secret is wrong, used or expired")
        return {"role": "owner", "consumer": m["consumer"], "label": m["label"], "mintedBy": "kap-mock.py", "mintedAt": m["at"]}

    def op_server_restart(self, req):
        secs = req.get("seconds", 60)
        self.restart = {"state": "counting", "by": req.get("by") or "web", "requested": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                        "secondsLeft": secs, "message": req.get("message"), "error": None, "via": None,
                        "restart": {"supported": "unknown", "via": None, "code": None}}
        return self.restart

    def op_server_restart_cancel(self, _):
        if not self.restart:
            raise ma.AdminError("no restart is counting down")
        self.restart = None
        return {"canceled": True}

    def op_update_list(self, _):
        return {"staged": [], "history": [], "kiln": self.kiln}

    def op_update_stage(self, _):
        raise ma.AdminError("the mock does not stage updates")

    op_update_cancel = op_update_rollback = op_update_stage


def metrics_sample(players: int) -> dict:
    """One `metrics` push in Kiln.Admin's shape (admin-protocol.md, metrics: "at" in ms, frame and tick times, memory,
    CPU, GC). Invented numbers that wander a little, so charts have something to draw."""
    t = time.time()
    w = math.sin(t / 47.0) + 0.5 * math.sin(t / 13.0)
    tick = 0.7 + 0.25 * w + random.random() * 0.2
    frame = 16.6 + 0.8 * w + random.random() * 0.6
    return {"at": int(t * 1000), "windowS": 2.0, "steps": 120, "stepsPerS": round(59.6 + 0.3 * random.random(), 2),
            "frameMs": {"avg": round(frame, 2), "p95": round(frame + 1.4, 2), "max": round(frame + 3.5, 2)},
            "tickMs": {"avg": round(tick, 2), "p95": round(tick * 3.6, 2), "max": round(tick * 9, 2)},
            "memory": {"workingSet": int(1.06e9 + 4e7 * w), "managedHeap": int(2.1e7 + 3e6 * w)},
            "cpu": {"percent": round(34 + 8 * w + 4 * random.random(), 1), "cores": 16},
            "gcPauseMs": round(max(0.0, 2.5 + 2 * w), 1), "players": players}


class Server30(ma.MockServer):
    async def send(self, c, obj):
        if isinstance(obj, dict) and obj.get("t") == "ready":
            obj = dict(obj, topics=["state", "log", "metrics"], restart={"supported": "unknown", "via": None, "code": None})
            c.seq.setdefault("metrics", 0)
        await super().send(c, obj)

    async def dispatch(self, c, f):
        if f.get("t") in ("sub", "unsub") and f.get("topic") == "metrics" and isinstance(f.get("id"), int):
            (c.sub.add if f["t"] == "sub" else c.sub.discard)("metrics")
            await self.send(c, {"t": "res", "id": f["id"], "ok": True, "op": f["t"]})
            return
        await super().dispatch(c, f)

    async def ticker(self):
        async def metrics():
            while True:
                await asyncio.sleep(2.0)
                for c in list(self.conns):
                    if c.ready and not c.closed and "metrics" in c.sub:
                        c.seq["metrics"] = c.seq.get("metrics", 0) + 1
                        await self.send(c, {"t": "push", "topic": "metrics", "seq": c.seq["metrics"],
                                            "data": metrics_sample(len(self.world.players))})
        m = asyncio.ensure_future(metrics())
        try:
            await super().ticker()
        finally:
            m.cancel()


async def serve(args) -> int:
    vec = json.loads(ma.VECTOR.read_text())
    token_hex = secrets.token_hex(32)
    base = Path(args.dir)
    world = World30(ma.load_fixture(None), players=args.players, calls=base / "kap-calls.jsonl")
    world.server = {"id": "srv-e2e", "name": os.environ.get("KWA_MOCK_SERVER_NAME") or "Embervale (mock)"}
    srv = Server30(world, bytes.fromhex(token_hex), False, args.quiet)
    server = await asyncio.start_server(srv.handle, "127.0.0.1", 0)
    srv.port = server.sockets[0].getsockname()[1]
    ep = ma.write_endpoint(base, srv.port, token_hex, world.server)
    print(f"[kap-mock] Kiln.Admin 30.9 mock on 127.0.0.1:{srv.port} ({len(world.ops)} ops); endpoint {ep}", flush=True)
    del vec
    tick = asyncio.ensure_future(srv.ticker())
    try:
        async with server:
            await server.serve_forever()
    except asyncio.CancelledError:
        pass
    finally:
        tick.cancel()
        await srv.bye_all()
        ep.unlink(missing_ok=True)
    return 0


def mint(args) -> int:
    host, port, token = ma.endpoint_of(argparse.Namespace(server=args.dir))
    c = ma.Client(host, port, token)
    r = c.call("login_mint", {"consumer": args.consumer, "label": "e2e"})
    c.close()
    if not r.get("ok"):
        print(json.dumps(r), file=sys.stderr)
        return 1
    print(r["data"]["secret"])
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    sub = ap.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("serve")
    s.add_argument("--dir", required=True)
    s.add_argument("--players", type=int, default=2)
    s.add_argument("--quiet", action="store_true")
    m = sub.add_parser("mint")
    m.add_argument("--dir", required=True)
    m.add_argument("--consumer", default=os.environ.get("KWA_CONSUMER") or "kilnwarden")
    a = ap.parse_args()
    if a.cmd == "serve":
        try:
            return asyncio.run(serve(a))
        except KeyboardInterrupt:
            return 0
    return mint(a)


if __name__ == "__main__":
    sys.exit(main())
