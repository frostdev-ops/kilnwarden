// SPDX-License-Identifier: MIT
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Kilnwarden.Kap;

namespace Kilnwarden.Offline.Support;

/// <summary>A Kiln.Admin KAP/1 server for tests (admin-protocol.md): writes endpoint.json and admin-token into
/// <see cref="Dir"/>, checks the client's mac, proves the token in ready, records every frame, answers requests through
/// <see cref="Answer"/>, pushes topics on demand.</summary>
public sealed class FakeKap : IDisposable
{
    public static readonly string[] DefaultOps =
    [
        "ping", "status", "admin_info", "players", "kill", "tell", "broadcast", "command", "command_run", "command_complete", "commands_describe",
        "login_mint", "login_redeem", "admin_token_rotate", "server_restart", "server_restart_cancel", "mod_load", "update_list", "update_stage",
        "update_cancel", "update_rollback", "config_set", "brand_new_op",
    ];

    /// <summary>What a 0.2.0-dev.30.8 Kiln.Admin offers (no login, restart or update ops, no metrics topic, and
    /// commands_describe ignores source and level).</summary>
    public static readonly string[] OldOps =
        ["ping", "status", "admin_info", "players", "kill", "tell", "broadcast", "command", "command_run", "command_complete", "commands_describe", "admin_token_rotate"];

    /// <summary>Behave like a 30.8 Kiln.Admin: <see cref="OldOps"/>, topics state and log, level ignored.</summary>
    public void BeOld()
    {
        Ops = OldOps;
        Topics = ["state", "log"];
        IgnoresLevel = true;
    }

    /// <summary>A 30.8 Kiln.Admin ignores <c>level</c>; a 30.9 one refuses an unknown one.</summary>
    public bool IgnoresLevel;

    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<Conn, byte> _conns = new();
    public readonly ConcurrentQueue<JsonObject> Frames = new();
    public byte[] Token = RandomNumberGenerator.GetBytes(32);
    public string[] Ops = DefaultOps;
    public string[] Topics = ["state", "log", "metrics"];
    public JsonObject? Limits;
    public bool WrongReadyMac;
    public volatile int Connections;
    public volatile int Handshakes;

    /// <summary>The answer to a req frame: a res object without id (null: no answer at all).</summary>
    public Func<JsonObject, JsonObject?> Answer = req => new JsonObject
    {
        ["t"] = "res", ["ok"] = true, ["op"] = req["op"]!.GetValue<string>(), ["ms"] = 1, ["data"] = new JsonObject { ["args"] = req["args"]?.DeepClone() },
    };

    public FakeKap(string adminDir)
    {
        Dir = adminDir;
        Directory.CreateDirectory(adminDir);
        _listener.Start();
        WriteFiles();
        _ = AcceptAsync();
    }

    public string Dir { get; }
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void WriteFiles()
    {
        File.WriteAllText(Path.Combine(Dir, "admin-token"), Convert.ToHexStringLower(Token) + "\n");
        File.WriteAllText(Path.Combine(Dir, "endpoint.json"), new JsonObject
        {
            ["format"] = 1, ["protocol"] = "kap/1", ["host"] = "127.0.0.1", ["port"] = Port, ["token"] = "admin-token",
        }.ToJsonString());
    }

    public IEnumerable<JsonObject> Requests(string op) => Frames.Where(f => f["t"]?.GetValue<string>() == "req" && f["op"]?.GetValue<string>() == op);

    public int Subscribed(string topic) => _conns.Keys.Count(c => c.Topics.ContainsKey(topic));

    public void Push(string topic, JsonNode data)
    {
        foreach (var c in _conns.Keys)
            if (c.Topics.ContainsKey(topic))
                c.Send(new JsonObject { ["t"] = "push", ["topic"] = topic, ["seq"] = Interlocked.Increment(ref c.Seq), ["data"] = data.DeepClone() });
    }

    public void Bye(string reason)
    {
        foreach (var c in _conns.Keys)
        {
            c.Send(new JsonObject { ["t"] = "bye", ["reason"] = reason });
            c.Close();
        }
    }

    public void DropAll()
    {
        foreach (var c in _conns.Keys) c.Close();
    }

