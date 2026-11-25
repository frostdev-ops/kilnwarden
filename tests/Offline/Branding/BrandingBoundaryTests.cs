using System.Text;
using Kilnwarden.Branding;
using Kilnwarden.Offline.Support;
namespace Kilnwarden.Offline.Branding;
public sealed class BrandingBoundaryTests
{
    [Fact]
    public void Svg_is_never_a_branding_asset_even_if_it_looks_passive() =>
        Assert.False(BrandingFiles.Matches("svg", Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>")));

    [Fact]
    public void Only_fixed_branding_names_are_allowed()
    {
        Assert.False(BrandingRules.IsFileName("crest.webp"));
        Assert.False(BrandingRules.IsFileName("logo.svg"));
        Assert.True(BrandingRules.IsFileName("logo.png"));
        Assert.True(BrandingRules.IsFileName("favicon.ico"));
    }

    [Fact]
    public void A_linked_branding_directory_is_not_read()
    {
        if (OperatingSystem.IsWindows()) return;
        using var dir = new TempDir();
        var real = Path.Combine(dir.Path, "real"); Directory.CreateDirectory(real);
        File.WriteAllBytes(Path.Combine(real, "logo.png"), BrandingTests.Png);
        var linked = Path.Combine(dir.Path, "branding"); Directory.CreateSymbolicLink(linked, real);
        Assert.Null(new BrandingFiles(linked, new TestLog()).Find(null, BrandingFiles.LogoNames));
    }

    [Fact]
    public void Cached_metadata_cannot_hide_a_replacement_symlink()
    {
        if (OperatingSystem.IsWindows()) return;
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "logo.png"); var target = Path.Combine(dir.Path, "elsewhere.png");
        File.WriteAllBytes(path, BrandingTests.Png); File.WriteAllBytes(target, BrandingTests.Png);
        File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(path));
        var files = new BrandingFiles(dir.Path, new TestLog()); Assert.NotNull(files.Find(null, BrandingFiles.LogoNames));
        File.Delete(path); File.CreateSymbolicLink(path, target);
        Assert.Null(files.Find(null, BrandingFiles.LogoNames));
    }
}
