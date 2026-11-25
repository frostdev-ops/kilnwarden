// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;

namespace Kilnwarden.Kap;

/// <summary>A request's answer: Kiln.Admin's <c>res</c> frame, or one the client made itself (not connected, busy,
/// timeout, refused). <see cref="Frame"/> is the res object as it would go to a page (its <c>id</c> is the caller's to
/// set).</summary>
public sealed class KapResult
{
    private KapResult(JsonObject frame)
    {
        Frame = frame;
    }

    /// <summary>The res frame: {t:"res", ok, op, ms?, data | error, code?}.</summary>
    public JsonObject Frame { get; }

    public bool Ok => KapFrames.Bool(Frame, "ok") == true;
    public string Op => KapFrames.Str(Frame, "op") ?? "";

    /// <summary>The KAP error code on failure (op_failed, unknown_op, busy, timeout, shutting_down, too_large; and the
    /// client's own: forbidden, refused).</summary>
    public string? Code => KapFrames.Str(Frame, "code");

    public string? Error => KapFrames.Str(Frame, "error");
    public JsonNode? Data => Frame["data"];

    /// <summary>From Kiln.Admin's res frame (the frame is kept as it came, minus nothing).</summary>
    public static KapResult FromFrame(JsonObject res) => new(res);

    /// <summary>A failure made on this side.</summary>
    public static KapResult Fail(string op, string code, string error) =>
        new(new JsonObject { ["t"] = "res", ["ok"] = false, ["op"] = op, ["ms"] = 0, ["error"] = error, ["code"] = code });

    public static KapResult Unavailable(string op) => Fail(op, "shutting_down", "Kiln.Admin is not connected");

    public override string ToString() => Ok ? $"{Op}: ok" : $"{Op}: {Code} {Error}";
}

/// <summary>What Kiln.Admin said in its handshake: hello (versions, server) and ready (ops, topics, limits, restart).</summary>
public sealed class KapSession
{
    public required JsonObject Hello { get; init; }
    public required JsonObject Ready { get; init; }
    public required IReadOnlySet<string> Ops { get; init; }
    public required IReadOnlyList<string> Topics { get; init; }
    public required KapLimits Limits { get; init; }

    /// <summary>A counter per connection (1 for the first), so listeners can tell a reconnect.</summary>
    public required long Generation { get; init; }

    public string? Kiln => KapFrames.Str(Hello, "kiln");
    public string? Admin => KapFrames.Str(Hello, "admin");
    public JsonNode? Server => Hello["server"];
    public JsonNode? Restart => Ready["restart"];
}

/// <summary>ready.limits, clamped to what this client accepts.</summary>
public sealed record KapLimits(int MaxFrame, int MaxClientFrame, int Inflight, int PingS, int IdleS)
{
    public static KapLimits Default { get; } = new(KapFrames.MaxServerFrame, KapFrames.MaxClientFrame, 64, 15, 45);

    public static KapLimits From(JsonObject? limits)
    {
        if (limits is null) return Default;
        int Clamp(string name, int def, int min, int max) => Math.Clamp(KapFrames.Int(limits, name) ?? def, min, max);
        var ping = Clamp("pingS", 15, 1, 300);
        return new KapLimits(
            Clamp("maxFrame", Default.MaxFrame, 64 << 10, KapFrames.MaxServerFrame),
            Clamp("maxClientFrame", Default.MaxClientFrame, 4 << 10, KapFrames.MaxClientFrame),
            Clamp("inflight", 64, 1, 256),
            ping,
            Clamp("idleS", 45, ping * 2, 3600));
    }
}

/// <summary>One push of a topic, shared by every subscriber: <see cref="Payload"/> is the frame's exact JSON (what a
/// page gets as its WebSocket message), serialized once. Do not change it.</summary>
public sealed record KapPush(string Topic, ulong Seq, ReadOnlyMemory<byte> Payload);
