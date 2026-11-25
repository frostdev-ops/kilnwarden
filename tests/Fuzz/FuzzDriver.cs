// SPDX-License-Identifier: MIT
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Kilnwarden.Fuzz;

/// <summary>A small mutation fuzzer (no SharpFuzz/AFL needed): seeds, byte flips, inserts, deletes, dictionary tokens
/// and splices, deterministic per seed. A finding (an exception outside the target's allowed set) is saved under
/// findings/&lt;target&gt;/ and counted; an input slower than 1 s is reported as a hang.</summary>
public static class FuzzDriver
{
    public static int Run(string[] args)
    {
        var name = args.Length > 0 ? args[0] : "all";
        var seconds = args.Length > 1 ? double.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 10;
        var seed = args.Length > 2 ? int.Parse(args[2]) : Environment.TickCount;
        var targets = name == "all" ? Targets.All.Concat(GlueTargets.All()).Concat(AuthTargets.All).Concat(BrandingTargets.All).Concat(CompleteTargets.All).ToArray() : [.. Targets.All.Concat(GlueTargets.All()).Concat(AuthTargets.All).Concat(BrandingTargets.All).Concat(CompleteTargets.All).Where(t => t.Name == name)];
        if (targets.Length == 0)
        {
            Console.Error.WriteLine($"no target '{name}'; targets: {string.Join(", ", Targets.All.Concat(GlueTargets.All()).Concat(AuthTargets.All).Concat(BrandingTargets.All).Concat(CompleteTargets.All).Select(t => t.Name))}");
            return 2;
        }
        var save = !args.Contains("--no-save", StringComparer.Ordinal);
        var findings = 0;
        foreach (var t in targets)
        {
            var r = Fuzz(t, TimeSpan.FromSeconds(seconds / targets.Length), int.MaxValue, seed, save: save);
            Console.WriteLine($"{t.Name,-16} {r.Runs,9} runs  {r.Findings.Count} findings  slowest {r.SlowestMs:F1} ms");
            foreach (var f in r.Findings.Take(5)) Console.WriteLine("    " + f);
            findings += r.Findings.Count;
        }
        return findings == 0 ? 0 : 1;
    }

    public sealed record Result(long Runs, List<string> Findings, double SlowestMs);

    public static Result Fuzz(Target t, TimeSpan budget, int maxRuns, int seed, bool save)
    {
        var rng = new Random(seed);
        var corpus = t.Seeds.Select(s => Encoding.Latin1.GetBytes(s)).ToList();
        var dict = t.Dictionary.Select(s => Encoding.Latin1.GetBytes(s)).ToArray();
        var findings = new List<string>();
        var seen = new HashSet<string>();
        var sw = Stopwatch.StartNew();
        long runs = 0;
        var slowest = 0.0;
        foreach (var s in corpus.ToList()) Exec(s);
        while (sw.Elapsed < budget && runs < maxRuns)
        {
            var input = Mutate(rng, corpus[rng.Next(corpus.Count)], corpus, dict);
            if (Exec(input) && corpus.Count < 256 && rng.Next(8) == 0) corpus.Add(input);
        }
        return new Result(runs, findings, slowest);

        bool Exec(byte[] input)
        {
            runs++;
            var t0 = Stopwatch.GetTimestamp();
            try
            {
                t.Run(input);
                return true;
            }
            catch (Exception e) when (t.Allowed.Any(a => a.IsInstanceOfType(e)))
            {
                return false;
            }
            catch (Exception e)
            {
                var key = e.GetType().Name + ":" + (e.StackTrace?.Split('\n').FirstOrDefault()?.Trim() ?? "");
                if (seen.Add(key))
                {
                    var file = save ? SaveFinding(t.Name, input) : "";
                    findings.Add($"{e.GetType().Name}: {e.Message} {file}\n      {e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
                }
                return false;
            }
            finally
            {
                var ms = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
                if (ms > slowest) slowest = ms;
                if (ms > 1000 && seen.Add("hang:" + input.Length))
                    findings.Add($"hang: {ms:F0} ms on a {input.Length}-byte input {(save ? SaveFinding(t.Name, input) : "")}");
            }
        }
    }

    private static byte[] Mutate(Random rng, byte[] src, List<byte[]> corpus, byte[][] dict)
    {
        var b = new List<byte>(src);
        var n = 1 + rng.Next(6);
        for (var i = 0; i < n; i++)
        {
            switch (rng.Next(7))
            {
                case 0 when b.Count > 0:
                    b[rng.Next(b.Count)] ^= (byte)(1 << rng.Next(8));
                    break;
                case 1:
                    b.Insert(rng.Next(b.Count + 1), (byte)rng.Next(256));
                    break;
                case 2 when b.Count > 0:
                {
                    var at = rng.Next(b.Count);
                    b.RemoveRange(at, Math.Min(b.Count - at, 1 + rng.Next(8)));
                    break;
                }
                case 3 when dict.Length > 0:
                    b.InsertRange(rng.Next(b.Count + 1), dict[rng.Next(dict.Length)]);
                    break;
                case 4 when b.Count > 0:
                {
                    var other = corpus[rng.Next(corpus.Count)];
                    if (other.Length == 0) break;
                    var at = rng.Next(b.Count);
                    var from = rng.Next(other.Length);
                    b.RemoveRange(at, b.Count - at);
                    b.AddRange(other.Skip(from));
                    break;
                }
                case 5 when b.Count > 0:
                {
                    var at = rng.Next(b.Count);
                    var len = Math.Min(b.Count - at, 1 + rng.Next(32));
                    var chunk = b.GetRange(at, len);
                    for (var k = rng.Next(1, 64); k > 0; k--) b.InsertRange(at, chunk);   // repetition (caps, quadratic paths)
                    break;
                }
                default:
                    if (b.Count > 0) b[rng.Next(b.Count)] = Specials[rng.Next(Specials.Length)];
                    break;
            }
        }
        if (b.Count > 64 * 1024) b.RemoveRange(64 * 1024, b.Count - 64 * 1024);
        return [.. b];
    }

    private static readonly byte[] Specials = [(byte)'\r', (byte)'\n', (byte)':', (byte)';', (byte)',', (byte)'=', (byte)'%', (byte)'/', (byte)' ', (byte)'\t', 0, 0x7F, 0xFF, (byte)'"'];

    private static string SaveFinding(string target, byte[] input)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "findings", target);
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, Convert.ToHexString(SHA256.HashData(input))[..16].ToLowerInvariant() + ".bin");
            File.WriteAllBytes(file, input);
            return file;
        }
        catch (IOException)
        {
            return "";
        }
    }
}