    private async Task AcceptAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient tcp;
            try
            {
                tcp = await _listener.AcceptTcpClientAsync(_cts.Token);
            }
            catch (Exception)
            {
                return;
            }
            var c = new Conn(tcp);
            _conns[c] = 0;
            Interlocked.Increment(ref Connections);
            _ = Task.Run(() => ServeAsync(c));
        }
    }

    private async Task ServeAsync(Conn c)
    {
        try
        {
            var nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            c.Send(new JsonObject
            {
                ["t"] = "hello", ["proto"] = "kap/1", ["nonce"] = nonce, ["kiln"] = "0.2.0-dev.30.9", ["admin"] = "0.2.0",
                ["server"] = new JsonObject { ["id"] = "srv-test", ["name"] = "Test Isle" }, ["auth"] = new JsonArray("hmac-sha256"),
            });
            var auth = await c.ReadAsync();
            Frames.Enqueue(auth);
            var cnonce = auth["cnonce"]!.GetValue<string>();
            var mac = Convert.FromHexString(auth["mac"]!.GetValue<string>());
            if (!CryptographicOperations.FixedTimeEquals(mac, Mac("kap1-auth", nonce, cnonce)))
            {
                c.Send(new JsonObject { ["t"] = "error", ["code"] = "auth_failed", ["message"] = "wrong mac" });
                c.Close();
                return;
            }
            var readyMac = Mac("kap1-ready", nonce, cnonce);
            if (WrongReadyMac) readyMac[0] ^= 1;
            c.Send(new JsonObject
            {
                ["t"] = "ready", ["session"] = "k-1", ["mac"] = Convert.ToHexStringLower(readyMac),
                ["ops"] = new JsonArray([.. Ops.Select(o => (JsonNode)o)]), ["topics"] = new JsonArray([.. Topics.Select(o => (JsonNode)o)]),
                ["limits"] = Limits?.DeepClone() ?? new JsonObject { ["maxFrame"] = 16 << 20, ["maxClientFrame"] = 1 << 20, ["inflight"] = 64, ["pingS"] = 15, ["idleS"] = 45 },
                ["restart"] = new JsonObject { ["supported"] = "unknown", ["via"] = null, ["code"] = null },
            });
            Interlocked.Increment(ref Handshakes);
            while (true)
            {
                var f = await c.ReadAsync();
                Frames.Enqueue(f);
                switch (f["t"]?.GetValue<string>())
                {
                    case "req":
                        var args = f["args"] as JsonObject;
                        if (!IgnoresLevel && args?["level"] is JsonValue lv && lv.GetValue<string>() is not ("everyone" or "admin" or "console"))
                        {
                            c.Send(new JsonObject
                            {
                                ["t"] = "res", ["id"] = f["id"]!.DeepClone(), ["ok"] = false, ["op"] = f["op"]!.DeepClone(), ["ms"] = 0,
                                ["error"] = "level: everyone, admin or console", ["code"] = "op_failed",
                            });
                            break;
                        }
                        var res = Answer(f);
                        if (res is not null)
                        {
                            res["id"] = f["id"]!.DeepClone();
                            c.Send(res);
                        }
                        break;
                    case "sub":
                        c.Topics[f["topic"]!.GetValue<string>()] = 0;
                        c.Send(new JsonObject { ["t"] = "res", ["id"] = f["id"]!.DeepClone(), ["ok"] = true, ["op"] = "sub" });
                        break;
                    case "unsub":
                        c.Topics.TryRemove(f["topic"]!.GetValue<string>(), out _);
                        c.Send(new JsonObject { ["t"] = "res", ["id"] = f["id"]!.DeepClone(), ["ok"] = true, ["op"] = "unsub" });
                        break;
                    case "ping":
                        c.Send(new JsonObject { ["t"] = "pong", ["id"] = f["id"]!.DeepClone(), ["ts"] = f["ts"]?.DeepClone() });
                        break;
                    case "bye":
                        c.Close();
                        return;
                }
            }
        }
        catch (Exception)
        {
            // closed
        }
        finally
        {
            _conns.TryRemove(c, out _);
            c.Close();
        }
    }

    private byte[] Mac(string label, string nonce, string cnonce) => HMACSHA256.HashData(Token, Encoding.ASCII.GetBytes(label + "\n" + nonce + "\n" + cnonce));

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        DropAll();
    }

    private sealed class Conn(TcpClient tcp)
    {
        private readonly NetworkStream _s = tcp.GetStream();
        private readonly object _write = new();
        public readonly ConcurrentDictionary<string, byte> Topics = new();
        public ulong Seq;

        public void Send(JsonObject f)
        {
            var payload = Encoding.UTF8.GetBytes(f.ToJsonString());
            var buf = new byte[4 + payload.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)payload.Length);
            payload.CopyTo(buf, 4);
            try
            {
                lock (_write) _s.Write(buf);
            }
            catch (Exception)
            {
                // closed
            }
        }

        public async Task<JsonObject> ReadAsync()
        {
            var head = new byte[4];
            await _s.ReadExactlyAsync(head);
            var payload = new byte[BinaryPrimitives.ReadUInt32LittleEndian(head)];
            await _s.ReadExactlyAsync(payload);
            return (JsonObject)JsonNode.Parse(payload)!;
        }

        public void Close()
        {
            try
            {
                tcp.Client.Shutdown(SocketShutdown.Both);
            }
            catch (Exception)
            {
            }
            tcp.Dispose();
        }
    }
}

public static class Eventually
{
    /// <summary>Polls <paramref name="cond"/> until true (fails after <paramref name="ms"/>).</summary>
    public static async Task True(Func<bool> cond, int ms = 5000, string? what = null)
    {
        var until = Environment.TickCount64 + ms;
        while (!cond())
        {
            if (Environment.TickCount64 > until) Assert.Fail($"timed out waiting for {what ?? "a condition"}");
            await Task.Delay(10);
        }
    }
}
