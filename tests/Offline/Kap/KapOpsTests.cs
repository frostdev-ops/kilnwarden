// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using Kilnwarden.Kap;

namespace Kilnwarden.Offline.Kap;

/// <summary>BINDING (step-1 requirements, 30.9 review-2): command_run, command_complete and commands_describe always carry
/// source "web:&lt;user&gt;" and the level from the user's role; no code path may omit them.</summary>
public class KapOpsTests
{
    public static TheoryData<string> CommandOps => new(KapOps.CommandOps);

    [Theory]
    [MemberData(nameof(CommandOps))]
    public void Command_ops_always_get_the_users_source_and_level(string op)
    {
        foreach (var level in new[] { CommandLevel.Everyone, CommandLevel.Admin, CommandLevel.Console })
        {
            var a = KapOps.Stamp(KapCaller.ForUser("alice", level), op, new JsonObject { ["line"] = "/kick bob" });
            Assert.Equal("web:alice", a["source"]!.GetValue<string>());
            Assert.Equal(CommandLevels.Wire(level), a["level"]!.GetValue<string>());
        }
        // no args at all
        var b = KapOps.Stamp(KapCaller.ForUser("bob", CommandLevel.Everyone), op, null);
        Assert.Equal("web:bob", b["source"]!.GetValue<string>());
        Assert.Equal("everyone", b["level"]!.GetValue<string>());
    }

    [Theory]
    [MemberData(nameof(CommandOps))]
    public void A_caller_cannot_choose_its_own_source_or_level(string op)
    {
        var a = KapOps.Stamp(KapCaller.ForUser("mod", CommandLevel.Everyone), op,
            new JsonObject { ["line"] = "x", ["source"] = "dashboard", ["level"] = "console", ["Level"] = "console" });
        Assert.Equal("web:mod", a["source"]!.GetValue<string>());
        Assert.Equal("everyone", a["level"]!.GetValue<string>());
    }

    [Theory]
    [MemberData(nameof(CommandOps))]
    public void Command_ops_are_refused_without_a_user_or_a_level(string op)
    {
        Assert.Throws<KapRefusedException>(() => KapOps.Stamp(KapCaller.Panel, op, null));
        Assert.Throws<KapRefusedException>(() => KapOps.Stamp(KapCaller.ForUser("viewer", CommandLevel.None), op, null));
    }

    [Theory]
    [InlineData("command")]
    [InlineData("login_mint")]
    [InlineData("admin_token_rotate")]
    public void Some_ops_are_never_sent(string op)
    {
        Assert.Throws<KapRefusedException>(() => KapOps.Stamp(KapCaller.Panel, op, null));
        Assert.Throws<KapRefusedException>(() => KapOps.Stamp(KapCaller.ForUser("owner", CommandLevel.Console), op, null));
    }

    [Fact]
    public void Login_redeem_is_the_panels_own()
    {
        Assert.Throws<KapRefusedException>(() => KapOps.Stamp(KapCaller.ForUser("owner", CommandLevel.Console), "login_redeem", null));
        Assert.NotNull(KapOps.Stamp(KapCaller.Panel, "login_redeem", new JsonObject { ["secret"] = "x" }));
    }

    [Fact]
    public void By_ops_record_the_user()
    {
        foreach (var op in KapOps.ByOps)
        {
            Assert.Equal("web:ann", KapOps.Stamp(KapCaller.ForUser("ann", CommandLevel.None), op, new JsonObject { ["by"] = "someone else" })["by"]!.GetValue<string>());
            Assert.Equal(Product.ModId, KapOps.Stamp(KapCaller.Panel, op, null)["by"]!.GetValue<string>());
        }
    }

