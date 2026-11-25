// A command form's values as the line Kiln parses (docs/kiln/commands.md, "Argument types"): numbers in invariant form
// within min/max, on/off for booleans, an enum's or choice's option, a player's name (its entity id when the name has
// a space) or a selector, x y z with ~ offsets, durations and times as typed, words without spaces, quotable strings
// quoted ("a \"b\"") when they need it, greedy text last and as typed. An optional argument left empty ends the line:
// a later one cannot be filled. Pure (unit/commands.test.ts).
import type { CommandNode } from "@kiln/admin-ui";
import type { CommandForm } from "./tree";
import { durationSeconds, timeHours } from "./units";

/** A position's three parts and whether each is relative (~). */
export interface PositionValue {
  x: string;
  y: string;
  z: string;
  rel?: [boolean, boolean, boolean];
}

export type ArgValue = string | PositionValue | undefined;

export interface BuiltLine {
  line: string;
  problems: string[];
}

const NUM = /^[+-]?(\d+(\.\d*)?|\.\d+)([eE][+-]?\d+)?$/;
const WS = /\s/;

export function isEmpty(v: ArgValue): boolean {
  if (v === undefined) return true;
  if (typeof v === "string") return v.trim() === "";
  return [v.x, v.y, v.z].every((p) => p.trim() === "") && !(v.rel ?? []).some(Boolean);
}

/** One argument's text, or a problem. */
export function token(node: CommandNode, name: string, v: ArgValue): { text?: string; problem?: string } {
  const type = (node.type ?? "string").toLowerCase();
  if (type === "position") {
    const p = typeof v === "object" ? v : { x: String(v ?? ""), y: "", z: "" };
    const parts = [p.x, p.y, p.z].map((s, i) => {
      const t = s.trim();
      const rel = p.rel?.[i] ?? false;
      if (rel) {
        if (t !== "" && !NUM.test(t)) return null;
        if (!node.relative && node.relative !== undefined) return null;
        return `~${t === "0" ? "" : t}`;
      }
      return NUM.test(t) ? t : null;
    });
    if (parts.some((x) => x === null)) return { problem: `${name}: three numbers (x y z)${node.relative !== false ? ", ~ for relative" : ""}` };
    return { text: parts.join(" ") };
  }
  const s = typeof v === "string" ? v.trim() : "";
  switch (type) {
    case "int":
    case "integer":
    case "long":
    case "double":
    case "float":
    case "number": {
      if (!NUM.test(s)) return { problem: `${name}: a number` };
      const n = Number(s);
      if ((type === "int" || type === "integer" || type === "long") && !Number.isInteger(n)) return { problem: `${name}: a whole number` };
      if (typeof node.min === "number" && n < node.min) return { problem: `${name}: at least ${node.min}` };
      if (typeof node.max === "number" && n > node.max) return { problem: `${name}: at most ${node.max}` };
      return { text: s };
    }
    case "bool":
    case "boolean":
      if (!/^(on|off|true|false|yes|no|1|0)$/i.test(s)) return { problem: `${name}: on or off` };
      return { text: /^(on|true|yes|1)$/i.test(s) ? "on" : "off" };
    case "enum":
    case "choice": {
      const opts = node.options ?? [];
      const hit = opts.find((o) => o.toLowerCase() === s.toLowerCase());
      if (opts.length && !hit) return { problem: `${name}: one of ${opts.join(", ")}` };
      if (WS.test(s)) return { problem: `${name}: one word` };
      return { text: hit ?? s };
    }
    case "greedy":
      return { text: typeof v === "string" ? v.replace(/^\s+/, "").replace(/[\r\n]+/g, " ") : "" };
    case "word":
      if (WS.test(s)) return { problem: `${name}: one word (no spaces)` };
      return { text: s };
    case "player":
    case "players":
    case "entity":
    case "entities":
      if (WS.test(s) && !/^@[a-z]\[.*\]$/.test(s)) return { problem: `${name}: no spaces (a name with a space: use its entity id)` };
      return { text: s };
    case "duration": {
      if (WS.test(s)) return { problem: `${name}: a duration such as 90s, 5m or 1h30m (no spaces)` };
      // a range in seconds (unit s), checked when the duration is one Kiln and this page both read
      const secs = durationSeconds(s);
      if (secs !== null && typeof node.min === "number" && secs < node.min) return { problem: `${name}: at least ${node.min} s` };
      if (secs !== null && typeof node.max === "number" && secs > node.max) return { problem: `${name}: at most ${node.max} s` };
      return { text: s };
    }
    case "time": {
      if (WS.test(s)) return { problem: `${name}: a time of day such as 7, 07:30, 7pm or noon (no spaces)` };
      const h = timeHours(s);
      const lo = typeof node.min === "number" ? node.min : 0, hi = typeof node.max === "number" ? node.max : 24;
      if (h !== null && (h < lo || h > hi)) return { problem: `${name}: a time of day from ${lo} to ${hi} h` };
      return { text: s };
    }
    default:
      // string and unknown types: quoted when needed (also "item": display names may have spaces)
      if (node.greedy) return { text: s };
      return { text: quote(s) };
  }
}

/** A word as is; text with spaces or quotes as "text" with \" and \\ escaped. */
export function quote(s: string): string {
  if (s !== "" && !/[\s"'\\]/.test(s)) return s;
  return `"${s.replace(/\\/g, "\\\\").replace(/"/g, '\\"')}"`;
}

/** The line for `form` of command `name` with `values` (by argument index). */
export function buildLine(name: string, form: CommandForm | null, values: ArgValue[], freeText = ""): BuiltLine {
  const parts = [`/${name}`];
  const problems: string[] = [];
  if (!form) {
    const rest = freeText.replace(/[\r\n]+/g, " ").trim();
    return { line: rest ? `${parts[0]} ${rest}` : parts[0], problems };
  }
  parts.push(...form.literals);
  let ended: string | null = null;
  form.args.forEach((a, i) => {
    const v = values[i];
    if (isEmpty(v)) {
      if (!a.optional) problems.push(`${a.name}: required`);
      else ended ??= a.name;
      return;
    }
    if (ended) {
      problems.push(`${a.name}: fill [${ended}] first, or leave the arguments after it empty`);
      return;
    }
    const t = token(a.node, a.name, v);
    if (t.problem) problems.push(t.problem);
    else if (t.text !== undefined && t.text !== "") parts.push(t.text);
  });
  return { line: parts.join(" "), problems };
}

/** A default value as the form shows it (placeholder), or "". */
export function defaultText(node: CommandNode): string {
  const d = node.default;
  if (d === undefined || d === null) return "";
  if (typeof d === "boolean") return d ? "on" : "off";
  if ((node.type ?? "") === "duration" && typeof d === "number") return `${d}s`;
  return String(d);
}
