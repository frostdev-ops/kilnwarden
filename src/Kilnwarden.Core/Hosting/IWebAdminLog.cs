// SPDX-License-Identifier: MIT
namespace Kilnwarden.Hosting;

/// <summary>Where the mod logs: kiln.log in the server (Kiln's Logger), the console in tools, a list in tests.
/// Callable from any thread.</summary>
public interface IWebAdminLog
{
    void Debug(string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? error = null);
}
