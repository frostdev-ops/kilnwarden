// Mods (DESIGN §5, §7): every mod folder with its on/off switch for the next start, what is loaded now, what waits for
// the next start (staged updates, rollbacks, the verdict on the last one), and installing or updating from a zip upload
// or an allowed URL, with an optional minisign signature. Nothing is replaced while the server runs: a checked package
// is staged through Kiln.Admin and applied at the next start, keeping the old version for a rollback. Staging, switches
// and sources are the owner's and ask for a recent proof; the SHA-256 is checked before anything is staged.
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useId, useState } from "react";
import { useSession } from "../app/context";
import { ops } from "../app/ops";
import { isApiError } from "../api/http";
import { isPartError, size, type ModFolder, type ModInfo, type PackagePlan, type TrustedKey, type UpdateSources, type UpdateUnit } from "../api/ops";
import { can } from "../state/session";
import { sentence } from "../ui/format";
import {
  Banner, Button, Card, Check, Copyable, Empty, ListField, LoadError, Loading, Page, Pill, Tabs, TextField, Toggle, explain, when, type Tone,
} from "../ui/kit";

const KEY = ["updates"];

/** A failed check or staging in plain words (also the setup wizard's "Add mods to your server"). */
export function problem(e: unknown): string {
  if (isApiError(e) && e.code === "reauth_required") return "Confirm your identity in the dialog, then try again. Nothing was changed.";
  if (isApiError(e) && e.code === "signature_required") return "Not staged: this server stages only packages a trusted key signed (Sources below). Get the package's .minisig from its publisher and check it again with the signature.";
  if (isApiError(e) && e.code === "bad_signature") return `Refused: ${sentence(e.message)} The package may have been altered, or the signature is for another file.`;
  if (isApiError(e) && e.code === "owner_only") return "Only an owner account may install, stage or switch mods.";
  return explain(e);
}

