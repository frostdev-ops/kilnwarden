// The setup wizard's "Customize your dashboard" (0.1.3, optional): the overview's own cards as a live preview, in the
// order they will have. Drag a card by its handle (mouse, touch, or the keyboard: Space, the arrow keys, Space), switch
// each on or off, or go back to the default in one click. It is the overview's Customize editor (OverviewCards.tsx),
// saved in the same per-account layout (PUT /api/core/dashboard).
import { useEffect, useRef, useState, type ReactNode } from "react";
import type { DashboardLayout } from "../api/panel";
import { LayoutEditor, useOverview } from "../pages/OverviewCards";
import { Banner, Button, Loading, explain } from "../ui/kit";
import { Frame } from "./Frame";
import type { Route } from "./wizard";

/** A card drawn small and untouchable: the preview shows it, the page's controls are the editor's. */
function Preview({ children }: { children: ReactNode }) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => { ref.current?.setAttribute("inert", ""); }, []);
  return (
    <div className="wa-preview" ref={ref} aria-hidden data-words="skip">
      <div className="wa-preview-inner">{children}</div>
    </div>
  );
}

export function DashboardStep({ route, back, next }: { route: Route | ""; back: () => void; next: () => void }) {
  const { available, titleOf, render, save, saved, layout, modules } = useOverview();
  const [draft, setDraft] = useState<DashboardLayout | null>(null);
  const value = draft ?? saved;
  const ready = !layout.isLoading && !modules.isLoading;
  return (
    <Frame step="dashboard" route={route} title="Customize your dashboard" back={back} skip={next} wide>
      <p className="wa-lead">Drag the cards into the order you like. Switch off what you don't need.</p>
      {!ready ? <Loading /> : (
        <LayoutEditor value={value} onChange={setDraft} titleOf={titleOf} className="wa-preview-grid" listLabel="Your dashboard's cards"
          content={(key, shown) => (shown ? <Preview>{render(key)}</Preview> : null)} />
      )}
      {save.isError && <Banner tone="err" title={`Not saved. ${explain(save.error)}`} />}
      <div className="wa-row">
        <Button kind="primary" busy={save.isPending} disabled={!ready} onClick={() => save.mutate(value, { onSuccess: next })}>Save</Button>
        <Button kind="ghost" disabled={!ready} onClick={() => setDraft({ order: [...available], hidden: [] })}>Use the default</Button>
      </div>
    </Frame>
  );
}
