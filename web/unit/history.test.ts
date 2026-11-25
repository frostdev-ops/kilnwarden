import { describe, expect, test } from "bun:test";
import { clearHistories, loadHistory, saveHistory, type HistoryStore } from "../src/commands/history";

class MemoryStore implements HistoryStore {
  map = new Map<string, string>();
  get length() { return this.map.size; }
  key(i: number) { return [...this.map.keys()][i] ?? null; }
  getItem(k: string) { return this.map.get(k) ?? null; }
  setItem(k: string, v: string) { this.map.set(k, v); }
  removeItem(k: string) { this.map.delete(k); }
}

// Security review S5: the console's history belongs to one account in one tab session, and sign-out clears it.
describe("console history", () => {
  test("is per account and never shared through the old browser-wide key", () => {
    const session = new MemoryStore();
    const legacy = new MemoryStore();
    legacy.setItem("kwa.console.history", JSON.stringify(["/give owner secret-item"]));
    saveHistory("u-a", ["/kick bob"], session, legacy);
    expect(legacy.getItem("kwa.console.history")).toBeNull();
    expect(loadHistory("u-a", session, legacy)).toEqual(["/kick bob"]);
    expect(loadHistory("u-b", session, legacy)).toEqual([]);
  });

  test("sign-out removes every account's history", () => {
    const session = new MemoryStore();
    const legacy = new MemoryStore();
    saveHistory("u-a", ["/a"], session, legacy);
    saveHistory("u-b", ["/b"], session, legacy);
    session.setItem("other", "kept");
    clearHistories(session, legacy);
    expect(loadHistory("u-a", session, legacy)).toEqual([]);
    expect(loadHistory("u-b", session, legacy)).toEqual([]);
    expect(session.getItem("other")).toBe("kept");
  });

  test("keeps the newest 100 lines and ignores junk", () => {
    const session = new MemoryStore();
    const legacy = new MemoryStore();
    saveHistory("u-a", Array.from({ length: 150 }, (_, i) => `/l${i}`), session, legacy);
    expect(loadHistory("u-a", session, legacy)).toHaveLength(100);
    session.setItem("kwa.console.history.u-c", "{not json");
    expect(loadHistory("u-c", session, legacy)).toEqual([]);
  });
});
