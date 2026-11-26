// SPDX-License-Identifier: MIT
namespace Kilnwarden.Setup;

/// <summary>The console-only setup over hosted access (0.1.2, <c>kilnwarden setup hosted &lt;name&gt;</c>): while there is
/// no owner, the relay link may come up and the Let's Encrypt certificate may be ordered, so the owner can then create
/// the account at https://&lt;name&gt;.kiln.nexus. Only the server console opens it (whoever can type there controls the
/// server anyway), for an hour at a time, in memory only (a restart closes it); it never opens a port, and the setup
/// page it makes reachable still needs a one-time link that only the console gives out (AuthService.SetupScope).</summary>
public sealed class HostedSetupWindow(TimeProvider time, Func<bool> ownerExists)
{
    public static readonly TimeSpan Length = TimeSpan.FromHours(1);

    private long _until;

    /// <summary>Open: no owner yet and the console opened it less than an hour ago.</summary>
    public bool Open => !ownerExists() && time.GetUtcNow().UtcTicks < Interlocked.Read(ref _until);

    /// <summary>When it closes (null when it is closed).</summary>
    public DateTimeOffset? Until => Open ? new DateTimeOffset(Interlocked.Read(ref _until), TimeSpan.Zero) : null;

    /// <summary>Opens it, or keeps it open for another hour from now. False once the owner exists (nothing to open).</summary>
    public bool Extend()
    {
        if (ownerExists()) return false;
        Interlocked.Exchange(ref _until, (time.GetUtcNow() + Length).UtcTicks);
        return true;
    }

    public void Close() => Interlocked.Exchange(ref _until, 0);
}
