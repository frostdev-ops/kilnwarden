// SPDX-License-Identifier: MIT
namespace Kilnwarden.Offline;

public class LayoutTests
{
    [Fact]
    public void Product_names_come_from_Directory_Build_props()
    {
        Assert.Equal("kilnwarden", Product.ModId);
        Assert.Equal("Kilnwarden", Product.Name);
        Assert.Matches(@"^\d+\.\d+\.\d+", Product.KwtlsVersion);
    }
}
