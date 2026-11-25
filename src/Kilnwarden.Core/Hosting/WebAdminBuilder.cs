// SPDX-License-Identifier: MIT
using Kilnwarden.Config;
using Kilnwarden.Modules;
using Kilnwarden.Routing;
using Kilnwarden.Security;
using Kilnwarden.Storage;

namespace Kilnwarden.Hosting;

/// <summary>What a component's Configure gets: the shared parts to register into, and the seams to replace.</summary>
public sealed class WebAdminBuilder
{
    private readonly List<(PipelineStage Stage, int Seq, IHttpMiddleware Middleware)> _middleware = [];

    internal WebAdminBuilder(WebAdminEnvironment env, DataStore data, ModuleRegistry modules, SettingsStore<ServerSettings> settings,
        WebAdminServices services, IServerInfo server)
    {
        Environment = env;
        Data = data;
        Modules = modules;
        Settings = settings;
        Services = services;
        Server = server;
    }

    public WebAdminEnvironment Environment { get; }
    public IWebAdminLog Log => Environment.Log;

    /// <summary>The data folder (kiln/data/kilnwarden). Components keep their files under it.</summary>
    public DataStore Data { get; }

    public ModuleRegistry Modules { get; }

    /// <summary>The server's own settings.json (bind, TLS, limits).</summary>
    public SettingsStore<ServerSettings> Settings { get; }

    public WebAdminServices Services { get; }

    /// <summary>Where the panel listens (filled once the listener is up).</summary>
    public IServerInfo Server { get; }

    /// <summary>The route gate (default: <see cref="DefaultAccessGate"/>). The auth component sets its own.</summary>
    public IAccessGate AccessGate { get; set; } = new DefaultAccessGate();

    /// <summary>Whether the owner account exists (default: <see cref="NoOwnerYet"/>, so loopback only).</summary>
    public ISetupState SetupState { get; set; } = new NoOwnerYet();

    /// <summary>Where write operations are recorded (default: nowhere). Also published as a service.</summary>
    public IAuditSink Audit { get; set; } = new NullAuditSink();

    /// <summary>Adds a middleware at <paramref name="stage"/>; within a stage, in the order added.</summary>
    public void Use(PipelineStage stage, IHttpMiddleware middleware) => _middleware.Add((stage, _middleware.Count, middleware));

    /// <summary>A settings file of the component's own, in the data folder, hot-reloaded (polled every 2 s); the host
    /// disposes it at stop.</summary>
    public SettingsStore<T> SettingsFile<T>(string file, Func<T, IReadOnlyList<string>>? validate = null) where T : class, new()
    {
        var s = new SettingsStore<T>(Data, file, Log, validate);
        s.Watch(Environment.Time);
        OwnedSettings.Add(s);
        return s;
    }

    internal List<IDisposable> OwnedSettings { get; } = [];

    internal IReadOnlyList<IHttpMiddleware> Middleware() =>
        [.. _middleware.OrderBy(m => m.Stage).ThenBy(m => m.Seq).Select(m => m.Middleware)];
}
