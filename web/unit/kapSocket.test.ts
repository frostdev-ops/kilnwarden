import { describe, expect, test } from "bun:test";
import { kapUrl, trackingWebSocket } from "../src/api/kapSocket";

class FakeWs {
  static last: FakeWs;
  readyState = 0;
  binaryType = "blob";
  sent: string[] = [];
  closed: [number?, string?] | null = null;
  listeners: Record<string, ((ev: unknown) => void)[]> = {};
  constructor(readonly url: string) {
    FakeWs.last = this;
  }
  send(d: string) {
    this.sent.push(d);
  }
  close(code?: number, reason?: string) {
    this.closed = [code, reason];
  }
  addEventListener(type: string, l: (ev: unknown) => void) {
    (this.listeners[type] ??= []).push(l);
  }
  fire(type: string, ev: unknown) {
    for (const l of this.listeners[type] ?? []) l(ev);
  }
}

describe("KAP socket", () => {
  test("the URL follows the page's scheme", () => {
    expect(kapUrl({ protocol: "https:", host: "panel.example:15737" })).toBe("wss://panel.example:15737/api/server/kap");
    expect(kapUrl({ protocol: "http:", host: "127.0.0.1:15737" })).toBe("ws://127.0.0.1:15737/api/server/kap");
  });

  test("4401 and 4403 are reported, other closes are not", () => {
    const seen: [number, string][] = [];
    const Ws = trackingWebSocket((c, r) => seen.push([c, r]), FakeWs as never);
    const order: string[] = [];
    for (const code of [4401, 4403, 1000, 1006, 1012]) {
      const ws = new Ws("ws://x/api/server/kap");
      ws.addEventListener("close", (() => order.push(`transport ${code}`)) as never);
      FakeWs.last.fire("close", { code, reason: `r${code}` });
    }
    expect(seen).toEqual([[4401, "r4401"], [4403, "r4403"]]);
    // ours runs first: the app knows the code before the transport reports the final close
    expect(order[0]).toBe("transport 4401");
  });

  test("passes everything through", () => {
    const Ws = trackingWebSocket(() => undefined, FakeWs as never);
    const ws = new Ws("ws://x/y");
    const inner = FakeWs.last;
    expect(inner.url).toBe("ws://x/y");
    ws.binaryType = "arraybuffer";
    expect(inner.binaryType).toBe("arraybuffer");
    inner.readyState = 1;
    expect(ws.readyState).toBe(1);
    ws.send("{}");
    expect(inner.sent).toEqual(["{}"]);
    ws.close(1000, "bye");
    expect(inner.closed).toEqual([1000, "bye"]);
    let got: unknown = null;
    ws.addEventListener("message", ((ev: unknown) => (got = ev)) as never);
    inner.fire("message", { data: "x" });
    expect(got).toEqual({ data: "x" });
  });
});

test('top-level reauth_required opens a prompt, not a replay or socket close', () => {
  let prompts = 0;
  const Ws = trackingWebSocket(() => {}, FakeWs as never, () => prompts++);
  const ws = new Ws('ws://test/api/server/kap');
  const frames: unknown[] = [];
  ws.addEventListener('message', ((ev: unknown) => frames.push(ev)) as never);
  for (const data of ['invalid', '{"t":"res","ok":true}', '{"t":"res","ok":false,"code":"forbidden"}', '{"t":"res","id":5,"ok":false,"code":"reauth_required","error":"confirm identity"}']) FakeWs.last.fire('message', { data });
  expect(prompts).toBe(1);
  expect(frames).toHaveLength(4);
  expect(FakeWs.last.sent).toEqual([]);
  expect(FakeWs.last.closed).toBeNull();
});
