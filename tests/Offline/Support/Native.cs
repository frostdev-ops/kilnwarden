// SPDX-License-Identifier: MIT
using Kilnwarden.Tls;

namespace Kilnwarden.Offline.Support;

/// <summary>The host build of kwtls (native/kwtls/build.sh host -> dist/libkwtls.so) for the TLS tests.</summary>
public static class Native
{
    public static string RepoRoot
    {
        get
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d is not null && !File.Exists(Path.Combine(d.FullName, "Directory.Build.props"))) d = d.Parent;
            return d?.FullName ?? throw new DirectoryNotFoundException("repo root");
        }
    }

    public static string KwtlsPath => Path.Combine(RepoRoot, "native", "kwtls", "dist", KwtlsLibrary.FileName(Product.KwtlsVersion));

    /// <summary>The library, or a skipped test when it is not built.</summary>
    public static KwtlsLibrary Kwtls()
    {
        if (!File.Exists(KwtlsPath)) Assert.Skip($"kwtls is not built ({KwtlsPath}): native/kwtls/build.sh host");
        return KwtlsLibrary.Load(KwtlsPath);
    }
}
