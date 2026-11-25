// SPDX-License-Identifier: MIT
using Kilnwarden.Fuzz;

namespace Kilnwarden.Offline.Fuzz;

/// <summary>Every fuzz target briefly (fixed seed, so a failure reproduces); the long runs are
/// <c>dotnet run -c Release</c> in tests/Fuzz.</summary>
public class FuzzSmokeTests
{
    public static TheoryData<string> Names() => [.. Targets.All.Concat(GlueTargets.All()).Select(t => t.Name)];

    [Theory]
    [MemberData(nameof(Names))]
    public void Target_survives_a_short_run(string name)
    {
        var t = Targets.All.Concat(GlueTargets.All()).Single(x => x.Name == name);
        var r = FuzzDriver.Fuzz(t, TimeSpan.FromSeconds(3), maxRuns: 20_000, seed: 1234, save: false);
        Assert.True(r.Findings.Count == 0, string.Join("\n", r.Findings));
        Assert.True(r.Runs >= 1000 || r.Runs == 20_000 || name == "kwtls-glue", $"{r.Runs} runs");
    }

    [Fact]
    public void Every_parser_has_a_target()
    {
        var names = Targets.All.Select(t => t.Name).ToHashSet();
        foreach (var n in new[] { "http-head", "ws-upgrade", "cookies", "query", "path", "json-body", "settings-file", "pem", "accept-encoding", "ip-range", "host", "forwarded" })
            Assert.Contains(n, names);
    }
}