export function ModsPage() {
  const me = useSession((s) => s.me)!;
  const qc = useQueryClient();
  const q = useQuery({ queryKey: KEY, queryFn: ops.updates, refetchInterval: 20_000 });
  const [msg, setMsg] = useState<{ tone: Tone; text: string } | null>(null);
  const refresh = () => qc.invalidateQueries({ queryKey: KEY });
  const act = useMutation({
    mutationFn: ({ f }: { f: () => Promise<unknown>; done: string }) => f(),
    onSuccess: (_, v) => setMsg({ tone: "ok", text: v.done }),
    onError: (e) => setMsg({ tone: "err", text: problem(e) }),
    onSettled: refresh,
  });
  const owner = me.owner;
  const data = q.data;
  const mods: ModInfo[] = !data || isPartError(data.mods) ? [] : Array.isArray(data.mods) ? data.mods : data.mods.mods ?? [];
  const units: UpdateUnit[] = !data || isPartError(data.updates) ? [] : data.updates.units ?? [];
  const waiting = units.filter((u) => u.staged || u.rollback || u.open || (u.probation && u.probation.state !== "ok"));
  const shown = units.filter((u) => u.staged || u.rollback || u.open || u.probation || u.previous);
  return (
    <Page title="Mods" wide sub="Switch mods on or off, update them and roll them back. Changes apply at the next start; the old version is kept for a rollback.">
      {q.isError && <LoadError what="The mod list" error={q.error} retry={() => void q.refetch()} />}
      {q.isLoading && <Loading />}
      <div aria-live="polite">{msg && <Banner tone={msg.tone} title={msg.text} actions={<Button size="s" kind="ghost" onClick={() => setMsg(null)}>Close</Button>} />}</div>
      {waiting.length > 0 && (
        <Banner tone="warn" title={`${waiting.length} change${waiting.length > 1 ? "s" : ""} wait for the next start`}>
          {data?.restart?.supported === "supported" ? "Restart the server (Overview or Schedules) to apply them." : "Stop and start the server to apply them (restart support is unknown here)."}
        </Banner>
      )}
      {data && <Folders folders={data.folders} owner={owner} onDone={(m) => { setMsg(m); void refresh(); }} />}
      {data && (
        <Card title="Loaded now" sub="This session only: unloading or reloading lasts until the server stops.">
          {isPartError(data.mods) ? <Banner tone="err" title="The loaded mods did not load">{sentence(data.mods.error.message)}</Banner>
            : !mods.length ? <Empty title="No mods are loaded">Kiln loads the mods in kiln/mods at start.</Empty>
            : (
              <div className="wa-table-wrap">
                <table className="wa-table wa-table-compact">
                  <thead><tr><th>Mod</th><th>Version</th><th>State</th><th><span className="k-sr-only">Actions</span></th></tr></thead>
                  <tbody>{mods.map((m) => (
                    <tr key={m.id}>
                      <td data-label="Mod">{m.name ?? m.id} <span className="k-muted k-mono">{m.id}</span></td>
                      <td data-label="Version">{m.version ?? <span className="k-muted">unknown</span>}</td>
                      <td data-label="State">{m.state ? <Pill tone={/load/i.test(String(m.state)) && !/un/i.test(String(m.state)) ? "ok" : /fail|error|fault/i.test(String(m.state)) ? "err" : "idle"}>{String(m.state).toLowerCase()}</Pill> : null}</td>
                      <td className="wa-actions">{owner && <>
                        <Button size="s" kind="ghost" aria-label={`Reload ${m.id}`} onClick={() => act.mutate({ f: () => ops.modLifecycle("reload", m.id), done: `${m.id}: reloaded.` })}>Reload</Button>
                        <Button size="s" kind="ghost" aria-label={`Unload ${m.id}`} onClick={() => window.confirm(`Unload ${m.id} until the server stops?`) && act.mutate({ f: () => ops.modLifecycle("unload", m.id), done: `${m.id}: unloaded until the server stops.` })}>Unload</Button>
                      </>}</td>
                    </tr>
                  ))}</tbody>
                </table>
              </div>
            )}
          {owner && <LoadFolder onLoad={(folder) => act.mutate({ f: () => ops.modLifecycle("load", folder), done: `${folder}: loaded.` })} />}
        </Card>
      )}
      {data && (
        <Card title="Next start" sub="Staged updates, requested rollbacks and the result of the last change, per mod and for Kiln.">
          {isPartError(data.updates) ? <Banner tone="err" title="The update list did not load">{sentence(data.updates.error.message)}</Banner>
            : !shown.length ? <Empty title="Nothing waits for the next start">Check a package below to stage an update.</Empty>
            : (
              <div className="wa-table-wrap">
                <table className="wa-table">
                  <thead><tr><th>Unit</th><th>Installed</th><th>At the next start</th><th>Last change</th><th><span className="k-sr-only">Actions</span></th></tr></thead>
                  <tbody>{shown.map((u) => (
                    <tr key={u.unit}>
                      <td data-label="Unit" className="k-mono">{u.unit}</td>
                      <td data-label="Installed">{u.current?.version ?? <span className="k-muted">not installed</span>}</td>
                      <td data-label="At the next start">
                        {u.staged && <div><Pill tone="info">update to {u.staged.version}</Pill> <span className="k-muted">by {u.staged.by}, {when(u.staged.staged)}</span></div>}
                        {u.rollback && <div><Pill tone="warn">rollback{u.previous ? ` to ${u.previous.version || "not installed"}` : ""}</Pill> <span className="k-muted">by {u.rollback.by}</span></div>}
                        {u.open && <div><Pill tone="warn">a change is still open</Pill></div>}
                        {!u.staged && !u.rollback && !u.open && <span className="k-muted">nothing</span>}
                      </td>
                      <td data-label="Last change">
                        {u.probation && <Pill tone={u.probation.state === "failed" ? "err" : "idle"} title={u.probation.error ?? undefined}>{u.probation.state} {u.probation.version}</Pill>}
                        {u.probation?.error && <div className="k-muted wa-break">{u.probation.error}</div>}
                        {u.previous && <div className="k-muted">kept {u.previous.version || "(none)"} ({u.previous.reason})</div>}
                      </td>
                      <td className="wa-actions">{owner && <>
                        {(u.staged || u.rollback) && <Button size="s" kind="ghost" aria-label={`Cancel ${u.unit}`} onClick={() => act.mutate({ f: () => ops.cancelUnit(u.unit), done: `${u.unit}: canceled.` })}>Cancel</Button>}
                        {u.previous && !u.staged && !u.rollback && <Button size="s" kind="danger" aria-label={`Roll ${u.unit} back`} onClick={() => window.confirm(`Roll ${u.unit} back to ${u.previous!.version || "not installed"} at the next start?`) && act.mutate({ f: () => ops.rollbackUnit(u.unit), done: `${u.unit}: rollback requested for the next start.` })}>Roll back</Button>}
                      </>}</td>
                    </tr>
                  ))}</tbody>
                </table>
              </div>
            )}
        </Card>
      )}
      {can(me, "updates.prepare") && owner && data && <Install limits={data.limits} origins={data.allowedOrigins} onPlan={refresh} />}
      {(data?.plans ?? []).map((p) => <PlanCard key={p.id} plan={p} owner={owner} onDone={(t) => { setMsg(t); void refresh(); }} />)}
      {owner && <Sources />}
    </Page>
  );
}

