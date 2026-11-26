// The setup wizard's "Add mods to your server" (0.1.3, optional): what is installed now, and one way in for a mod, a
// zip upload or a link. It is the Mods page's own pipeline, with every check it has: the server checks the zip (and its
// SHA-256; a link must come from an allowed source and needs its SHA-256), a minisign signature against your trusted
// keys, and a server that stages only signed packages refuses the rest. A checked mod is staged for the next start, as
// on the Mods page; "Restart now" only shows where the panel offers restarts. No catalog here: the Kiln Launcher and
// Nexus Mods are where mods are found.
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useId, useState } from "react";
import { ops } from "../app/ops";
import { isPartError, size, type PackagePlan } from "../api/ops";
import { problem } from "../pages/Mods";
import { RestartControls } from "../pages/OverviewCards";
import { Banner, Button, Check, Copyable, Pill, Tabs, TextField } from "../ui/kit";
import { Details, Frame } from "./Frame";
import type { Route } from "./wizard";

const NEXUS = "https://www.nexusmods.com/enshrouded/mods/";
export const STAGED_KEY = "wa-setup-mods";

/** The mods this wizard staged in this browser (the end page counts them). */
export function stagedHere(): string[] {
  try { return JSON.parse(sessionStorage.getItem(STAGED_KEY) ?? "[]") as string[]; } catch { return []; }
}