    [Fact]
    public void Identity_named_arguments_of_other_ops_are_data_and_pass_through()
    {
        // admin-protocol.md §8: give_item, creative_give and build_shape take an item or flatten `level`, and update_stage's
        // required `source` is its incoming folder. Only the command ops (source, level) and the by-ops (by) carry identity.
        var user = KapCaller.ForUser("ann", CommandLevel.Everyone);
        Assert.Equal("25", KapOps.Stamp(user, "give_item", new JsonObject { ["entity"] = 1, ["item"] = "sword", ["level"] = 25 })["level"]!.ToJsonString());
        Assert.Equal("25", KapOps.Stamp(user, "creative_give", new JsonObject { ["entity"] = 1, ["item"] = "sword", ["level"] = 25 })["level"]!.ToJsonString());
        Assert.Equal("12.5", KapOps.Stamp(user, "build_shape", new JsonObject { ["edit"] = "flatten", ["level"] = 12.5 })["level"]!.ToJsonString());
        var stage = KapOps.Stamp(user, "update_stage", new JsonObject { ["kind"] = "mod", ["source"] = "my-mod-1.2", ["by"] = "someone else" });
        Assert.Equal("my-mod-1.2", stage["source"]!.GetValue<string>());
        Assert.Equal("web:ann", stage["by"]!.GetValue<string>());
    }

    [Fact]
    public void Stamping_copies_the_args()
    {
        var args = new JsonObject { ["line"] = "x" };
        var a = KapOps.Stamp(KapCaller.ForUser("ann", CommandLevel.Admin), "command_run", args);
        Assert.False(args.ContainsKey("source"));
        Assert.NotSame(args, a);
    }

    [Theory]
    [InlineData("web:alice")]
    [InlineData("al ice")]
    [InlineData("alice\nINFO forged")]
    [InlineData("")]
    public void Source_names_are_safe(string name)
    {
        Assert.Throws<ArgumentException>(() => KapCaller.ForUser(name, CommandLevel.Everyone));
    }

    [Fact]
    public void Op_names_are_checked()
    {
        Assert.Throws<KapRefusedException>(() => KapOps.Stamp(KapCaller.Panel, "Bad Op", null));
        Assert.Throws<KapRefusedException>(() => KapOps.Stamp(KapCaller.Panel, "", null));
    }

    /// <summary>Every op of the Kiln.Admin this build targets (kiln-admin-ops.txt) has a rule: an unlisted op is refused
    /// for everyone, owners too, so a missing row silently hides a feature.</summary>
    [Fact]
    public void Policy_names_every_op_of_the_targeted_Kiln_Admin()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "kiln-admin-ops.txt");
        var ops = File.ReadAllLines(file).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
        Assert.True(ops.Count > 150, "the op list is there");
        Assert.DoesNotContain(ops, op => KapPolicy.Rule(op) is null);
    }

    [Fact]
    public void Replication_diagnostics_are_admin_and_capture_is_a_write()
    {
        foreach (var op in new[] { "repl_stats", "repl_watch", "repl_component" })
        {
            var r = KapPolicy.Rule(op)!;
            Assert.Equal(Kilnwarden.Modules.BuiltinRole.Admin, r.DefaultRole);
            Assert.False(r.Write);
        }
        var capture = KapPolicy.Rule("repl_capture")!;
        Assert.Equal(Kilnwarden.Modules.BuiltinRole.Admin, capture.DefaultRole);
        Assert.True(capture.Write);
    }

    [Fact]
    public void Policy_never_lists_a_blocked_or_unknown_op()
    {
        foreach (var op in KapOps.Never.Concat(KapOps.PanelOnly)) Assert.Null(KapPolicy.Rule(op));
        Assert.Null(KapPolicy.Rule("brand_new_op"));
        // code-loading and update ops are the owner's by default
        foreach (var op in new[] { "mod_load", "mod_reload", "update_stage", "config_set", "permissions_player_node", "backup_restore" })
            Assert.Equal(Kilnwarden.Modules.BuiltinRole.Owner, KapPolicy.Rule(op)!.DefaultRole);
    }
}
