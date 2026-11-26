// Drag and drop to reorder a list, made by hand (0.1.3; no library): a mouse, a finger or a pen drags an item by its
// handle, and the keyboard does the same (Space or Enter picks it up, the arrow keys move it, Space or Enter drops it,
// Escape puts it back). Each move is announced to screen readers. The order changes live (onChange) while dragging, so
// whatever shows the list (the overview's cards, the setup wizard's preview) shows the new order at once.
// A touch on an item outside its handle still scrolls the page; the handle itself takes the touch (touch-action: none).
import { useEffect, useId, useLayoutEffect, useRef, useState, type KeyboardEvent, type PointerEvent, type ReactNode } from "react";
import { createPortal } from "react-dom";

/** What the handle (a button) of one item gets: spread it on the element. */
export interface HandleProps {
  ref: (el: HTMLButtonElement | null) => void;
  onPointerDown: (e: PointerEvent<HTMLButtonElement>) => void;
  onKeyDown: (e: KeyboardEvent<HTMLButtonElement>) => void;
  onBlur: () => void;
  "aria-label": string;
  "aria-describedby": string;
  "aria-pressed": boolean;
  className: string;
  type: "button";
}

export interface ItemState { dragging: boolean; grabbed: boolean; index: number; count: number }

/** `order` with `key` moved to place `to` (0-based, clamped). */
export function moveTo(order: readonly string[], key: string, to: number): string[] {
  const from = order.indexOf(key);
  if (from < 0) return [...order];
  const next = order.filter((k) => k !== key);
  next.splice(Math.max(0, Math.min(next.length, to)), 0, key);
  return next;
}

/** The key whose rectangle holds the point, among the list's items (null: none). */
export function itemAt(rects: readonly { key: string; left: number; top: number; right: number; bottom: number }[], x: number, y: number): string | null {
  return rects.find((r) => x >= r.left && x <= r.right && y >= r.top && y <= r.bottom)?.key ?? null;
}

/** The screen reader's sentences. */
export const SAY = {
  grabbed: (label: string, at: number, of: number) => `Picked up ${label}. Position ${at} of ${of}. Use the arrow keys to move it, Space to drop it, Escape to cancel.`,
  lifted: (label: string, at: number, of: number) => `Picked up ${label}. Position ${at} of ${of}.`,
  moved: (label: string, at: number, of: number) => `${label}, position ${at} of ${of}.`,
  dropped: (label: string, at: number, of: number) => `${label} dropped at position ${at} of ${of}.`,
  canceled: (label: string, at: number) => `Move canceled. ${label} is back at position ${at}.`,
};

interface Drag {
  key: string; pointerId: number; x0: number; y0: number; touch: boolean; active: boolean; origin: string[]; lastOver: string | null; x: number; y: number;
  stop: () => void;
}

function scrollParent(el: HTMLElement | null): HTMLElement | null {
  for (let e = el?.parentElement ?? null; e; e = e.parentElement) {
    const o = getComputedStyle(e).overflowY;
    if ((o === "auto" || o === "scroll") && e.scrollHeight > e.clientHeight) return e;
  }
  return (document.scrollingElement as HTMLElement | null) ?? null;
}

