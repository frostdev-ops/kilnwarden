// Commands as forms (DESIGN §5a): commands_describe's catalog grouped by mod and searchable, and each typed command's
// tree as the forms it allows. A form is one way through the tree: the literals on the way (its subcommand: "zone
// create") and the arguments after them, in order. An argument is optional when the line may end before it (an
// executable node before it, or its own `optional`). Arguments that branch (tp <player> | tp <x> <y> <z>) give one form
// each; forms with the same literals are variants of one tab. Pure functions (unit/commands.test.ts).
import type { CommandInfo, CommandNode } from "@kiln/admin-ui";

export interface FormArg {
  node: CommandNode;
  name: string;
  optional: boolean;
}

export interface CommandForm {
  /** the literals after the command's name, e.g. ["zone", "create"] for /claim zone create */
  literals: string[];
  args: FormArg[];
  /** a permission level of the deepest subcommand node, when the tree has one */
  level: string | null;
  description: string | null;
}

export interface FormTab {
  key: string;
  label: string;
  forms: CommandForm[];
}

const MAX_DEPTH = 16;
const MAX_FORMS = 64;

/** Every form of a typed command (none for an untyped one: it takes free text). */
export function formsOf(cmd: CommandInfo): CommandForm[] {
  const root = cmd.tree;
  if (!cmd.typed || !root) return [];
  const out: CommandForm[] = [];
  const walk = (node: CommandNode, literals: string[], args: FormArg[], level: string | null, desc: string | null, depth: number) => {
    if (out.length >= MAX_FORMS) return;
    const kids = depth >= MAX_DEPTH ? [] : node.children ?? [];
    const lvl = node.level ?? level;
    const d = node.description ?? desc;
    if (kids.length === 0) {
      out.push({ literals, args, level: lvl, description: d });
      return;
    }
    // an executable node with children: its own form ends here too (when it is a literal: "zone" alone)
    const executableHere = !!node.executable;
    if (executableHere && kids.every((k) => k.kind === "literal")) out.push({ literals, args, level: lvl, description: d });
    for (const k of kids) {
      if (k.kind === "literal") walk(k, [...literals, k.name ?? ""], args, lvl, null, depth + 1);
      else walk(k, literals, [...args, { node: k, name: k.name ?? `arg${args.length + 1}`, optional: executableHere || k.optional === true }], lvl, d, depth + 1);
    }
  };
  walk(root, [], [], root.level ?? null, root.description ?? cmd.description ?? null, 0);
  // an argument after an optional one is optional too (the line can only end, not skip)
  for (const f of out) {
    let opt = false;
    for (const a of f.args) {
      opt ||= a.optional;
      a.optional = opt;
    }
  }
  return dedupe(out);
}

function dedupe(forms: CommandForm[]): CommandForm[] {
  const seen = new Set<string>();
  return forms.filter((f) => {
    const k = [...f.literals, "|", ...f.args.map((a) => `${a.name}:${a.node.type ?? ""}`)].join(" ");
    if (seen.has(k)) return false;
    seen.add(k);
    return true;
  });
}

/** Forms grouped into tabs by their literals ("" = the command itself). */
export function tabsOf(forms: CommandForm[]): FormTab[] {
  const tabs = new Map<string, FormTab>();
  for (const f of forms) {
    const key = f.literals.join(" ");
    let t = tabs.get(key);
    if (!t) tabs.set(key, (t = { key, label: key || "(main)", forms: [] }));
    t.forms.push(f);
  }
  return [...tabs.values()];
}

/** A variant's label inside a tab: its arguments as <name> / [name]. */
export function usageOf(f: CommandForm): string {
  return f.args.map((a) => (a.optional ? `[${a.name}]` : `<${a.name}>`)).join(" ") || "(no arguments)";
}

export interface CatalogGroup {
  owner: string;
  commands: CommandInfo[];
}

/** The catalog grouped by owning mod (Kiln's own first), filtered by a search over names, aliases and descriptions. */
export function groupCatalog(list: CommandInfo[], query = ""): CatalogGroup[] {
  const q = query.trim().toLowerCase().replace(/^\//, "");
  const hit = (c: CommandInfo) =>
    !q || c.name.toLowerCase().includes(q) || (c.aliases ?? []).some((a) => a.toLowerCase().includes(q)) || (c.description ?? "").toLowerCase().includes(q) ||
    (c.owner ?? "").toLowerCase().includes(q);
  const groups = new Map<string, CommandInfo[]>();
  for (const c of list) {
    if (!c || typeof c.name !== "string" || !hit(c)) continue;
    const owner = c.owner || "other";
    (groups.get(owner) ?? groups.set(owner, []).get(owner)!).push(c);
  }
  return [...groups.entries()]
    .map(([owner, commands]) => ({ owner, commands: commands.sort((a, b) => a.name.localeCompare(b.name)) }))
    .sort((a, b) => (a.owner === "kiln" ? -1 : b.owner === "kiln" ? 1 : a.owner.localeCompare(b.owner)));
}
