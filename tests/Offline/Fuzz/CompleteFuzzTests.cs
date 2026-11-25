// SPDX-License-Identifier: MIT
using Kilnwarden.Fuzz;

namespace Kilnwarden.Offline.Fuzz;

/// <summary>The release parsers' fuzz targets (ClientHello, minisign, markdown, player state, relay peers), briefly (fixed seed); long runs: <c>dotnet run -c Release -- &lt;name&gt; 60</c>
/// in tests/Fuzz.</summary>
public class CompleteFuzzTests
{
    public static TheoryData<string> Names() => [.. CompleteTargets.All.Select(t => t.Name)];

    [Theory]
    [MemberData(nameof(Names))]
    public void Release_target_survives_a_short_run(string name)
    {
        var t = CompleteTargets.All.Single(x => x.Name == name);
        var r = FuzzDriver.Fuzz(t, TimeSpan.FromSeconds(3), maxRuns: 20_000, seed: 2468, save: false);
        Assert.True(r.Findings.Count == 0, string.Join("\n", r.Findings));
        Assert.True(r.Runs >= 300, $"{r.Runs} runs");
    }

    /// <summary>The two markdown inputs the long run saved (javascript: inside an https host name, a false finding of the
    /// target's old check): the target passes them now, and the page they render has no script URL.</summary>
    [Theory]
    [InlineData("WyMgUnVgbGVzCgpCZSAqKioqLgotIGEKLSBiCjEuIHgKW2xdKGh0dHBzOi8vZQotIGIKMS4geApbbF0oaHR0cHM6Ly9lCi0gYgoxLiB4CltsXShodHRwczovL2UKLSBiCjEuIHgKW2xdKGh0dHBzOi8vZQotIGIKMS4geApbbF0oaHR0cHM6AC9lCi0gYgoxLiB4CltsXShodHRwczovL2UKLSBiCjEuIHgKW2xdKGh0dHBzOi8vZQotIGIKMS4geApbbF0oaHR0cHM6Ly9lCi0gYgoxLiB4CltsXShodHRwczovL2UKLSBiCjEuIHgKW2xdKGh0dHBzOi8vZQoKMS4geApbbF0ocHM6Ly9lCgoxLiB4CltsXShwczovL2UKCjEuIHgKW2xdKHBzOi8vZQoKMS4geApbbF0ocHM6Ly9lCgoxLiB4CltsXShwczovL2UKCjEuIHgKW2xdKHBzOi8vZQoKMS4geApbbF0ocHM6Ly9lCgoxLiB4CltsXShwczovL2UKCjEuIHgKW2xdKHBzOi8vZQoKMS4geApbbF0ocHM6Ly9lCgoxLiB4CltsXShwczovL2UKCjEuIHgKW2xdKHBzOi8vZQoKMS4geApbbF0ocHM6Ly9lCgoxLiB4CltsXShwczovL2UKCjEuIHgKW2xdKHBzOi8vZQoKMS4geApbbF0ocHM6Ly9lCgoxLiB4CltsXShwczovL2UKCjEuIHgKW2xdKHBzOi8vZQoKMS4geApbbF0ocHM6Ly9lCgoxLiB4CltsXShwczovL2UKCjEuIHgKW2xdKHBzOi8vZQoKMS4geApbbF0ocHM6Ly9lCgoxLiB4CltsXShwczovL2UKCjEuIHgKW2xdKHBzOi8vZQoKMS4geApbbF0ocHM6Ly9lCgoxLiB4CltsXShwczovL2UKCjEuIHgKW2xdKHBzOi8vZQoKMS4geApbbF0ocHM6Ly9lCgoxLiB4CltsXShwczovL2UKCjEuIHgKW2xdKHBzOi8vZQoKMS4geApbbF0ocHM6Ly9lCgoxLiB4CltsXShwczovL2UKCjEuIHgKW2xdKGh0dHBzOi8vZQoKMS4geApbbF0oaHR0cHM6Ly9lCgoxLiB4CltsXShodHRwczovL2UKCjEuIHgKW2xdKGh0dHBzOi8vZQoKMS4geApbbF0oaHR0cHM6Ly9lCgoxLiB4CltsXShodHRwczovL2UKCjEuIHgKW2xdKGh0dHBzOi8vZQoKMS4geApbbF0oaHR0cHM6Ly9lCgoxLiB4CltsXShodHRwczovL2UKCjEuIHgKW2xdKGh0dHBzOi8vZQoKMS4geApbbF0oaHR0cHM6Ly9lCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgplCgoxLiB4CltsXShodHRwczovL2UKCjEuIHgvL2UKCjEuIHgvL2UKCjEuIHgvL2UKCjEuIHgvL2UKCjEuIHgvL2UKCjEuIHgvL2UKCjEuIHgvL2UKCjEuIHgvL2UKCjEuIHgvL2UKCjEuIHgvL2UKCjEuIHgvL2UKCjEuIHgvL2UKCjEuIHgvL2UKCjEuIHgvL2UKCjEuIHgvL2UKCjEuIHgvL2UKCjEuIHgKW2xdKGh0dHBzOi8vZQoKMS4geApbbF0qaHR0cAoxLiB4CnRzOi8vZQotIGIKMS4geAp0czovL2UKLSBiCjEuIHgKdHM6Ly9lCi0gYgoxLiB4CnRzOi8vXShlCi0gYgoxLiB4CnRzOi8vZQotIGIKMS4geAp0czovL2UKLSBiCjEuIHgKdHM6Ly9lCi0gYgoxLiB4CnRzOi8vZQotIGIKMS4geAp0czovL2UKLSBiCjEuIHgKdHM6Ly9lCi0gYgoxLiB4CnRzOi8vZQotIGIKMS4geAp0czovL2UKLSBiCjEuIHgKdHM6Ly9lCi0tIGIqKi4KLSBhCi0gYioqLgotIGEKLSBiKiouCi0gYQotIGIqKi4KLSBhCi0gYioqLgotIGEKLSBiKiouCi0gYQotIGIqKi4KLSBhCi0gYioqLgotIGEKLSBiKiouCi0gYQotIGIqKi4KLSBhCi0gYioqLgotIGEKLSBiKiouCi0gYQotIGIqKi4KLSBhCi0gYgoxLiB4CltsXShodHRwczovL2UueGphdmFzY3JpcHQ6KSBgY2AgX2lf")]
    [InlineData("IyBSdWxlcwoKQmUgKipraW5kKiouCi0gYQotIGIKMS4geApbbF0qa2luZCoqLgotIGEKLSBiCjEuIHgKW2xdKmtpbmQqKi4KLSBhCi0gYgoxLiB4CltsXSpraW5kKiouCi0gYQotIGIKMS4geApbbF0qa2luZCoqLi0gYgoxLiB4CltsXSpraW5kKiouCi0gYQotIGIKMS4geApbbF0qa2luZCoqLgotIGEKLSBiCjEuIHgKW2xdKmtpbmQqKi4KLSBhCi0gYgoxLiB4CltsXSpraW5kKiouCi0gYQotIGIKMS4geApbbF0qa2luZCoqLgotIGEKLSBiCjEuIHgKW2xdKmtpbmQqKi4KLSBhCi0gYgoxOyB4CltsXSpraW5kKiouCi0gYQotIGIKMS4geApbbF0qa2luZCoqLgotIGEKLSBiCjEuIHgKW2xdKmtpbmQqKi4KLSBhCi0gYgoxLiB4CltsXSpraW5kKiouCi0gYQotIGIKMS4geApbbF0qa2luZCoqLgotIGEKLSBiCjEuIHgKW2xdKmtpbmQqKi4KLSBhCi0gYgoxLiB4CltsXSpraW5kKiouCi0gYQotIGIKMS4geApbbF0qa2luZCoqLgotIGEKLSBiCjEuIHgKW2xdKmtpbmQqKi4KLSBhCi0gYgoxLiB4CltsXSpraW5kKiouCi0gYQotAGJKMS4geApbbF0oaHR0cHM6Ly9lLnhqYXZhc2NyaXB0OikgYGNgIF9pXw==")]
    public void The_long_run_markdown_findings_replay_clean(string input)
    {
        var bytes = Convert.FromBase64String(input);
        CompleteTargets.All.Single(x => x.Name == "markdown").Run(bytes);
        var html = Kilnwarden.PublicPage.PublicMarkdown.Render(System.Text.Encoding.UTF8.GetString(bytes));
        Assert.DoesNotContain("href=\"javascript", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }
}
