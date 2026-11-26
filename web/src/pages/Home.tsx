// Overview: the server, quick links, the panel and every module's widgets (OverviewCards.tsx), in the account's own
// layout (GET/PUT /api/core/dashboard: order and hidden cards). Customize (0.1.3) is the same editor as the setup
// wizard's "Customize your dashboard": drag a card by its handle (mouse, touch, or the keyboard: Space, the arrow keys,
// Space), switch each card on or off where it is, or go back to the default with one click. The cards wait for the
// saved layout, so they never jump from the default order to the account's.
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useSession } from "../app/context";
import { panel } from "../app/panel";
import type { DashboardLayout } from "../api/panel";
import { can } from "../state/session";
import { duration } from "../ui/format";
import { Banner, Button, LoadError, Loading, Page, explain } from "../ui/kit";
import { LayoutEditor, useOverview } from "./OverviewCards";

export function HomePage() {
  const branding = useSession((s) => s.branding);
  const { me, modules, layout, available, titleOf, render, save, saved } = useOverview();
  const online = useQuery({ queryKey: ["activity", "online"], queryFn: panel.activityOnline, refetchInterval: 15_000, enabled: can(me, "activity.view") });
  const [draft, setDraft] = useState<DashboardLayout | null>(null);
  const [note, setNote] = useState<{ tone: "ok" | "err" | "warn"; text: string } | null>(null);
  const editing = draft !== null;
  const visible = saved.order.filter((k) => !saved.hidden.includes(k));
  const you = online.data?.players.find((p) => p.you);
  const store = (l: DashboardLayout) => save.mutate(l, {
    onSuccess: () => {
      setDraft(null);
      setNote({ tone: "ok", text: "Your overview is saved. Only your account sees this layout." });
    },
    onError: (e) => setNote({ tone: "err", text: `Not saved. ${explain(e)}` }),
  });

  return (
    <Page title={branding.name} sub="Overview" wide actions={editing ? (
      <>
        <Button kind="primary" busy={save.isPending} onClick={() => draft && store(draft)}>Save layout</Button>
        <Button kind="ghost" onClick={() => setDraft({ order: [...available], hidden: [] })}>Use the default</Button>
        <Button kind="ghost" onClick={() => { setDraft(null); setNote(null); }}>Cancel</Button>
      </>
    ) : <Button onClick={() => { setNote(null); setDraft(saved); }} disabled={layout.isLoading || modules.isLoading}>Customize</Button>}>
      {you && <Banner tone="ember" title={`You are in game as ${you.name}`}>On for {duration(you.seconds)}.</Banner>}
      <div aria-live="polite">{note && <Banner tone={note.tone} title={note.text} actions={<Button size="s" kind="ghost" onClick={() => setNote(null)}>Close</Button>} />}</div>
      {editing && <Banner tone="info" title="Customize your overview">Drag a card by its handle to move it, or use the switch to show or hide it. Then save.</Banner>}
      {modules.isError && <LoadError what="The widgets" error={modules.error} retry={() => void modules.refetch()} />}
      {layout.isError && !editing && <Banner tone="warn" title="Your saved layout did not load">The overview shows every card in the default order.</Banner>}
      {layout.isLoading ? <Loading /> : draft ? (
        <LayoutEditor value={draft} onChange={setDraft} titleOf={titleOf} className="wa-dash wa-dash-editing"
          content={(key, shown) => (shown ? render(key) : <p className="k-muted wa-dash-placeholder">Hidden</p>)} />
      ) : (
        <div className="wa-dash">
          {visible.map((key) => <div key={key} className="wa-dash-item" data-card={key}>{render(key)}</div>)}
        </div>
      )}
    </Page>
  );
}
