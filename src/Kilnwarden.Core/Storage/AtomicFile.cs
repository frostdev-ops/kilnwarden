// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;

namespace Kilnwarden.Storage;

/// <summary>Whole-file writes that never leave a torn file: write "&lt;name&gt;.&lt;random&gt;.tmp" next to the target,
/// flush it to disk, then move it over the target (MoveFileEx REPLACE_EXISTING under Windows/Wine, rename(2) on
/// Linux). Secret files (keys, accounts, sessions, webhook URLs) are for the server's own user only: mode 0600 on Linux;
/// on Windows and under Wine the file is created with a protected DACL that allows only the current user (and
/// SYSTEM on real Windows). Wine's server turns such a DACL into Unix mode 0660 on the real file (it gives the owner's
/// entry to the user and the group bits; others get nothing), where a plain FileStream (no security descriptor:
/// "everyone") is left at the umask's 0644 (found in the complete lab).</summary>
public static class AtomicFile
{
    public static void Write(string path, ReadOnlySpan<byte> data, bool secret = false)
    {
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        var tmp = Path.Combine(dir, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var fs = Create(tmp, secret))
            {
                fs.Write(data);
                fs.Flush(flushToDisk: true);
            }
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(tmp);
            }
            catch (Exception)
            {
                // the temp file stays; the target is untouched
            }
            throw;
        }
    }

    private static FileStream Create(string tmp, bool secret)
    {
        if (secret && OperatingSystem.IsWindows()) return OwnerOnly.Create(tmp);
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (secret) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(tmp, options);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static class OwnerOnly
    {
        public static FileStream Create(string path)
        {
            var me = System.Security.Principal.WindowsIdentity.GetCurrent().User
                     ?? throw new InvalidOperationException("no user SID for the server's process");
            var sec = new System.Security.AccessControl.FileSecurity();
            sec.SetOwner(me);
            sec.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            // read, write, delete and the file's own security, never execute (Wine maps the owner's entry to the user and
            // group bits: 0660; others get nothing)
            const System.Security.AccessControl.FileSystemRights rights = System.Security.AccessControl.FileSystemRights.Read |
                System.Security.AccessControl.FileSystemRights.Write | System.Security.AccessControl.FileSystemRights.Delete |
                System.Security.AccessControl.FileSystemRights.ChangePermissions | System.Security.AccessControl.FileSystemRights.TakeOwnership;
            sec.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(me, rights, System.Security.AccessControl.AccessControlType.Allow));
            // real Windows: services and backups run as SYSTEM; Wine maps only the owner's entry to the file's mode
            if (!IsWine())
                sec.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                    new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.LocalSystemSid, null),
                    System.Security.AccessControl.FileSystemRights.FullControl, System.Security.AccessControl.AccessControlType.Allow));
            return new FileInfo(path).Create(FileMode.CreateNew, System.Security.AccessControl.FileSystemRights.Write | System.Security.AccessControl.FileSystemRights.ReadData,
                FileShare.None, 4096, FileOptions.None, sec);
        }

        private static bool? _wine;

        private static bool IsWine() => _wine ??= NativeLibrary.TryLoad("ntdll.dll", out var h) && NativeLibrary.TryGetExport(h, "wine_get_version", out _);
    }
}
