import { describe, expect, test } from "bun:test";
import type { CommandInfo } from "@kiln/admin-ui";
import { buildLine, defaultText, quote, token } from "../src/commands/line";
import { formsOf, groupCatalog, tabsOf, usageOf } from "../src/commands/tree";

const help: CommandInfo = {
  name: "help", aliases: ["?"], owner: "kiln", typed: true, permission: "everyone",
  tree: {
    kind: "literal", name: "help", executable: true,
    children: [{ kind: "argument", name: "command", executable: true, type: "word", children: [{ kind: "argument", name: "sub", executable: true, type: "greedy" }] }],
  },
};

const zone: CommandInfo = {
  name: "zone", owner: "claims", typed: true, permission: "admin",
  tree: {
    kind: "literal", name: "zone",
    children: [
      { kind: "literal", name: "create", level: "admin", children: [
        { kind: "argument", name: "name", type: "string", quotable: true, children: [
          { kind: "argument", name: "at", type: "position", relative: true, children: [
            { kind: "argument", name: "radius", type: "double", min: 1, max: 500, optional: true, default: 10, executable: true },
          ] },
        ] },
      ] },
      { kind: "literal", name: "delete", children: [{ kind: "argument", name: "name", type: "word", executable: true }] },
      { kind: "literal", name: "list", executable: true },
    ],
  },
};

const tp: CommandInfo = {
  name: "tp", owner: "kiln", typed: true,
  tree: {
    kind: "literal", name: "tp",
    children: [
      { kind: "argument", name: "target", type: "player", executable: true },
      { kind: "argument", name: "x", type: "double", children: [{ kind: "argument", name: "y", type: "double", children: [{ kind: "argument", name: "z", type: "double", executable: true }] }] },
    ],
  },
};

describe("forms from the tree", () => {
  test("help: optional arguments after an executable node", () => {
    const [f, ...rest] = formsOf(help);
    expect(rest).toEqual([]);
    expect(f.literals).toEqual([]);
    expect(f.args.map((a) => [a.name, a.optional])).toEqual([["command", true], ["sub", true]]);
    expect(usageOf(f)).toBe("[command] [sub]");
  });

  test("subcommands become tabs, with their levels", () => {
    const forms = formsOf(zone);
    const tabs = tabsOf(forms);
    expect(tabs.map((t) => t.label)).toEqual(["create", "delete", "list"]);
    const create = tabs[0].forms[0];
    expect(create.level).toBe("admin");
    expect(create.args.map((a) => [a.name, a.optional])).toEqual([["name", false], ["at", false], ["radius", true]]);
    expect(tabs[2].forms[0].args).toEqual([]);
  });

  test("branching arguments are variants of one tab", () => {
    const tabs = tabsOf(formsOf(tp));
    expect(tabs.length).toBe(1);
    expect(tabs[0].label).toBe("(main)");
    expect(tabs[0].forms.map(usageOf)).toEqual(["<target>", "<x> <y> <z>"]);
  });

  test("untyped commands have no forms (free text)", () => {
    expect(formsOf({ name: "claim", typed: false, usage: "/claim list" })).toEqual([]);
  });

  test("a deep or wide tree is capped", () => {
    const wide: CommandInfo = { name: "w", typed: true, tree: { kind: "literal", name: "w", children: Array.from({ length: 200 }, (_, i) => ({ kind: "literal", name: `s${i}`, executable: true })) } };
    expect(formsOf(wide).length).toBeLessThanOrEqual(64);
    let node: Record<string, unknown> = { kind: "argument", name: "leaf", type: "word", executable: true };
    for (let i = 0; i < 100; i++) node = { kind: "literal", name: `l${i}`, children: [node] };
    expect(() => formsOf({ name: "d", typed: true, tree: { kind: "literal", name: "d", children: [node] } })).not.toThrow();
  });
});