function Folders({ folders, owner, onDone }: { folders: ModFolder[] | undefined; owner: boolean; onDone: (m: { tone: Tone; text: string }) => void }) {
  const [now, setNow] = useState(false);
  const flip = useMutation({
    mutationFn: ({ f, on }: { f: ModFolder; on: boolean }) => ops.switchMod(f.folder, on, now),
    onSuccess: (r, { f, on }) => {
      const label = f.name ?? f.id ?? f.folder;
      const base = !r.changed ? `${label} was already ${on ? "on" : "off"}.` : on ? `${label} is on: it loads at the next start.` : `${label} is off: it is skipped at the next start.`;
      const live = r.now === null ? "" : r.now.ok ? (on ? " Loaded now too." : " Unloaded now too.") : ` It could not be ${on ? "loaded" : "unloaded"} now (the server answered ${r.now.status ?? "no"}); it changes at the next start.`;
      onDone({ tone: r.now && !r.now.ok ? "warn" : "ok", text: base + live });
    },
    onError: (e) => onDone({ tone: "err", text: problem(e) }),
  });
  if (!folders) return null;
  return (
    <Card title="Mod folders" sub="Off: skipped at the next start (a .disabled file in its folder). Nothing is deleted.">
      {!folders.length ? <Empty title="No mod folders">Mods go in kiln/mods on the server, one folder each. Install one below.</Empty> : (
        <div className="wa-form wa-form-wide">
          {owner && <Check label="Also load or unload it now, in this session" checked={now} onChange={setNow} hint="(otherwise only at the next start)" />}
          <div>
            {folders.map((f) => (
              <div key={f.folder} className="wa-folder-row">
                <div className="wa-folder-name">
                  <div>{f.name ?? f.id ?? f.folder} {f.version && <span className="k-muted">{f.version}</span>}</div>
                  <div className="k-muted k-mono">kiln/mods/{f.folder}</div>
                </div>
                {f.locked ? (
                  <span className="wa-row"><Pill tone="ok">on</Pill><span className="k-muted">Always on: {f.locked}.</span></span>
                ) : owner ? (
                  <Toggle label={f.disabled ? "Off" : "On"} checked={!f.disabled} disabled={flip.isPending} ariaLabel={`${f.name ?? f.id ?? f.folder} at the next start`}
                    onChange={(on) => flip.mutate({ f, on })} />
                ) : <Pill tone={f.disabled ? "idle" : "ok"}>{f.disabled ? "off" : "on"}</Pill>}
              </div>
            ))}
          </div>
        </div>
      )}
    </Card>
  );
}

function LoadFolder({ onLoad }: { onLoad: (folder: string) => void }) {
  const [folder, setFolder] = useState("");
  const ok = /^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$/.test(folder);
  return (
    <form className="wa-row wa-end wa-gap" onSubmit={(e) => { e.preventDefault(); if (ok) onLoad(folder); }}>
      <TextField label="Load a mod folder now (kiln/mods/…)" value={folder} onChange={setFolder} mono />
      <Button type="submit" disabled={!ok}>Load</Button>
    </form>
  );
}

