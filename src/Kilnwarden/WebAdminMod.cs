// SPDX-License-Identifier: MIT
// The Kiln side: OnLoad builds the environment and starts the host on a pool thread (nothing slow on the tick thread);
// Tick.Pre drains the game-thread queue; OnUnload stops the host in the step 0 order and releases every socket, thread,
// timer and file (KILN106).
using System.Diagnostics;
using Kilnwarden.Hosting;

namespace Kilnwarden;

public sealed class WebAdminMod : Mod
{
    private WebAdminHost? _host;
    private GameThreadQueue? _game;
    private LegacyNotice? _notice;
#if LAB
    private Lab.LabProbe? _lab;
    private PacingHandler? _pacing;
#endif

    /// <summary>The components after core, in start order: the shared first-party list (Hosting/FirstPartyComponents).</summary>
    internal static IEnumerable<IWebAdminComponent> Components() => FirstPartyComponents.All();

    public override void OnLoad()
    {
        var sw = Stopwatch.StartNew();
        // after the rename from Kiln Web Admin (kiln-webadmin): never two copies, and the old data folder moves over once.
        // Kiln's mod list knows a copy loaded from any folder (or the profile), loaded before this one or waiting for its turn.
        var kilnHasIt = Context.Info.Id != LegacyInstall.OldModId &&
                        Mods.Get(LegacyInstall.OldModId) is { State: ModState.Pending or ModState.Loaded or ModState.Disabled } oldEntry
            ? oldEntry.Directory : null;
        if (LegacyInstall.ActiveOldModFolder(Context.Info.Directory, kilnHasIt) is { } oldFolder)
        {
            var where = string.Equals(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(oldFolder))),
                Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Context.Info.Directory))), StringComparison.OrdinalIgnoreCase)
                ? $"kiln/mods/{Path.GetFileName(Path.TrimEndingDirectorySeparator(oldFolder))}" : oldFolder;
            // 0.1.2: say exactly what to delete in kiln.log, in the console's answers and on the panel's own address
            ConsoleCommands.Off = Setup.SetupTexts.OldCopy(where);
            Log.Error($"{Product.Name}: {ConsoleCommands.Off}");
            _notice = LegacyNotice.TryStart(Context.DataDirectory,
                WebAdminEnvironment.QueryPortOf(Path.GetFullPath(Path.Combine(Context.Info.Directory, "..", "..", ".."))), ConsoleCommands.Off,
                m => Log.Info($"{Product.Name}: {m}"));
            return;
        }
        var (migration, moved) = LegacyInstall.MigrateDataFolder(Context.DataDirectory);
        switch (migration)
        {
            case LegacyInstall.Migration.Moved: Log.Info($"{Product.Name}: {moved}"); break;
            case LegacyInstall.Migration.BothKept: Log.Warn($"{Product.Name}: {moved}"); break;
            case LegacyInstall.Migration.Failed: Log.Error($"{Product.Name}: {moved}"); return;
        }
        _game = new GameThreadQueue();
        Context.Tick.Pre += OnPre;
        var env = new WebAdminEnvironment
        {
            DataDirectory = Context.DataDirectory,
            ModDirectory = Context.Info.Directory,
            ServerDirectory = Path.GetFullPath(Path.Combine(Context.Info.Directory, "..", "..", "..")),
            ModVersion = Context.Info.Version,
            KilnVersion = Game.KilnVersion,
            Log = new KilnLog(Log),
            GameThread = _game,
        };
        IEnumerable<IWebAdminComponent> components = Components();
#if LAB
        _lab = new Lab.LabProbe(Context.DataDirectory);
        components = components.Append(_lab);
        Context.Tick.Pre += OnLabPre;
        Context.Tick.Post += OnLabPost;
        if (_lab.OnlinePacing)
            _pacing = Pacing.OnServerWait(static (ref long ns) =>
            {
                if (ns > Pacing.ServerOnlineWaitNs) ns = Pacing.ServerOnlineWaitNs;
            });
#endif
        _host = new WebAdminHost(env, components);
        _host.Start();
        Log.Info($"{Product.Name} {Context.Info.Version}: starting in the background (OnLoad {sw.Elapsed.TotalMilliseconds:F1} ms)");
    }

    private void OnPre(in TickInfo tick) => _game?.Drain();

    // Each command also answers to its old first word, webadmin (Kiln Web Admin's name; the claim page and older notes
    // say "webadmin claim").
    // The server console only (the console folder kiln/control/console/<name>.cmd, which works over FTP, and kilnctl
    // cmd): on a rented game server without a browser on the machine, this is how the owner gets the first setup link.
    // 0.1.2: "kilnwarden setup hosted <name>" sets up hosted access from the console alone (a rented server).
    [Command("kilnwarden setup", Description = "what to do next to set up the panel; \"kilnwarden setup hosted <name>\" for a server without a browser",
        Permission = CommandPermission.Console)]
    [Command("webadmin setup", Description = "the old name of kilnwarden setup",
        Permission = CommandPermission.Console)]
    private string SetupCommand(string what = "", string name = "") => ConsoleCommands.Setup(_host, what, name);

    [Command("kilnwarden status", Description = "a checklist of the panel's setup: what is done, what is left, what to do next",
        Permission = CommandPermission.Console)]
    [Command("webadmin status", Description = "the old name of kilnwarden status",
        Permission = CommandPermission.Console)]
    private string StatusCommand() => ConsoleCommands.Status(_host);

    // The console only, like setup: a rented game server's owner claims the relay name (relay.json host) with the link
    // in the answer file, signed in with Discord on frostdev.io (docs/hosting-routes.md).
    [Command("kilnwarden claim", Description = "a one-time link to claim this server's relay name (relay.json host) with your Discord account",
        Permission = CommandPermission.Console)]
    [Command("webadmin claim", Description = "the old name of kilnwarden claim",
        Permission = CommandPermission.Console)]
    private string ClaimCommand() => ConsoleCommands.Claim(_host);
#if LAB
    private void OnLabPre(in TickInfo tick) => _lab?.Pre(tick.Step, tick.WallDeltaMs);
    private void OnLabPost(in TickInfo tick) => _lab?.Post();
#endif

    public override void OnUnload()
    {
        var sw = Stopwatch.StartNew();
        Context.Tick.Pre -= OnPre;
#if LAB
        Context.Tick.Pre -= OnLabPre;
        Context.Tick.Post -= OnLabPost;
        _pacing?.Dispose();
        _pacing = null;
#endif
        var summary = _host?.Stop() ?? "never started";
        _host = null;
        _notice?.Dispose();
        _notice = null;
        ConsoleCommands.Off = null;
        _game?.Dispose();
        _game = null;
#if LAB
        _lab?.Dispose();
        _lab = null;
#endif
        Log.Info($"{Product.Name}: {summary}; OnUnload {sw.Elapsed.TotalMilliseconds:F1} ms");
    }

    private sealed class KilnLog(Logger log) : IWebAdminLog
    {
        public void Debug(string message) => log.Debug(message);
        public void Info(string message) => log.Info(message);
        public void Warn(string message) => log.Warn(message);

        public void Error(string message, Exception? error = null)
        {
            if (error is null) log.Error(message);
            else log.Error(error, message);
        }
    }
}
