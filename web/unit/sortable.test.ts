// Drag and drop's logic (src/ui/Sortable.tsx, 0.1.3) and the dashboard layout the editor shows (api/panel.ts layoutOf):
// moving an item to a place, finding the item under the pointer, what screen readers hear, and the whole layout with
// hidden cards kept in their places.
import { describe, expect, test } from "bun:test";
import { isDefaultLayout, layoutOf } from "../src/api/panel";
import { SAY, itemAt, moveTo } from "../src/ui/Sortable";

describe("moving an item", () => {
  const order = ["a", "b", "c", "d"];
  test("to any place, clamped at both ends", () => {
    expect(moveTo(order, "a", 2)).toEqual(["b", "c", "a", "d"]);
    expect(moveTo(order, "d", 0)).toEqual(["d", "a", "b", "c"]);
    expect(moveTo(order, "b", -5)).toEqual(["b", "a", "c", "d"]);
    expect(moveTo(order, "b", 99)).toEqual(["a", "c", "d", "b"]);
    expect(moveTo(order, "c", 2)).toEqual(order);
  });
  test("an unknown key changes nothing, and the list given is never changed", () => {
    expect(moveTo(order, "x", 0)).toEqual(order);
    expect(order).toEqual(["a", "b", "c", "d"]);
  });
});

describe("the item under the pointer", () => {
  const rects = [
    { key: "a", left: 0, top: 0, right: 100, bottom: 50 }, { key: "b", left: 110, top: 0, right: 210, bottom: 80 },
    { key: "c", left: 0, top: 60, right: 100, bottom: 120 },
  ];
  test("in a grid of cards of different heights", () => {
    expect(itemAt(rects, 50, 25)).toBe("a");
    expect(itemAt(rects, 150, 70)).toBe("b");
    expect(itemAt(rects, 50, 100)).toBe("c");
    expect(itemAt(rects, 105, 25)).toBeNull(); // the gap between cards
  });
});

describe("what screen readers hear", () => {
  test("picking up, moving, dropping, canceling", () => {
    expect(SAY.grabbed("Server", 1, 5)).toBe("Picked up Server. Position 1 of 5. Use the arrow keys to move it, Space to drop it, Escape to cancel.");
    expect(SAY.moved("Server", 2, 5)).toBe("Server, position 2 of 5.");
    expect(SAY.dropped("Server", 2, 5)).toBe("Server dropped at position 2 of 5.");
    expect(SAY.canceled("Server", 1)).toBe("Move canceled. Server is back at position 1.");
  });
});

describe("the layout the editor shows", () => {
  const available = ["panel/server", "panel/links", "panel/info", "metrics/players"];
  test("hidden cards stay in their saved place; new cards come last; gone ones go", () => {
    expect(layoutOf(available, { order: ["panel/info", "gone/card", "panel/server"], hidden: ["panel/server", "gone/card"] }))
      .toEqual({ order: ["panel/info", "panel/server", "panel/links", "metrics/players"], hidden: ["panel/server"] });
  });
  test("nothing saved: the default", () => {
    expect(layoutOf(available, undefined)).toEqual({ order: available, hidden: [] });
    expect(isDefaultLayout(available, layoutOf(available, { order: [], hidden: [] }))).toBe(true);
    expect(isDefaultLayout(available, { order: available, hidden: ["panel/info"] })).toBe(false);
    expect(isDefaultLayout(available, { order: [...available].reverse(), hidden: [] })).toBe(false);
  });
});