/** A .minisig file's text (or null), at most 4 KiB. */
async function readSignature(f: File | null): Promise<string | undefined> {
  if (!f) return undefined;
  if (f.size > 4096) throw new Error("That signature file is larger than 4 KiB: choose the package's .minisig file.");
  return f.text();
}

function Install({ limits, origins, onPlan }: { limits: { maxUploadBytes: number }; origins: string[]; onPlan: () => void }) {
  const [tab, setTab] = useState<"upload" | "url">("upload");
  const [file, setFile] = useState<File | null>(null);
  const [sig, setSig] = useState<File | null>(null);
  const [url, setUrl] = useState("");
  const [sigUrl, setSigUrl] = useState("");
  const [sha, setSha] = useState("");
  const [expectId, setExpectId] = useState("");
  const [progress, setProgress] = useState<number | null>(null);
  const zipId = useId();
  const sigId = useId();
  const go = useMutation({
    mutationFn: async () => {
      const signature = await readSignature(sig);
      if (tab === "url") return ops.fetchUrl(url.trim(), sha.trim().toLowerCase(), expectId.trim(), { signature, signatureUrl: signature ? undefined : sigUrl.trim() || undefined });
      const bytes = new Uint8Array(await file!.arrayBuffer());
      return ops.upload({ name: file!.name, bytes }, { sha256: sha.trim().toLowerCase() || undefined, expectId: expectId.trim(), signature, onProgress: setProgress });
    },
    onSettled: () => { setProgress(null); onPlan(); },
  });
  const shaOk = /^[0-9a-f]{64}$/i.test(sha.trim());
  const tooBig = !!file && file.size > limits.maxUploadBytes;
  return (
    <Card title="Install or update" sub="A zip of a mod (kiln.mod.json at its top or in one folder) or a Kiln runtime bundle. It is checked first; nothing is staged yet.">
      <Tabs label="Source" value={tab} onChange={setTab} tabs={[{ id: "upload", label: "Upload a zip" }, { id: "url", label: "From a URL" }]} />
      <form className="wa-form" noValidate onSubmit={(e) => { e.preventDefault(); go.mutate(); }}>
        {tab === "upload" ? (
          <div className="k-field wa-file">
            <label className="k-label" htmlFor={zipId}>Zip file (at most {size(limits.maxUploadBytes)})</label>
            <input id={zipId} type="file" accept=".zip,application/zip" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
            {tooBig && <span className="k-error-text" role="alert">That file is larger than {size(limits.maxUploadBytes)}.</span>}
          </div>
        ) : (
          <TextField label="URL (https)" value={url} onChange={setUrl} mono inputMode="url" hint={origins.length ? `Allowed: ${origins.join(", ")}.` : "No source is allowed yet: add one under Sources."} />
        )}
        <TextField label={tab === "url" ? "SHA-256 (required)" : "SHA-256 from the publisher (recommended)"} value={sha} onChange={setSha} mono maxLength={64}
          error={sha && !shaOk ? "64 hex digits." : undefined} />
        <div className="k-field wa-file">
          <label className="k-label" htmlFor={sigId}>Signature (.minisig, optional)</label>
          <input id={sigId} type="file" accept=".minisig,text/plain" onChange={(e) => setSig(e.target.files?.[0] ?? null)} />
          <span className="k-hint">The publisher's minisign signature of this zip. Checked against your trusted keys (Sources); a signature that does not match is refused.</span>
        </div>
        {tab === "url" && !sig && <TextField label="Signature URL (optional)" value={sigUrl} onChange={setSigUrl} mono inputMode="url" hint="Where the .minisig file is, on an allowed source." />}
        <TextField label="Expected mod id (optional)" value={expectId} onChange={setExpectId} mono />
        <div className="wa-row">
          <Button kind="primary" type="submit" busy={go.isPending} disabled={tab === "upload" ? !file || tooBig || (!!sha && !shaOk) : !url || !shaOk}>Check package</Button>
        </div>
        {progress !== null && <progress max={1} value={progress} aria-label="Upload progress" />}
        <div aria-live="polite">
          {go.isError && <Banner tone="err" title={problem(go.error)} />}
          {go.isSuccess && <Banner tone="ok" title="Checked. Review it below, then stage it." />}
        </div>
      </form>
    </Card>
  );
}