export function SortableList({ items, label, onChange, render, className, itemClassName, listLabel }: {
  items: readonly string[];
  label: (key: string) => string;
  onChange: (next: string[]) => void;
  render: (key: string, handle: HandleProps, state: ItemState) => ReactNode;
  className?: string;
  itemClassName?: (key: string) => string | undefined;
  listLabel: string;
}) {
  const help = useId();
  const listRef = useRef<HTMLUListElement | null>(null);
  const handles = useRef(new Map<string, HTMLButtonElement>());
  const order = useRef<readonly string[]>(items);
  order.current = items;
  const change = useRef(onChange);
  change.current = onChange;
  const [dragging, setDragging] = useState<string | null>(null);
  const [ghost, setGhost] = useState<{ x: number; y: number } | null>(null);
  const [grabbed, setGrabbed] = useState<{ key: string; origin: string[] } | null>(null);
  const grabbedRef = useRef(grabbed);
  grabbedRef.current = grabbed;
  const moving = useRef(false);
  const drag = useRef<Drag | null>(null);
  const raf = useRef(0);
  const [said, setSaid] = useState("");
  const flip = useRef(false);
  const say = (text: string) => {
    // the same sentence twice must still be read: it changes by one invisible character
    flip.current = !flip.current;
    setSaid(text + (flip.current ? " " : ""));
  };
  const pos = (key: string, list: readonly string[] = order.current) => list.indexOf(key) + 1;

  // after a keyboard move the grabbed handle keeps the focus (React may have moved its node)
  useLayoutEffect(() => {
    if (grabbed && moving.current) handles.current.get(grabbed.key)?.focus({ preventScroll: false });
    moving.current = false;
  });

  useEffect(() => () => {
    cancelAnimationFrame(raf.current);
    drag.current?.stop();
  }, []);

  const hitTest = (d: Drag) => {
    const root = listRef.current;
    if (!root) return;
    const rects = [...root.querySelectorAll<HTMLElement>(":scope > [data-sort-key]")].map((el) => {
      const r = el.getBoundingClientRect();
      return { key: el.dataset.sortKey!, left: r.left, top: r.top, right: r.right, bottom: r.bottom };
    });
    const over = itemAt(rects, d.x, d.y);
    if (over && over !== d.key && over !== d.lastOver) {
      d.lastOver = over;
      change.current(moveTo(order.current, d.key, order.current.indexOf(over)));
    } else if (!over) d.lastOver = null;
  };

  // near the top or bottom edge of whatever scrolls, it scrolls while dragging (a phone's short screen)
  const autoScroll = () => {
    const d = drag.current;
    if (!d?.active) return;
    const sp = scrollParent(listRef.current);
    if (sp) {
      const top = sp === document.scrollingElement ? 0 : sp.getBoundingClientRect().top;
      const bottom = sp === document.scrollingElement ? window.innerHeight : sp.getBoundingClientRect().bottom;
      const edge = 48;
      const step = d.y < top + edge ? -Math.ceil((top + edge - d.y) / 4) : d.y > bottom - edge ? Math.ceil((d.y - (bottom - edge)) / 4) : 0;
      if (step) {
        sp.scrollTop += step;
        hitTest(d);
      }
    }
    raf.current = requestAnimationFrame(autoScroll);
  };

  const end = (dropped: boolean) => {
    const d = drag.current;
    d?.stop();
    drag.current = null;
    cancelAnimationFrame(raf.current);
    setGhost(null);
    setDragging(null);
    if (!d?.active) return;
    if (dropped) say(SAY.dropped(label(d.key), pos(d.key), order.current.length));
    else {
      change.current(d.origin);
      say(SAY.canceled(label(d.key), pos(d.key, d.origin)));
    }
  };

  // the drag's moves and its end are followed on the window: React moves the card's node while it is dragged, and a
  // node that moves loses the pointer's capture
  const onMove = (e: globalThis.PointerEvent) => {
    const d = drag.current;
    if (!d || d.pointerId !== e.pointerId) return;
    d.x = e.clientX;
    d.y = e.clientY;
    if (!d.active) {
      if (Math.hypot(d.x - d.x0, d.y - d.y0) < (d.touch ? 3 : 5)) return;
      d.active = true;
      setDragging(d.key);
      say(SAY.lifted(label(d.key), pos(d.key), order.current.length));
      raf.current = requestAnimationFrame(autoScroll);
    }
    if (e.cancelable) e.preventDefault();
    setGhost({ x: d.x, y: d.y });
    hitTest(d);
  };

  const keyMove = (key: string, to: number) => {
    const next = moveTo(order.current, key, to);
    if (next.indexOf(key) === order.current.indexOf(key)) return;
    moving.current = true;
    change.current(next);
    say(SAY.moved(label(key), next.indexOf(key) + 1, next.length));
  };

  const handleFor = (key: string): HandleProps => ({
    type: "button",
    className: "wa-grip",
    ref: (el) => { if (el) handles.current.set(key, el); else handles.current.delete(key); },
    "aria-label": `Move ${label(key)}`,
    "aria-describedby": help,
    "aria-pressed": grabbed?.key === key,
    onPointerDown: (e) => {
      if (e.pointerType === "mouse" && e.button !== 0) return;
      if (drag.current) end(false);
      if (grabbedRef.current) setGrabbed(null);
      e.preventDefault();
      e.currentTarget.focus({ preventScroll: true });
      const id = e.pointerId;
      const up = (ev: globalThis.PointerEvent) => { if (ev.pointerId === id) end(true); };
      const cancel = (ev: globalThis.PointerEvent) => { if (ev.pointerId === id) end(false); };
      window.addEventListener("pointermove", onMove, { passive: false });
      window.addEventListener("pointerup", up);
      window.addEventListener("pointercancel", cancel);
      const stop = () => {
        window.removeEventListener("pointermove", onMove);
        window.removeEventListener("pointerup", up);
        window.removeEventListener("pointercancel", cancel);
      };
      drag.current = { key, pointerId: id, x0: e.clientX, y0: e.clientY, touch: e.pointerType !== "mouse", active: false, origin: [...order.current], lastOver: null, x: e.clientX, y: e.clientY, stop };
    },
    onKeyDown: (e) => {
      if (drag.current?.active) {
        if (e.key === "Escape") { e.preventDefault(); end(false); }
        return;
      }
      const g = grabbedRef.current;
      const i = order.current.indexOf(key);
      if (g?.key !== key) {
        if (e.key === " " || e.key === "Enter") {
          e.preventDefault();
          setGrabbed({ key, origin: [...order.current] });
          say(SAY.grabbed(label(key), i + 1, order.current.length));
        }
        return;
      }
      switch (e.key) {
        case "ArrowUp": case "ArrowLeft": e.preventDefault(); keyMove(key, i - 1); break;
        case "ArrowDown": case "ArrowRight": e.preventDefault(); keyMove(key, i + 1); break;
        case "Home": e.preventDefault(); keyMove(key, 0); break;
        case "End": e.preventDefault(); keyMove(key, order.current.length - 1); break;
        case " ": case "Enter":
          e.preventDefault();
          setGrabbed(null);
          say(SAY.dropped(label(key), i + 1, order.current.length));
          break;
        case "Escape":
          e.preventDefault();
          moving.current = true;
          change.current(g.origin);
          setGrabbed(null);
          say(SAY.canceled(label(key), g.origin.indexOf(key) + 1));
          break;
        default:
      }
    },
    onBlur: () => {
      // a blur that a keyboard move caused is not a drop; leaving the handle otherwise drops the card where it is
      if (moving.current) return;
      const g = grabbedRef.current;
      if (g?.key === key) {
        setGrabbed(null);
        say(SAY.dropped(label(key), pos(key), order.current.length));
      }
    },
  });

  return (
    <>
      <ul ref={listRef} className={["wa-sort", className, dragging && "wa-sort-active"].filter(Boolean).join(" ")} aria-label={listLabel}>
        {items.map((key, index) => {
          const state = { dragging: dragging === key, grabbed: grabbed?.key === key, index, count: items.length };
          return (
            <li key={key} data-sort-key={key} className={["wa-sort-item", itemClassName?.(key), state.dragging && "dragging", state.grabbed && "grabbed"].filter(Boolean).join(" ")}>
              {render(key, handleFor(key), state)}
            </li>
          );
        })}
      </ul>
      <p id={help} className="k-sr-only" data-words="skip">Press Space or Enter to pick it up. Move it with the arrow keys. Space or Enter drops it; Escape cancels.</p>
      <div className="k-sr-only" aria-live="assertive" aria-atomic="true" data-words="skip">{said}</div>
      {dragging && ghost && createPortal(<div className="wa-sort-ghost" style={{ left: ghost.x, top: ghost.y }} aria-hidden>{label(dragging)}</div>, document.body)}
    </>
  );
}
