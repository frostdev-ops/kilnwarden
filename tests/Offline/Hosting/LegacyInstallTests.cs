// SPDX-License-Identifier: MIT
// After the rename from Kiln Web Admin (kiln-webadmin): the old data folder moves over once, and an old mod folder that
// is still switched on keeps the panel off.
using Kilnwarden.Hosting;
using Kilnwarden.Offline.Support;

namespace Kilnwarden.Offline.Hosting;

public sealed class LegacyInstallTests
{
    [Fact]
    public void The_old_data_folder_moves_to_the_new_name_with_its_files()
    {
        using var kiln = new TempDir();
        var old = Path.Combine(kiln.Path, "data", "kiln-webadmin");
        Directory.CreateDirectory(Path.Combine(old, "tls"));
        File.WriteAllText(Path.Combine(old, "accounts.json"), "{}");
        File.WriteAllText(Path.Combine(old, "tls", "key.pem"), "k");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(Path.Combine(old, "accounts.json"), UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var target = Path.Combine(kiln.Path, "data", "kilnwarden");
        Directory.CreateDirectory(target); // Kiln may make the (empty) data folder before OnLoad

        var (result, message) = LegacyInstall.MigrateDataFolder(target);

        Assert.Equal(LegacyInstall.Migration.Moved, result);
        Assert.Contains("moved kiln/data/kiln-webadmin to kiln/data/kilnwarden", message);
        Assert.False(Directory.Exists(old));
        Assert.Equal("{}", File.ReadAllText(Path.Combine(target, "accounts.json")));
        Assert.Equal("k", File.ReadAllText(Path.Combine(target, "tls", "key.pem")));
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(target, "accounts.json")));
        // the second start finds nothing to do
        Assert.Equal(LegacyInstall.Migration.Nothing, LegacyInstall.MigrateDataFolder(target).Result);
    }

    [Fact]
    public void Without_an_old_folder_nothing_happens_and_with_both_folders_both_are_kept()
    {
        using var kiln = new TempDir();
        var target = Path.Combine(kiln.Path, "data", "kilnwarden");
        Assert.Equal(LegacyInstall.Migration.Nothing, LegacyInstall.MigrateDataFolder(target).Result);
        Assert.False(Directory.Exists(target));

        var old = Path.Combine(kiln.Path, "data", "kiln-webadmin");
        Directory.CreateDirectory(old);
        File.WriteAllText(Path.Combine(old, "accounts.json"), "old");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "accounts.json"), "new");
        var (result, message) = LegacyInstall.MigrateDataFolder(target);
        Assert.Equal(LegacyInstall.Migration.BothKept, result);
        Assert.Contains("both hold files", message);
        Assert.Equal("old", File.ReadAllText(Path.Combine(old, "accounts.json")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(target, "accounts.json")));
    }

    [Fact]
    public void A_mod_still_running_under_the_old_id_migrates_nothing()
    {
        using var kiln = new TempDir();
        var old = Path.Combine(kiln.Path, "data", "kiln-webadmin");
        Directory.CreateDirectory(old);
        Assert.Equal(LegacyInstall.Migration.Nothing, LegacyInstall.MigrateDataFolder(old).Result);
        Assert.True(Directory.Exists(old));
    }

    [Fact]
    public void An_old_mod_folder_counts_only_while_it_is_installed_and_switched_on()
    {
        using var kiln = new TempDir();
        var self = Path.Combine(kiln.Path, "mods", "kilnwarden");
        var old = Path.Combine(kiln.Path, "mods", "kiln-webadmin");
        Directory.CreateDirectory(self);
        Assert.Null(LegacyInstall.ActiveOldModFolder(self));
        Directory.CreateDirectory(old);
        Assert.Null(LegacyInstall.ActiveOldModFolder(self)); // an empty leftover folder
        File.WriteAllText(Path.Combine(old, "kiln.mod.json"), "{}");
        Assert.Equal(old, LegacyInstall.ActiveOldModFolder(self));
        File.WriteAllText(Path.Combine(old, ".disabled"), "");
        Assert.Null(LegacyInstall.ActiveOldModFolder(self));
        Assert.Null(LegacyInstall.ActiveOldModFolder(old)); // the old mod itself
    }

    [Fact]
    public void An_old_copy_in_a_folder_with_another_name_counts_by_its_manifest_id()
    {
        // Kiln loads every kiln/mods/<folder> by the id in its kiln.mod.json, whatever the folder is called
        using var kiln = new TempDir();
        var self = Path.Combine(kiln.Path, "mods", "kilnwarden");
        Directory.CreateDirectory(self);
        File.WriteAllText(Path.Combine(self, "kiln.mod.json"), """{"id":"kilnwarden"}""");
        var other = Path.Combine(kiln.Path, "mods", "hello");
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "kiln.mod.json"), """{"id":"hello"}""");
        var old = Path.Combine(kiln.Path, "mods", "Kiln Web Admin 0.1.0");
        Directory.CreateDirectory(old);
        File.WriteAllText(Path.Combine(old, "kiln.mod.json"), "// a test build\n{\"id\": \"kiln-webadmin\", \"version\": \"0.1.0\",}");
        Assert.Equal(old, LegacyInstall.ActiveOldModFolder(self));
        File.WriteAllText(Path.Combine(old, ".disabled"), "");
        Assert.Null(LegacyInstall.ActiveOldModFolder(self));
    }

    [Fact]
    public void A_copy_Kiln_has_loaded_or_will_load_counts_wherever_it_lives()
    {
        // a profile can load a mod from outside kiln/mods: Kiln's own list names its folder
        using var kiln = new TempDir();
        var self = Path.Combine(kiln.Path, "mods", "kilnwarden");
        Directory.CreateDirectory(self);
        var elsewhere = Path.Combine(kiln.Path, "store", "kiln-webadmin", "0.1.0");
        Assert.Equal(elsewhere, LegacyInstall.ActiveOldModFolder(self, kilnHasIt: elsewhere));
        Assert.Null(LegacyInstall.ActiveOldModFolder(self, kilnHasIt: null));
    }

    [Fact]
    public void A_link_at_the_new_name_is_never_deleted_and_the_panel_stays_off()
    {
        // kiln/data/kilnwarden made as a link to an empty folder (another disk, say): the move would replace the link with
        // the old folder, so it is left for the owner, and the panel does not start fresh
        using var kiln = new TempDir();
        var old = Path.Combine(kiln.Path, "data", "kiln-webadmin");
        Directory.CreateDirectory(old);
        File.WriteAllText(Path.Combine(old, "relay-key.pem"), "k");
        var elsewhere = Path.Combine(kiln.Path, "other-disk", "kilnwarden");
        Directory.CreateDirectory(elsewhere);
        var target = Path.Combine(kiln.Path, "data", "kilnwarden");
        try
        {
            Directory.CreateSymbolicLink(target, elsewhere);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return; // Windows without the symbolic link privilege (no developer mode): nothing to check here
        }

        var (result, message) = LegacyInstall.MigrateDataFolder(target);

        Assert.Equal(LegacyInstall.Migration.Failed, result);
        Assert.Contains("link", message);
        Assert.NotNull(new DirectoryInfo(target).LinkTarget);
        Assert.True(Directory.Exists(elsewhere));
        Assert.Equal("k", File.ReadAllText(Path.Combine(old, "relay-key.pem")));
    }
}
