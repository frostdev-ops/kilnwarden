// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using Kilnwarden.Hosting;

namespace Kilnwarden.Offline.Support;

public sealed class TestLog : IWebAdminLog
{
    public readonly ConcurrentQueue<string> Lines = new();
    public void Debug(string message) => Lines.Enqueue("D " + message);
    public void Info(string message) => Lines.Enqueue("I " + message);
    public void Warn(string message) => Lines.Enqueue("W " + message);
    public void Error(string message, Exception? error = null) => Lines.Enqueue("E " + message + (error is null ? "" : " :: " + error));
    public bool Has(string part) => Lines.Any(l => l.Contains(part, StringComparison.Ordinal));
    public override string ToString() => string.Join("\n", Lines);
}

public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kwa-test-" + Guid.NewGuid().ToString("N")[..12]);
    public TempDir() => Directory.CreateDirectory(Path);
    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
            // Windows: a loaded native DLL (kwtls-<version>.dll stays loaded for the process) cannot be deleted
        }
    }
}

public sealed class TestPrincipal(string id, params string[] perms) : Security.IPrincipal
{
    public string Id => id;
    public string Name => id;
    public bool Has(string permission) => perms.Contains(permission) || perms.Contains("*");
}
