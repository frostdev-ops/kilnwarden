import { describe, expect, test } from "bun:test";
import { buildLine, token } from "../src/commands/line";
import { WORLD, durationSeconds, durationText, groundDistance, planeToWorld, splitDuration, timeHours, timeText, worldToPlane } from "../src/commands/units";

describe("duration and time pickers", () => {
  test("a number and a unit make Kiln's duration", () => {
    expect(durationText("90", "s")).toBe("90s");
    expect(durationText("5", "m")).toBe("5m");
    expect(durationText("1.5", "h")).toBe("1.5h");
    expect(durationText("", "s")).toBe("");
    expect(durationText("-3", "s")).toBe("");
    expect(splitDuration("5m")).toEqual({ amount: "5", unit: "m" });
    expect(splitDuration("1h30m")).toBeNull();
  });

  test("durations as Kiln reads them, in seconds", () => {
    expect(durationSeconds("90s")).toBe(90);
    expect(durationSeconds("5m")).toBe(300);
    expect(durationSeconds("1h30m")).toBe(5400);
    expect(durationSeconds("250ms")).toBe(0.25);
    expect(durationSeconds("00:05:00")).toBe(300);
    expect(durationSeconds("90")).toBe(90);
    expect(durationSeconds("soon")).toBeNull();
  });

  test("an hour:minute input as the time of day", () => {
    expect(timeText("7:30")).toBe("07:30");
    expect(timeText("24:00")).toBe("24:00");
    expect(timeText("24:30")).toBe("");
    expect(timeText("")).toBe("");
    expect(timeHours("07:30")).toBe(7.5);
    expect(timeHours("7pm")).toBe(19);
    expect(timeHours("noon")).toBe(12);
    expect(timeHours("dusk")).toBeNull();
  });

  test("the line checks a duration's range in seconds and a time's in hours", () => {
    const d = { type: "duration", min: 1, max: 86400 } as never;
    expect(token(d, "duration", "5m")).toEqual({ text: "5m" });
    expect(token(d, "duration", "2d")).toEqual({ text: "2d" }); // a form this page cannot read is Kiln's to check
    expect(token(d, "duration", "25h").problem).toBe("duration: at most 86400 s");
    const t = { type: "time", min: 0, max: 24 } as never;
    expect(token(t, "time", "07:30")).toEqual({ text: "07:30" });
    expect(token(t, "time", "dusk")).toEqual({ text: "dusk" });
    expect(token(t, "time", "25")).toEqual({ problem: "time: a time of day from 0 to 24 h" });
    const form = { key: "add", literals: ["add"], args: [{ name: "duration", node: d, optional: false }] } as never;
    expect(buildLine("time", form, ["90s"]).line).toBe("/time add 90s");
  });
});

describe("the world plane", () => {
  test("a click becomes world x and z (north up), and back", () => {
    expect(planeToWorld(0, 320, 320, 320)).toEqual({ x: 0, z: 0 });
    expect(planeToWorld(320, 0, 320, 320)).toEqual({ x: WORLD, z: WORLD });
    expect(planeToWorld(160, 80, 320, 320)).toEqual({ x: 5120, z: 7680 });
    expect(planeToWorld(-5, 400, 320, 320)).toEqual({ x: 0, z: 0 });
    expect(worldToPlane(5120, 7680, 100, 100)).toEqual({ px: 50, py: 25 });
    expect(groundDistance({ x: 0, z: 0 }, { x: 3, z: 4 })).toBe(5);
  });
});
