// The Kiln side: wait for the web panel in OnLoad, let go in OnUnload. Everything that names a Kilnwarden.Api type is
// in Panel.cs, which only runs when the panel is there (an optional dependency).
using Kiln;

namespace MyPanelMod;

public sealed class MyPanelMod : Mod
{
    private IDisposable? _panel;

    public override void OnLoad()
    {
        if (!Mods.IsLoaded("kilnwarden")) return;
        _panel = Panel.Start(Context.Info.Id, Log.Info);
    }

    public override void OnUnload()
    {
        _panel?.Dispose();
        _panel = null;
    }
}
