// SPDX-License-Identifier: MIT
using Kilnwarden.Fuzz;

namespace Kilnwarden.Offline.Fuzz;

/// <summary>The branding fuzz targets, briefly (fixed seed); long runs: <c>dotnet run -c Release -- &lt;name&gt; 60</c>
/// in tests/Fuzz.</summary>
public class BrandingFuzzTests
{
    public static TheoryData<string> Names() => [.. BrandingTargets.All.Select(t => t.Name)];

    [Theory]
    [MemberData(nameof(Names))]
    public void Branding_target_survives_a_short_run(string name)
    {
        var t = BrandingTargets.All.Single(x => x.Name == name);
        var r = FuzzDriver.Fuzz(t, TimeSpan.FromSeconds(3), maxRuns: 20_000, seed: 2468, save: false);
        Assert.True(r.Findings.Count == 0, string.Join("\n", r.Findings));
        Assert.True(r.Runs >= 300, $"{r.Runs} runs");
    }
}