describe("the line", () => {
  const create = tabsOf(formsOf(zone))[0].forms[0];

  test("a full form", () => {
    const r = buildLine("zone", create, ["Old Mill", { x: "10", y: "2.5", z: "-3" }, "25"]);
    expect(r).toEqual({ line: '/zone create "Old Mill" 10 2.5 -3 25', problems: [] });
  });

  test("an optional argument left out ends the line", () => {
    expect(buildLine("zone", create, ["mill", { x: "", y: "5", z: "", rel: [true, false, true] }, ""]).line).toBe("/zone create mill ~ 5 ~");
  });

  test("required arguments and ranges are checked", () => {
    expect(buildLine("zone", create, ["", { x: "1", y: "2", z: "3" }]).problems).toEqual(["name: required"]);
    expect(buildLine("zone", create, ["m", { x: "1", y: "2", z: "3" }, "9999"]).problems).toEqual(["radius: at most 500"]);
    expect(buildLine("zone", create, ["m", { x: "a", y: "2", z: "3" }]).problems[0]).toContain("three numbers");
  });

  test("a later optional argument cannot be filled after an empty one", () => {
    const [f] = formsOf(help);
    expect(buildLine("help", f, ["", "x"]).problems[0]).toContain("fill [command] first");
    expect(buildLine("help", f, ["zone", "create  a thing"]).line).toBe("/help zone create  a thing");
  });

  test("tokens by type", () => {
    expect(token({ type: "int", min: 1, max: 5 }, "n", "3")).toEqual({ text: "3" });
    expect(token({ type: "int" }, "n", "2.5").problem).toContain("whole");
    expect(token({ type: "double" }, "n", "1e3")).toEqual({ text: "1e3" });
    expect(token({ type: "bool" }, "b", "true")).toEqual({ text: "on" });
    expect(token({ type: "bool" }, "b", "maybe").problem).toBeDefined();
    expect(token({ type: "enum", options: ["Day", "Night"] }, "e", "night")).toEqual({ text: "Night" });
    expect(token({ type: "enum", options: ["Day"] }, "e", "dusk").problem).toContain("one of Day");
    expect(token({ type: "word" }, "w", "two words").problem).toContain("one word");
    expect(token({ type: "player" }, "p", "Old Gus").problem).toContain("entity id");
    expect(token({ type: "players" }, "p", "@a[distance=..20, type=player]")).toEqual({ text: "@a[distance=..20, type=player]" });
    expect(token({ type: "position", relative: false }, "at", { x: "1", y: "2", z: "3", rel: [true, false, false] }).problem).toBeDefined();
    expect(token({ type: "greedy" }, "t", "  hello\nworld ")).toEqual({ text: "hello world " });
    expect(token({ type: "string" }, "s", 'say "hi"')).toEqual({ text: '"say \\"hi\\""' });
  });

  test("quoting", () => {
    expect(quote("plain")).toBe("plain");
    expect(quote("")).toBe('""');
    expect(quote("a b")).toBe('"a b"');
    expect(quote("back\\slash")).toBe('"back\\\\slash"');
  });

  test("untyped commands take free text on one line", () => {
    expect(buildLine("claim", null, [], " here  mill\n 20 ")).toEqual({ line: "/claim here  mill  20", problems: [] });
    expect(buildLine("claim", null, []).line).toBe("/claim");
  });

  test("defaults as placeholders", () => {
    expect(defaultText({ default: 10 })).toBe("10");
    expect(defaultText({ default: true })).toBe("on");
    expect(defaultText({ type: "duration", default: 90 })).toBe("90s");
    expect(defaultText({})).toBe("");
  });
});

describe("catalog", () => {
  test("grouped by mod, Kiln first, searchable", () => {
    const g = groupCatalog([zone, tp, help, { name: "claim", owner: "claims", description: "Protected areas" }]);
    expect(g.map((x) => x.owner)).toEqual(["kiln", "claims"]);
    expect(g[0].commands.map((c) => c.name)).toEqual(["help", "tp"]);
    expect(groupCatalog([zone, tp, help], "?").flatMap((x) => x.commands.map((c) => c.name))).toEqual(["help"]);
    expect(groupCatalog([zone, tp, help, { name: "claim", owner: "claims", description: "Protected areas" }], "protected")[0].commands[0].name).toBe("claim");
    expect(groupCatalog([zone], "/zo")[0].commands[0].name).toBe("zone");
  });

  test("odd entries are skipped", () => {
    expect(groupCatalog([null as never, { name: 5 } as never, help]).length).toBe(1);
  });
});
