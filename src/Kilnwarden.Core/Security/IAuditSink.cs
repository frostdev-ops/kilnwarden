// SPDX-License-Identifier: MIT
using System.Net;
using System.Text.Json.Nodes;

namespace Kilnwarden.Security;

/// <summary>Where write operations are recorded (DESIGN §5 Audit log; the auth component provides the JSONL store).
/// Called from I/O threads; must not block for long.</summary>
public interface IAuditSink
{
    void Record(AuditEntry entry);
}

/// <param name="User">The account id ("" for anonymous, e.g. a failed sign-in).</param>
/// <param name="Op">What ran: "players.kick", "core.settings.edit", "auth.login".</param>
/// <param name="Args">The arguments with secrets already redacted.</param>
public sealed record AuditEntry(DateTimeOffset At, string User, string UserName, string Op, JsonObject? Args, bool Ok, string? Error,
    double Ms, IPAddress? Client);

/// <summary>Records nothing (before an audit component is installed).</summary>
public sealed class NullAuditSink : IAuditSink
{
    public void Record(AuditEntry entry) { }
}
