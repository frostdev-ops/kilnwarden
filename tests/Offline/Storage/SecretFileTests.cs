// SPDX-License-Identifier: MIT
using Kilnwarden.Offline.Support;
using Kilnwarden.Storage;

namespace Kilnwarden.Offline.Storage;

/// <summary>Secret files are the server user's only (found in the complete lab: under Wine they were 0644, because a
/// plain FileStream creates a file without a security descriptor and Wine then follows the umask).</summary>
public class SecretFileTests
{
    [Fact]
    public void Secret_files_are_owner_only_and_old_ones_are_secured_at_start()
    {
        using var tmp = new TempDir();
        var data = new DataStore(tmp.Path);
        data.WriteBytes("relay-key.pem", "key"u8, secret: true);
        File.WriteAllText(Path.Combine(tmp.Path, "accounts.json"), "{}"); // as an older version (or a hand edit) left it
        Directory.CreateDirectory(Path.Combine(tmp.Path, "ext"));
        File.WriteAllText(Path.Combine(tmp.Path, "ext", "mod.json"), "{}");
        Assert.Equal(3, data.Resecure());
        foreach (var f in new[] { "relay-key.pem", "accounts.json", "ext/mod.json" })
        {
            var path = Path.Combine(tmp.Path, f);
            Assert.Equal(f == "relay-key.pem" ? "key" : "{}", File.ReadAllText(path));
            if (OperatingSystem.IsWindows())
            {
                var acl = new FileInfo(path).GetAccessControl();
                Assert.True(acl.AreAccessRulesProtected, f);
                var me = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
                foreach (System.Security.AccessControl.FileSystemAccessRule r in acl.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier)))
                    Assert.True(r.IdentityReference.Equals(me) || ((System.Security.Principal.SecurityIdentifier)r.IdentityReference).IsWellKnown(System.Security.Principal.WellKnownSidType.LocalSystemSid), f);
            }
            else Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        }
    }
}
