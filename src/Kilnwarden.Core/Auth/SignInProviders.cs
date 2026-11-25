// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Kilnwarden.Auth;

/// <summary>An identity another service vouched for (step 2: Steam OpenID, Discord through frostdev.io's broker). The
/// provider has verified it before it reaches <see cref="AuthService.SignInExternal"/>; this side only maps it to a
/// linked account.</summary>
/// <param name="Provider">The provider's id ("steam", "discord").</param>
/// <param name="Subject">The provider's stable user id (SteamID64, Discord user id).</param>
/// <param name="Groups">Discord: the user's role ids in the configured guild (for identity.json roleMap).</param>
public sealed record ExternalIdentity(string Provider, string Subject, string? DisplayName = null, IReadOnlyList<string>? Groups = null);

/// <summary>A sign-in method besides passwords (DESIGN §3). Step 2's components implement it, register it in
/// <see cref="SignInProviders"/> (a shared service) and map their own routes (start, callback); on a verified identity
/// they call <see cref="AuthService.SignInExternal"/>, and the account page links one through
/// <see cref="AuthService.Link"/>.</summary>
public interface ISignInProvider
{
    /// <summary>[a-z][a-z0-9-]{0,31}: the key in <see cref="Account.Links"/> and the session's "via".</summary>
    string Id { get; }

    string Title { get; }
}

/// <summary>The registered sign-in providers ("local", the password, is always there and not listed here).</summary>
public sealed partial class SignInProviders
{
    private readonly ConcurrentDictionary<string, ISignInProvider> _all = new(StringComparer.Ordinal);

    public IReadOnlyList<ISignInProvider> All => [.. _all.Values.OrderBy(p => p.Id, StringComparer.Ordinal)];

    public void Add(ISignInProvider p)
    {
        if (!IsId(p.Id) || p.Id == "local") throw new ArgumentException($"bad provider id '{p.Id}'");
        if (!_all.TryAdd(p.Id, p)) throw new InvalidOperationException($"provider '{p.Id}' is registered already");
    }

    public bool Remove(string id) => _all.TryRemove(id, out _);
    public bool Has(string id) => _all.ContainsKey(id);

    public static bool IsId(string? s) => s is not null && IdRe().IsMatch(s);

    /// <summary>What a provider's subject may be: 1 to 128 visible ASCII characters.</summary>
    public static bool IsSubject(string? s) => s is { Length: > 0 and <= 128 } && s.All(c => c is > ' ' and < (char)0x7F);

    [GeneratedRegex("^[a-z][a-z0-9-]{0,31}$")]
    private static partial Regex IdRe();
}
