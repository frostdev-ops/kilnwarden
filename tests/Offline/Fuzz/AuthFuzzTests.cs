// SPDX-License-Identifier: MIT
using Kilnwarden.Fuzz;

namespace Kilnwarden.Offline.Fuzz;

/// <summary>The auth lane's fuzz targets, briefly (fixed seed); long runs: <c>dotnet run -c Release -- &lt;name&gt; 60</c>
/// in tests/Fuzz.</summary>
public class AuthFuzzTests
{
    public static TheoryData<string> Names() => [.. AuthTargets.All.Select(t => t.Name)];

    [Theory]
    [MemberData(nameof(Names))]
    public void Auth_target_survives_a_short_run(string name)
    {
        var t = AuthTargets.All.Single(x => x.Name == name);
        var r = FuzzDriver.Fuzz(t, TimeSpan.FromSeconds(3), maxRuns: 20_000, seed: 4321, save: false);
        Assert.True(r.Findings.Count == 0, string.Join("\n", r.Findings));
        Assert.True(r.Runs >= 300, $"{r.Runs} runs");
    }

    [Fact]
    public void Every_auth_parser_has_a_target()
    {
        var names = AuthTargets.All.Select(t => t.Name).ToHashSet();
        foreach (var n in new[] { "kap-frame", "kap-endpoint", "kap-token", "session-cookie", "auth-json", "accounts-file", "roles-file", "sessions-file", "page-frame-gate" })
            Assert.Contains(n, names);
    }
}
