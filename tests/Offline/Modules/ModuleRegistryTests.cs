// SPDX-License-Identifier: MIT
using System.Text;
using System.Text.Json.Nodes;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Modules;
using Kilnwarden.Offline.Support;
using Kilnwarden.Routing;
using Kilnwarden.Security;
using Kilnwarden.Storage;

namespace Kilnwarden.Offline.Modules;

public class ModuleRegistryTests
{
    private static ModuleSpec Spec(string id, bool canDisable = true, params string[] perms) => new()
    {
        Id = id, Title = id, CanDisable = canDisable,
        Permissions = [.. perms.Select(p => new PermissionSpec(p, p))],
        Pages = [new PageSpec("main", id, PageKind.Builtin, id)],
    };

    private static readonly RouteHandler Ok = _ => ValueTask.FromResult(HttpResponse.Empty());

    [Fact]
    public void Routes_mount_under_api_module()
    {
        var r = new ModuleRegistry();
        var h = r.Register(Spec("players", true, "players.kick"), "kilnwarden");
        h.MapGet("/", RouteAccess.SignedIn, Ok).MapPost("/{id}/kick", RouteAccess.Requires("players.kick"), Ok);
        Assert.Equal(["/api/players", "/api/players/{id}/kick"], r.Routes.Routes.Select(x => x.Pattern).Order());
    }

    [Fact]
    public void Ids_permissions_and_routes_are_checked()
    {
        var r = new ModuleRegistry();
        Assert.Throws<ArgumentException>(() => r.Register(Spec("Bad Id"), "o"));
        Assert.Throws<ArgumentException>(() => r.Register(Spec("a", true, "b.x"), "o"));            // not its prefix
        Assert.Throws<ArgumentException>(() => r.Register(Spec("a", true, "a.x", "a.x"), "o"));     // twice
        var h = r.Register(Spec("a", true, "a.x"), "o");
        Assert.Throws<InvalidOperationException>(() => r.Register(Spec("a"), "o"));                // id taken
        Assert.Throws<ArgumentException>(() => h.MapGet("/y", RouteAccess.Requires("a.undeclared"), Ok));
        h.MapGet("/{id}", RouteAccess.SignedIn, Ok);
        Assert.Throws<InvalidOperationException>(() => h.MapGet("/{other}", RouteAccess.SignedIn, Ok)); // same shape
        h.MapPost("/{id}", RouteAccess.SignedIn, Ok);                                               // other method is fine
        Assert.Throws<ArgumentException>(() => h.Map("TRACE", "/t", RouteAccess.Public, Ok));
    }

    [Fact]
    public void Unregister_by_owner_removes_routes_and_permissions()
    {
        var r = new ModuleRegistry();
        var changes = 0;
        r.Changed += () => changes++;
        r.Register(Spec("ext-a", true, "ext-a.x"), "some-mod").MapGet("/", RouteAccess.SignedIn, Ok);
        r.Register(Spec("ext-b"), "some-mod");
        r.Register(Spec("core2"), "kilnwarden");
        Assert.Equal(2, r.UnregisterOwner("some-mod"));
        Assert.Single(r.Modules);
        Assert.Equal(0, r.Routes.Count);
        Assert.Null(r.FindPermission("ext-a.x"));
        Assert.True(changes >= 4);
    }

    [Fact]
    public void Switches_persist_and_core_modules_stay_on()
    {
        using var dir = new TempDir();
        var data = new DataStore(dir.Path);
        var r = new ModuleRegistry(data);
        r.Register(Spec("sched"), "o");
        r.Register(Spec("users", canDisable: false), "o");
        r.SetEnabled("sched", false);
        Assert.False(r.IsEnabled("sched"));
        Assert.Throws<InvalidOperationException>(() => r.SetEnabled("users", false));
        Assert.Throws<KeyNotFoundException>(() => r.SetEnabled("nope", false));

        var again = new ModuleRegistry(data);
        again.Register(Spec("sched"), "o");
        Assert.False(again.IsEnabled("sched"));
        again.SetEnabled("sched", true);
        Assert.True(new ModuleRegistry(data).Also(x => x.Register(Spec("sched"), "o")).IsEnabled("sched"));
    }

    [Fact]
    public void Describe_shows_only_what_the_user_may_see()
    {
        var r = new ModuleRegistry();
        r.Register(new ModuleSpec
        {
            Id = "players", Title = "Players",
            Permissions = [new PermissionSpec("players.view", "v"), new PermissionSpec("players.kick", "k")],
            Pages = [new PageSpec("list", "Players", PageKind.Builtin, "players") { Permission = "players.view" }],
        }, "o");
        Assert.Empty(r.Describe(null));
        Assert.Empty(r.Describe(new TestPrincipal("u")));
        var seen = r.Describe(new TestPrincipal("u", "players.view"));
        var m = Assert.Single(seen)!.AsObject();
        Assert.Equal("players", (string?)m["id"]);
        Assert.Equal(["players.view"], m["permissions"]!.AsArray().Select(x => (string?)x));
    }

    [Fact]
    public async Task Actions_are_gated_routes_that_audit()
    {
        var r = new ModuleRegistry();
        var h = r.Register(Spec("mods", true, "mods.reload"), "o");
        h.AddAction(new ActionSpec("reload", "mods.reload", call => ValueTask.FromResult<JsonNode?>(new JsonObject { ["got"] = call.Args["id"]!.DeepClone() }))
        {
            Redact = ["secret"],
        });
        var route = Assert.Single(r.Routes.Routes);
        Assert.Equal("/api/mods/actions/reload", route.Pattern);
        Assert.Equal(RouteAccess.Requires("mods.reload"), route.Access);

        var audit = new ListAudit();
        var services = new WebAdminServices();
        services.Add<IAuditSink>(audit);
        var ctx = Ctx(services, "{\"id\":\"x\",\"secret\":\"pw\"}");
        ctx.User = new TestPrincipal("ada", "mods.reload");
        ctx.Route = new RouteMatch(route, new Dictionary<string, string>());
        var res = await route.Handler!(ctx);
        Assert.Equal(200, res.Status);
        Assert.Contains("\"got\":\"x\"", Encoding.UTF8.GetString(res.Body.Span));
        var e = Assert.Single(audit.Entries);
        Assert.Equal("mods.reload", e.Op);
        Assert.Equal("***", (string?)e.Args!["secret"]);
        Assert.True(e.Ok);
    }

    internal static RequestContext Ctx(WebAdminServices services, string? json = null) => new()
    {
        Request = new HttpRequest
        {
            Method = "POST", Target = "/", Path = "/",
            Headers = json is null ? new() : new HttpHeaders().Also(h => h.Add("Content-Type", "application/json")),
            Body = json is null ? default : Encoding.UTF8.GetBytes(json),
        },
        Connection = new ConnectionInfo { Id = 1, PeerAddress = System.Net.IPAddress.Loopback, LocalEndPoint = new(System.Net.IPAddress.Loopback, 1) },
        Services = services,
        ClientAddress = System.Net.IPAddress.Loopback,
        Scheme = "https",
        Host = "localhost:15737",
    };

    private sealed class ListAudit : IAuditSink
    {
        public readonly List<AuditEntry> Entries = [];
        public void Record(AuditEntry entry) => Entries.Add(entry);
    }
}

internal static class Ext
{
    public static T Also<T>(this T x, Action<T> a)
    {
        a(x);
        return x;
    }
}
