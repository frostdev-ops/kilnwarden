// Public page settings: off by default; names, mods, map dots and widgets off until chosen. Rules and join info are
// a small, safe markdown. Saving asks for a recent proof. The page itself is /api/public/page (no script), its data
// /api/public/status; the preview below is exactly what visitors get.
import { useMutation, useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { isApiError } from "../api/http";
import type { PublicSettings } from "../api/laneC";
import { lc } from "../ext/client";
import { sentence } from "../ui/format";
import { Banner, Button, Card, Check, LoadError, Loading, Page, SelectField, TextAreaField, Toggle, explain, when } from "../ui/kit";
import "../laneC.css";

const GRIDS = [25, 50, 100, 200, 250, 500, 1000];
const MARKDOWN = <>Markdown: <code># Heading</code>, <code>## Smaller heading</code>, <code>- list items</code>, <code>**bold**</code>, <code>*italic*</code>, <code>`code`</code> and <code>[links](https://…)</code>. Blank lines make paragraphs.</>;

export function PublicPageSettingsPage() {
  const q = useQuery({ queryKey: ["public-settings"], queryFn: lc.publicSettings });
  const preview = useQuery({ queryKey: ["public-preview"], queryFn: lc.publicPreview });
  const [s, setS] = useState<PublicSettings | null>(null);
  const [dirty, setDirty] = useState(false);
  useEffect(() => {
    if (q.data && !s) setS(q.data.settings);
  }, [q.data, s]);
  const save = useMutation({
    mutationFn: (v: PublicSettings) => lc.savePublicSettings(v),
    onSuccess: () => { setDirty(false); void q.refetch(); void preview.refetch(); },
  });
  if (q.isError) return <Page title="Public page"><LoadError what="The public page's settings" error={q.error} retry={() => void q.refetch()} /></Page>;
  if (!s || !q.data) return <Page title="Public page"><Loading /></Page>;
  const set = (patch: Partial<PublicSettings>) => { setDirty(true); save.reset(); setS({ ...s, ...patch }); };
  const grid = s.mapDotsGrid ?? 100;
  const grids = GRIDS.includes(grid) ? GRIDS : [...GRIDS, grid].sort((a, b) => a - b);
  const dots = q.data.mapDots;
  const address = `${location.origin}/api/public/page`;
  return (
    <Page title="Public page" sub="A page anyone can open, without signing in. Off until you turn it on."
      actions={q.data.settings.enabled && <a className="k-btn" href="/api/public/page" target="_blank" rel="noreferrer">Open it</a>}>
      <form className="wa-tabpanel" noValidate onSubmit={(e) => { e.preventDefault(); save.mutate(s); }}>
        <Card title="What it shows">
          <div className="wa-form wa-form-wide">
            <Toggle label="Public page on" checked={s.enabled} onChange={(enabled) => set({ enabled })} hint={<>Anyone with the address can open it: <span className="k-mono wa-break">{address}</span></>} />
            <Toggle label="Online or offline" checked={s.showStatus} onChange={(showStatus) => set({ showStatus })} />
            <Toggle label="Player count" checked={s.showPlayerCount} onChange={(showPlayerCount) => set({ showPlayerCount })} />
            <Toggle label="Player names" checked={s.showPlayerNames} onChange={(showPlayerNames) => set({ showPlayerNames })} hint="The names of everyone online." />
            <Toggle label="Mod list" checked={s.showMods} onChange={(showMods) => set({ showMods })} hint="Names and versions." />
            <Toggle label="Map with player dots" checked={s.showMapDots} disabled={dots?.available === false} onChange={(showMapDots) => set({ showMapDots })}
              hint={dots?.available === false ? sentence((dots as { reason?: string }).reason ?? "Not available on this server.") : "A dot for each player, at the middle of the grid square they are in. No names."} />
            {s.showMapDots && (
              <>
                <div className="wa-narrow">
                  <SelectField label="Grid" value={String(grid)} onChange={(v) => set({ mapDotsGrid: Number(v) })}
                    options={grids.map((g) => ({ value: String(g), label: `${g} m squares` }))} hint="Larger squares say less about where someone is." />
                </div>
                <Banner tone="warn" title="Dots show roughly where players are, to anyone">
                  There are no names, but with one player online the dot is that player. Turn it off if your players would not want that.
                </Banner>
              </>
            )}
          </div>
        </Card>
        <Card title="Rules and how to join">
          <div className="wa-form wa-form-wide">
            <TextAreaField label="How to join" value={s.joinInfo} onChange={(joinInfo) => set({ joinInfo })} maxLength={500} rows={3}
              hint={<>{MARKDOWN} At most 500 characters ({500 - s.joinInfo.length} left).</>} />
            <TextAreaField label="Rules" value={s.rules} onChange={(rules) => set({ rules })} maxLength={4000} rows={6}
              hint={<>{MARKDOWN} At most 4,000 characters ({4000 - s.rules.length} left).</>} />
          </div>
        </Card>
        {q.data.widgets.length > 0 && (
          <Card title="Widgets from mods">
            <fieldset className="wa-checks">
              <legend className="k-sr-only">Widgets from mods</legend>
              {q.data.widgets.map((w) => (
                <Check key={w.key} label={<>{w.title} <span className="k-mono k-muted">{w.key}</span></>} checked={s.widgets.includes(w.key)}
                  onChange={(on) => set({ widgets: on ? [...s.widgets, w.key] : s.widgets.filter((x) => x !== w.key) })} />
              ))}
            </fieldset>
          </Card>
        )}
        <div className="wa-save-row">
          <Button kind="primary" type="submit" busy={save.isPending} disabled={!dirty}>Save</Button>
          {dirty && <Button kind="ghost" onClick={() => { setS(q.data!.settings); setDirty(false); save.reset(); }}>Discard changes</Button>}
        </div>
        <div aria-live="polite">
          {save.isSuccess && <Banner tone="ok" title={s.enabled ? "Saved. The public page shows the new settings within 15 seconds." : "Saved. The public page is off."} />}
          {save.isError && <Banner tone={isApiError(save.error) && save.error.code === "reauth_required" ? "warn" : "err"}
            title={isApiError(save.error) && save.error.code === "reauth_required" ? "Confirm it is you in the dialog, then press Save again. Nothing was saved." : `Not saved. ${explain(save.error)}`} />}
        </div>
      </form>
      <Card title="Preview" sub="Exactly the data visitors get, as saved." actions={<Button size="s" kind="ghost" busy={preview.isFetching} onClick={() => void preview.refetch()}>Refresh</Button>}>
        {preview.isError ? <LoadError what="The preview" error={preview.error} retry={() => void preview.refetch()} />
          : !preview.data ? <Loading />
          : <Preview data={preview.data} on={q.data.settings.enabled} />}
      </Card>
    </Page>
  );
}

function Preview({ data, on }: { data: Record<string, unknown>; on: boolean }) {
  const names = Array.isArray(data.playerNames) ? (data.playerNames as string[]) : null;
  const mods = Array.isArray(data.mods) ? data.mods : null;
  const dots = Array.isArray(data.dots) ? data.dots : null;
  return (
    <div className="wa-preview-page">
      {!on && <p className="wa-note">The page is off: visitors get "not found". Player data is gathered only while it is on.</p>}
      <dl className="wa-kv">
        <dt>Name</dt><dd>{String(data.name ?? "")}</dd>
        {"online" in data && <><dt>Status</dt><dd>{data.online ? "online" : "offline"}</dd></>}
        {"players" in data && <><dt>Players</dt><dd>{data.players === null || data.players === undefined ? "not known yet" : String(data.players)}</dd></>}
        {names && <><dt>Names</dt><dd className="wa-break">{names.length ? names.join(", ") : "nobody online"}</dd></>}
        {dots && <><dt>Map dots</dt><dd>{dots.length} on a {String(data.dotsGrid ?? "")} m grid</dd></>}
        {mods && <><dt>Mods</dt><dd>{mods.length} listed</dd></>}
        {"rules" in data && <><dt>Rules</dt><dd>shown</dd></>}
        {"joinInfo" in data && <><dt>How to join</dt><dd>shown</dd></>}
        {Array.isArray(data.widgets) && <><dt>Widgets</dt><dd>{data.widgets.length}</dd></>}
        {typeof data.at === "string" && <><dt>As of</dt><dd>{when(data.at)}</dd></>}
      </dl>
    </div>
  );
}