function SignatureLine({ plan }: { plan: PackagePlan }) {
  const s = plan.signature;
  if (!s) return null;
  switch (s.state) {
    case "verified": return <div className="wa-signature"><Pill tone="ok">signed</Pill><span>by your trusted key <strong>{s.key}</strong>{s.trustedComment && <span className="k-muted"> · {s.trustedComment}</span>}</span></div>;
    case "unsigned": return <div className="wa-signature"><Pill tone="warn">not signed</Pill><span className="k-muted">Compare the SHA-256 with the publisher's before you stage it.</span></div>;
    case "unknown_key": return <div className="wa-signature"><Pill tone="warn">signed by an unknown key</Pill><span className="k-muted">Key id <span className="k-mono">{s.keyId}</span> is not one of your trusted keys (Sources).</span></div>;
    case "invalid": return <div className="wa-signature"><Pill tone="err">bad signature</Pill><span>The signature does not match this package.</span></div>;
  }
}

function PlanCard({ plan, owner, onDone }: { plan: PackagePlan; owner: boolean; onDone: (m: { tone: Tone; text: string }) => void }) {
  const [label, setLabel] = useState("");
  const [checked, setChecked] = useState(false);
  const sources = useQuery({ queryKey: [...KEY, "sources"], queryFn: ops.sources, enabled: owner });
  const needsSig = !!sources.data?.settings.requireSignature && plan.signature?.state !== "verified";
  const stage = useMutation({
    mutationFn: () => ops.stage(plan.id, plan.sha256, label || undefined),
    onSuccess: (r) => onDone({ tone: "ok", text: `${r.unit}: staged for the next start.` }),
  });
  const remove = useMutation({ mutationFn: () => ops.removePlan(plan.id), onSuccess: () => onDone({ tone: "ok", text: "The checked package was removed." }) });
  const title = plan.kind === "kiln" ? `Kiln ${plan.version}` : `${plan.name ?? plan.modId} ${plan.version}`;
  const warnings = plan.warnings.filter((w) => !/^(not signed|signed by key)/.test(w));
  return (
    <Card title={<>Checked: {title}</>} sub={`${plan.current ? `Installed: ${plan.current}` : "Not installed yet"} · ${plan.files} files, ${size(plan.bytes)} · the check expires ${when(plan.expiresAt)}`}>
      <div className="wa-form">
        <SignatureLine plan={plan} />
        <div className="wa-stack">
          <span className="k-label">SHA-256</span><Copyable text={plan.sha256} name="the SHA-256" />
          <span className="k-muted wa-break">From {plan.source.type === "url" ? plan.source.url : `upload ${plan.source.name}`}{plan.folder ? ` · folder kiln/mods/${plan.folder}` : ""}</span>
        </div>
        {warnings.map((w) => <Banner key={w} tone="warn" title={sentence(w)} />)}
        {needsSig && <Banner tone="warn" title="This server stages signed packages only">A trusted key must sign this package (Sources below). Check it again with its .minisig file.</Banner>}
        {owner && <>
          <TextField label="Label (optional, kept in Kiln's records)" value={label} onChange={setLabel} maxLength={120} />
          <Check label="I compared the SHA-256 with the publisher's" checked={checked} onChange={setChecked} />
          <div className="wa-row">
            <Button kind="danger" busy={stage.isPending} disabled={!checked || needsSig} onClick={() => stage.mutate()}>Stage for the next start</Button>
            <Button kind="ghost" busy={remove.isPending} onClick={() => remove.mutate()}>Remove</Button>
          </div>
        </>}
        <div aria-live="polite">
          {stage.isError && <Banner tone="err" title={problem(stage.error)} />}
          {remove.isError && <Banner tone="err" title={problem(remove.error)} />}
        </div>
      </div>
    </Card>
  );
}

