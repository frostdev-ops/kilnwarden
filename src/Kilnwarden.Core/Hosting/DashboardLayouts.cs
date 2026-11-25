// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Kilnwarden.Http;
using Kilnwarden.Modules;
using Kilnwarden.Routing;
using Kilnwarden.Storage;

namespace Kilnwarden.Hosting;

/// <summary>Each account's own overview layout (DESIGN §5 Dashboard: widget grid, per-user layout): the order of the
/// widgets and the ones hidden, kept per account in dashboards.json. GET/PUT /api/core/dashboard (signed in, own account
/// only). Keys are "&lt;module&gt;/&lt;widget&gt;" or the overview's own cards ("panel/server", "panel/links", "panel/info").</summary>
public sealed partial class DashboardLayouts
{
    public const string File = "dashboards.json";
    public const int MaxKeys = 64, MaxAccounts = 1000;

    public sealed class Layout
    {
        public List<string> Order { get; set; } = [];
        public List<string> Hidden { get; set; } = [];
    }

    private sealed class LayoutsFile
    {
        public int Format { get; set; } = 1;
        public Dictionary<string, Layout> Accounts { get; set; } = [];
    }

    private readonly DataStore _data;
    private readonly object _gate = new();
    private LayoutsFile _file;

    public DashboardLayouts(DataStore data)
    {
        _data = data;
        try
        {
            _file = data.ReadJson<LayoutsFile>(File) ?? new LayoutsFile();
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or IOException)
        {
            _file = new LayoutsFile(); // a layout is only a preference: an unreadable file starts over
        }
    }

    public Layout Get(string account)
    {
        lock (_gate) return _file.Accounts.TryGetValue(account, out var l) ? l : new Layout();
    }

    public void Set(string account, Layout layout)
    {
        if (layout.Order is null || layout.Hidden is null || layout.Order.Count > MaxKeys || layout.Hidden.Count > MaxKeys ||
            layout.Order.Concat(layout.Hidden).Any(k => k is null || !KeyRe().IsMatch(k)))
            throw new HttpError(422, $"order and hidden: at most {MaxKeys} widget keys each (module/widget)");
        lock (_gate)
        {
            var next = new Dictionary<string, Layout>(_file.Accounts) { [account] = new Layout { Order = [.. layout.Order.Distinct()], Hidden = [.. layout.Hidden.Distinct()] } };
            if (next.Count > MaxAccounts) throw new HttpError(409, "too many saved layouts");
            _data.WriteJson(File, new LayoutsFile { Accounts = next });
            _file = new LayoutsFile { Accounts = next };
        }
    }

    /// <summary>Drops the layouts of accounts that no longer exist.</summary>
    public void Prune(IEnumerable<string> liveAccounts)
    {
        var live = liveAccounts.ToHashSet(StringComparer.Ordinal);
        lock (_gate)
        {
            if (_file.Accounts.Keys.All(live.Contains)) return;
            var next = _file.Accounts.Where(kv => live.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            _data.WriteJson(File, new LayoutsFile { Accounts = next });
            _file = new LayoutsFile { Accounts = next };
        }
    }

    public static void Map(ModuleRegistration core, DashboardLayouts layouts)
    {
        core.MapGet("/dashboard", RouteAccess.SignedIn, ctx =>
        {
            var l = layouts.Get(ctx.User!.Id);
            return ValueTask.FromResult(HttpResponse.Json(new JsonObject
            {
                ["order"] = new JsonArray([.. l.Order.Select(k => (JsonNode)k)]), ["hidden"] = new JsonArray([.. l.Hidden.Select(k => (JsonNode)k)]),
            }));
        });
        core.MapPut("/dashboard", RouteAccess.SignedIn, ctx =>
        {
            layouts.Set(ctx.User!.Id, ctx.ReadJson<Layout>());
            return ValueTask.FromResult(HttpResponse.Json(new { ok = true }));
        }, 16 * 1024);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,63}/[a-z0-9][a-z0-9_-]{0,63}$")]
    private static partial Regex KeyRe();
}