export function ModsStep({ route, back, next }: { route: Route | ""; back: () => void; next: () => void }) {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ["updates"], queryFn: ops.updates });
  // a server that stages only signed packages (Mods > Sources) says so before the button
  const sources = useQuery({ queryKey: ["updates", "sources"], queryFn: ops.sources });
  const [tab, setTab] = useState<"upload" | "url">("upload");
  const [file, setFile] = useState<File | null>(null);
  const [url, setUrl] = useState("");
  const [sha, setSha] = useState("");
  const [plan, setPlan] = useState<PackagePlan | null>(null);
  const [trust, setTrust] = useState(false);
  const [staged, setStaged] = useState<string[]>(stagedHere);
  const zipId = useId();
  const data = q.data;
  const folders = data?.folders ?? [];
  const limit = data?.limits.maxUploadBytes ?? 0;
  const tooBig = !!file && limit > 0 && file.size > limit;
  const shaOk = /^[0-9a-f]{64}$/i.test(sha.trim());
  const check = useMutation({
    mutationFn: async () => {
      if (tab === "url") return ops.fetchUrl(url.trim(), sha.trim().toLowerCase());
      return ops.upload({ name: file!.name, bytes: new Uint8Array(await file!.arrayBuffer()) });
    },
    onSuccess: (p) => { setPlan(p); setTrust(false); },
  });
  const stage = useMutation({
    mutationFn: (p: PackagePlan) => ops.stage(p.id, p.sha256),
    onSuccess: (_, p) => {
      const list = [...staged, `${p.name ?? p.modId ?? "Kiln"} ${p.version}`];
      sessionStorage.setItem(STAGED_KEY, JSON.stringify(list));
      setStaged(list);
      setPlan(null);
      setFile(null);
      setUrl("");
      setSha("");
      check.reset();
      void qc.invalidateQueries({ queryKey: ["updates"] });
    },
  });
  const signed = plan?.signature?.state === "verified";
  const needsSig = !!plan && !signed && !!sources.data?.settings.requireSignature;
  const supported = data?.restart?.supported === "supported";

  return (
    <Frame step="mods" route={route} title="Add mods to your server" back={back} skip={staged.length ? undefined : next}>
      <p className="wa-lead">
        {!data ? "Looking at your mods…" : folders.length === 0 ? "No mods installed yet." : `Installed: ${folders.length} mod${folders.length === 1 ? "" : "s"}.`}
      </p>
      {folders.length > 0 && (
        <ul className="wa-mod-list" aria-label="Installed mods">
          {folders.slice(0, 6).map((f) => <li key={f.folder}>{f.name ?? f.id ?? f.folder}</li>)}
          {folders.length > 6 && <li className="k-muted">and {folders.length - 6} more</li>}
        </ul>
      )}
      {q.isError && <Banner tone="err" title="The mod list didn't load.">{problem(q.error)}</Banner>}
      {data && isPartError(data.updates) && <Banner tone="warn" title="The server isn't answering about mods right now.">You can add mods later on the Mods page.</Banner>}
      {staged.length > 0 && (
        <Banner tone="ok" title={`${staged.join(", ")}: installs at the next restart.`}>
          <RestartControls supported={supported} compact />
        </Banner>
      )}
      {!plan ? (
        <form className="wa-form" noValidate onSubmit={(e) => { e.preventDefault(); check.mutate(); }}>
          <Tabs label="Add a mod" value={tab} onChange={(t) => { setTab(t); check.reset(); }} tabs={[{ id: "upload", label: "Upload a zip" }, { id: "url", label: "Paste a link" }]} />
          {tab === "upload" ? (
            <div className="k-field wa-file">
              <label className="k-label" htmlFor={zipId}>Mod zip</label>
              <input id={zipId} type="file" accept=".zip,application/zip" onChange={(e) => { setFile(e.target.files?.[0] ?? null); check.reset(); }} />
              {tooBig && <span className="k-error-text" role="alert">That file is larger than {size(limit)}.</span>}
            </div>
          ) : (
            <>
              <TextField label="Link" value={url} onChange={setUrl} mono inputMode="url" placeholder="https://…" />
              <TextField label="Its SHA-256" value={sha} onChange={setSha} mono maxLength={64} error={sha && !shaOk ? "64 letters and digits, from the mod's page." : undefined} />
            </>
          )}
          {check.isError && <Banner tone="err" title="Not added.">{problem(check.error)}</Banner>}
          <Button kind={staged.length ? "default" : "primary"} type="submit" busy={check.isPending} disabled={tab === "upload" ? !file || tooBig : !url.trim() || !shaOk}>Check it</Button>
        </form>
      ) : (
        <div className="wa-form">
          <p className="wa-plan-line"><strong>{plan.kind === "kiln" ? `Kiln ${plan.version}` : `${plan.name ?? plan.modId} ${plan.version}`}</strong>{" "}
            {signed ? <Pill tone="ok">signed by {plan.signature?.key}</Pill> : <Pill tone="warn">not signed</Pill>}</p>
          {needsSig && <Banner tone="warn" title="This server only installs signed mods.">Add its signature on the Mods page.</Banner>}
          {!signed && !needsSig && <Check label="I trust where this file came from" checked={trust} onChange={setTrust} />}
          {stage.isError && <Banner tone="err" title="Not installed.">{problem(stage.error)}</Banner>}
          <div className="wa-row">
            <Button kind="primary" busy={stage.isPending} disabled={needsSig || (!signed && !trust)} onClick={() => stage.mutate(plan)}>Install at the next restart</Button>
            <Button kind="ghost" onClick={() => { setPlan(null); void ops.removePlan(plan.id).catch(() => undefined); }}>Cancel</Button>
          </div>
          <Details>
            <span className="k-label">SHA-256</span>
            <Copyable text={plan.sha256} name="the SHA-256" />
            <p className="wa-note">{plan.current ? `Installed now: ${plan.current}.` : "Not installed yet."} {plan.files} files, {size(plan.bytes)}.</p>
            {plan.warnings.map((w) => <p key={w} className="wa-note">{w}</p>)}
          </Details>
        </div>
      )}
      {staged.length > 0 && <Button kind="primary" onClick={next}>Next</Button>}
      <p className="wa-fine">Find mods in the Kiln Launcher or on <a href={NEXUS} target="_blank" rel="noopener noreferrer">Nexus Mods</a>.</p>
    </Frame>
  );
}