function Sources() {
  const q = useQuery({ queryKey: [...KEY, "sources"], queryFn: ops.sources });
  const qc = useQueryClient();
  const [draft, setDraft] = useState<UpdateSources | null>(null);
  const [saved, setSaved] = useState(false);
  const save = useMutation({
    mutationFn: (s: UpdateSources) => ops.saveSources(s),
    onSuccess: () => { setDraft(null); setSaved(true); void qc.invalidateQueries({ queryKey: [...KEY] }); },
  });
  if (q.isError) return <Card title="Sources and signatures"><LoadError what="The sources" error={q.error} retry={() => void q.refetch()} /></Card>;
  if (!q.data) return null;
  const s = draft ?? q.data.settings;
  const keys = s.trustedKeys ?? [];
  const set = (patch: Partial<UpdateSources>) => { setSaved(false); setDraft({ ...s, ...patch }); };
  const setKey = (i: number, k: Partial<TrustedKey>) => set({ trustedKeys: keys.map((x, j) => (j === i ? { ...x, ...k } : x)) });
  return (
    <Card title="Sources and signatures" sub="Where packages may come from, and the publishers whose signatures you trust.">
      <form className="wa-form wa-form-wide" noValidate onSubmit={(e) => { e.preventDefault(); save.mutate(s); }}>
        {q.data.problems.length > 0 && <Banner tone="warn" title="updates.json has problems: the panel uses the last good settings"><ul className="wa-problems">{q.data.problems.map((p) => <li key={p}>{sentence(p)}</li>)}</ul></Banner>}
        <ListField label="Allowed origins" value={s.allowedOrigins} onChange={(allowedOrigins) => set({ allowedOrigins })}
          hint="The only origins a package URL (and each redirect) may use: https://host or https://host:port, one per line." rows={4} />
        <fieldset className="wa-fieldset">
          <legend>Trusted publisher keys</legend>
          <p className="wa-note">minisign public keys: the line that starts with RW in the publisher's .pub file. A package signed by one of these shows as signed.</p>
          {!keys.length && <p className="k-muted wa-p">No trusted keys yet.</p>}
          {keys.map((k, i) => (
            <div key={i} className="wa-key-row">
              <TextField label="Name" value={k.name} onChange={(name) => setKey(i, { name })} maxLength={64} />
              <TextField label="Public key" value={k.publicKey} onChange={(publicKey) => setKey(i, { publicKey: publicKey.trim() })} mono placeholder="RW…" />
              <Button kind="ghost" aria-label={`Remove the key ${k.name || i + 1}`} onClick={() => set({ trustedKeys: keys.filter((_, j) => j !== i) })}>Remove</Button>
            </div>
          ))}
          <div className="wa-row"><Button size="s" onClick={() => set({ trustedKeys: [...keys, { name: "", publicKey: "" }] })} disabled={keys.length >= 16}>Add a key</Button></div>
        </fieldset>
        <Toggle label="Stage only signed packages" checked={!!s.requireSignature} onChange={(requireSignature) => set({ requireSignature })}
          hint="A trusted key must have signed a package before it can be staged. Off: unsigned packages can be staged after you compare their SHA-256. A signature that does not match is always refused." />
        <div className="wa-save-row">
          <Button kind="primary" type="submit" disabled={!draft} busy={save.isPending}>Save sources</Button>
          {draft && <Button kind="ghost" onClick={() => { setDraft(null); save.reset(); }}>Discard changes</Button>}
        </div>
        <div aria-live="polite">
          {save.isError && <Banner tone="err" title={isApiError(save.error) && save.error.status === 422 ? "Not saved: fix these first." : problem(save.error)}>
            {isApiError(save.error) && save.error.status === 422 && <ul className="wa-problems">{save.error.message.split("; ").map((p) => <li key={p}>{sentence(p)}</li>)}</ul>}
          </Banner>}
          {saved && <Banner tone="ok" title="Sources saved." />}
        </div>
      </form>
    </Card>
  );
}
