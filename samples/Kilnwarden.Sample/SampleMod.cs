// SPDX-License-Identifier: MIT
// The Kiln side of the sample: OnLoad waits for the panel, OnUnload lets go. Everything that names a Kilnwarden.Api
// type lives in SamplePanel, which only loads when the panel is there (an optional dependency).
namespace Kilnwarden.Sample;

public sealed class SampleMod : Mod
{
    private IDisposable? _panel;

    public override void OnLoad()
    {
        if (!Mods.IsLoaded("kilnwarden")) return;
        _panel = SamplePanel.Start(Context.Info.Id, Log.Info);
    }

    public override void OnUnload()
    {
        _panel?.Dispose();
        _panel = null;
    }
}
