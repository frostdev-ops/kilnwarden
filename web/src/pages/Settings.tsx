// Panel settings (DESIGN §2, §4, §5): the panel's address and TLS (settings.json), its certificate (Let's Encrypt),
// sign-in (Steam, Discord, sessions), hosted access (the frostdev.io relay), branding, the modules' switches and the map
// art. Each part saves through its own validation; saving asks for a recent proof (the shell's dialog) and nothing is
// retried on its own. Users with core.settings.view but not core.settings.edit see everything read-only.
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useId, useMemo, useState, type ReactNode } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { ep, session, useSession } from "../app/context";
import { panel } from "../app/panel";
import { isApiError } from "../api/http";
import {
  ALWAYS_ON, TLS_ENGINES, TLS_MODES, enumName, enumValue, imageType, isClaimUrl, isLoopback, shortFingerprint, type AcmeChallenge, type ModuleSwitch,
  type RelayClaimLink, type RelayStatus, type RoleMapping, type ServerSettings, type SettingsFile, type SettingsFileId, type SettingsFiles, type TlsEngine,
  type TlsMode,
} from "../api/panel";
import { can } from "../state/session";
import { ago, sentence } from "../ui/format";
import {
  Banner, Button, Card, Copyable, Empty, ListField, LoadError, Loading, NumberField, Page, Pill, SelectField, TabPanel, Tabs, TextField, Toggle,
  explain, when, type Tone,
} from "../ui/kit";

type TabId = "address" | "certificate" | "signin" | "relay" | "branding" | "modules" | "map";
const TABS: { id: TabId; label: string }[] = [
  { id: "address", label: "Address and TLS" }, { id: "certificate", label: "Certificate" }, { id: "signin", label: "Sign-in" },
  { id: "relay", label: "Hosted access" }, { id: "branding", label: "Branding" }, { id: "modules", label: "Modules" }, { id: "map", label: "Map art" },
];
const FILES = ["settings-files"];

/** A save's outcome, said under its form. */
interface Outcome { tone: Tone; title: string; items?: string[]; extra?: ReactNode }

/** A refused save as an outcome: the dialog for a recent proof, the file's problems as a list, or the message. */
function refused(e: unknown): Outcome {
  if (isApiError(e) && e.code === "reauth_required") return { tone: "warn", title: "Confirm it is you in the dialog, then press Save again. Nothing was saved." };
  if (isApiError(e) && e.status === 422) return { tone: "err", title: "Not saved: fix these first.", items: e.message.split("; ").map(sentence) };
  if (isApiError(e) && e.status === 403) return { tone: "err", title: "Your account may not change this." };
  return { tone: "err", title: `Not saved. ${explain(e)}` };
}

function OutcomeBanner({ o }: { o: Outcome | null }) {
  return (
    <div aria-live="polite">
      {o && (
        <Banner tone={o.tone} title={o.title}>
          {o.items && o.items.length > 0 && <ul className="wa-problems">{o.items.map((x) => <li key={x}>{x}</li>)}</ul>}
          {o.extra}
        </Banner>
      )}
    </div>
  );
}

export function SettingsPage() {
  const me = useSession((s) => s.me)!;
  const loc = useLocation();
  const nav = useNavigate();
  const fromHash = TABS.find((t) => `#${t.id}` === loc.hash)?.id;
  const [tab, setTab] = useState<TabId>(fromHash ?? "address");
  useEffect(() => {
    if (fromHash && fromHash !== tab) setTab(fromHash);
  }, [fromHash]); // eslint-disable-line react-hooks/exhaustive-deps
  if (!can(me, "core.settings.view")) {
    return (
      <Page title="Panel settings">
        <Banner tone="warn" title="Your account may not see the panel's settings">Ask the server's owner if you need them.</Banner>
      </Page>
    );
  }
  const edit = can(me, "core.settings.edit");
  const pick = (t: TabId) => {
    setTab(t);
    nav({ hash: t }, { replace: true });
  };
  return (
    <Page title="Panel settings" wide sub="Where the panel listens, its certificate, sign-in, hosted access, its look and its modules.">
      {!edit && <Banner tone="info" title="Read only">You can see these settings. Only the owner can change them.</Banner>}
      <Tabs label="Settings sections" idBase="wa-settings" tabs={TABS} value={tab} onChange={pick} />
      <TabPanel idBase="wa-settings" id={tab}>
        {tab === "address" && <AddressTab edit={edit} />}
        {tab === "certificate" && <CertificateTab edit={edit} />}
        {tab === "signin" && <SignInTab edit={edit} />}
        {tab === "relay" && <RelayTab edit={edit} />}
        {tab === "branding" && <BrandingTab edit={edit} />}
        {tab === "modules" && <ModulesTab />}
        {tab === "map" && <MapArtTab />}
      </TabPanel>
    </Page>
  );
}

// ------------------------------------------------------------------------------------------------ settings files

/** One settings file as a form: its value from GET /api/core/settings/files, a draft, and a save that never retries. */
function useSettingsFile<K extends SettingsFileId>(id: K) {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: FILES, queryFn: panel.settingsFiles });
  const file = q.data?.files.find((f) => f.id === id) as SettingsFile<K> | undefined;
  const [draft, setDraft] = useState<SettingsFiles[K] | null>(null);
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  const value = draft ?? file?.value ?? null;
  const save = useMutation({
    mutationFn: (v: SettingsFiles[K]) => panel.saveSettingsFile(id, v),
    onSuccess: () => {
      setDraft(null);
      setOutcome({ tone: "ok", title: "Saved." });
      void qc.invalidateQueries({ queryKey: FILES });
    },
    onError: (e) => setOutcome(refused(e)),
  });
  const set = (patch: Partial<SettingsFiles[K]>) => {
    if (!value) return;
    setOutcome(null);
    setDraft({ ...value, ...patch });
  };
  return { q, file, value, set, dirty: draft !== null, save: () => value && save.mutate(value), saving: save.isPending, outcome, discard: () => { setDraft(null); setOutcome(null); } };
}

type FileForm<K extends SettingsFileId> = ReturnType<typeof useSettingsFile<K>>;

/** The file's own problems (settings the server refused when it read the file), the form, and its save row. */
function FileCard<K extends SettingsFileId>({ form, title, sub, edit, children, saveLabel = "Save" }: {
  form: FileForm<K>; title: string; sub?: ReactNode; edit: boolean; children: (v: SettingsFiles[K]) => ReactNode; saveLabel?: string;
}) {
  return (
    <Card title={title} sub={sub}>
      {form.q.isError ? <LoadError what="The settings" error={form.q.error} retry={() => void form.q.refetch()} />
        : !form.value ? <Loading />
        : (
          <form className="wa-form wa-form-wide" noValidate onSubmit={(e) => { e.preventDefault(); form.save(); }}>
            {form.file && form.file.problems.length > 0 && (
              <Banner tone="warn" title={`${form.file.file} has problems: the panel uses the last good settings`}>
                <ul className="wa-problems">{form.file.problems.map((p) => <li key={p}>{sentence(p)}</li>)}</ul>
              </Banner>
            )}
            <fieldset className="wa-plain" disabled={!edit}>
              <div className="wa-sections">{children(form.value)}</div>
            </fieldset>
            {edit && (
              <div className="wa-save-row">
                <Button kind="primary" type="submit" busy={form.saving} disabled={!form.dirty}>{saveLabel}</Button>
                {form.dirty && <Button kind="ghost" onClick={form.discard}>Discard changes</Button>}
                {form.file && <span className="k-muted">Saved to {form.file.file} in the panel's data folder.</span>}
              </div>
            )}
            <OutcomeBanner o={form.outcome} />
          </form>
        )}
    </Card>
  );
}

// ------------------------------------------------------------------------------------------------ address and TLS

const BINDS = [
  { value: "127.0.0.1", label: "This machine only (127.0.0.1)" },
  { value: "0.0.0.0", label: "Every IPv4 address (0.0.0.0)" },
  { value: "::", label: "Every address, IPv4 and IPv6 (::)" },
  { value: "custom", label: "One address of this machine" },
];
const MODE_LABELS: Record<TlsMode, string> = {
  "self-signed": "Self-signed certificate", file: "My own certificate files", acme: "Let's Encrypt", off: "Off (plain HTTP)",
};
const MODE_HINTS: Record<TlsMode, string> = {
  "self-signed": "Made on first start and kept in the data folder. Browsers warn until you trust it; its fingerprint is in kiln.log and on the Overview.",
  file: "Your own PEM files (the full chain and the key), reloaded when they change.",
  acme: "A certificate from Let's Encrypt, renewed by the panel. Set it up under Certificate; until it arrives the panel uses a self-signed one.",
  off: "No encryption. Only for this machine, or behind a reverse proxy that does TLS.",
};
const ENGINE_LABELS: Record<TlsEngine, string> = { auto: "Automatic", kwtls: "kwtls (rustls)", "ssl-stream": "The system's (SslStream)" };
const ENGINE_HINT = "Automatic: kwtls (ECDSA certificates) when its library loads, else the system's (RSA and TLS 1.2 under Wine).";
const LIMITS: [string, string, number, number, string?][] = [
  ["maxConnections", "Connections at once", 1, 100000], ["maxConnectionsPerIp", "Connections per address", 1, 100000],
  ["connectionsPerSecondPerIp", "New connections per second per address", 0.1, 10000], ["requestsPerSecondPerIp", "Requests per second per address", 0.1, 100000],
  ["requestBurstPerIp", "Request burst per address", 1, 100000], ["maxHeaderBytes", "Header size", 1024, 1048576, "bytes"],
  ["maxHeaderCount", "Headers per request", 8, 1000], ["maxTargetBytes", "Address length", 256, 65536, "bytes"],
  ["maxBodyBytes", "Request body", 0, 67108864, "bytes"], ["maxWebSocketMessageBytes", "WebSocket message", 1024, 67108864, "bytes"],
  ["handshakeTimeoutMs", "TLS handshake timeout", 100, 600000, "ms"], ["headerTimeoutMs", "Header timeout", 100, 600000, "ms"],
  ["keepAliveTimeoutMs", "Idle connection timeout", 100, 600000, "ms"], ["bodyTimeoutMs", "Body timeout", 100, 600000, "ms"],
  ["writeTimeoutMs", "Write timeout", 100, 600000, "ms"], ["maxRequestsPerConnection", "Requests per connection", 1, 10000000],
];

/** Where the panel is reachable after a change of bind, port or TLS (best guess from this page's address). */
function newAddress(s: ServerSettings, mode: TlsMode): string {
  const proto = mode === "off" ? "http" : "https";
  const wild = s.bind === "0.0.0.0" || s.bind === "::";
  const host = wild || isLoopback(s.bind) ? location.hostname : s.bind.includes(":") ? `[${s.bind}]` : s.bind;
  const port = s.port || Number(location.port) || (proto === "https" ? 443 : 80);
  return `${proto}://${host}:${port}/`;
}

function AddressTab({ edit }: { edit: boolean }) {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ["core-settings"], queryFn: panel.settings });
  const [draft, setDraft] = useState<ServerSettings | null>(null);
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  const [custom, setCustom] = useState(false);
  const orig = q.data?.settings;
  const s = draft ?? orig ?? null;
  const save = useMutation({
    mutationFn: (next: ServerSettings) => panel.saveSettings(next),
    onSuccess: (_, next) => {
      const moved = orig && (orig.bind !== next.bind || orig.port !== next.port || enumName(TLS_MODES, orig.tls.mode, "self-signed") !== enumName(TLS_MODES, next.tls.mode, "self-signed"));
      const url = newAddress(next, enumName(TLS_MODES, next.tls.mode, "self-signed"));
      setDraft(null);
      setOutcome(moved
        ? { tone: "warn", title: "Saved. The panel's listener restarts at its new address.", extra: <p className="wa-p">If this page stops answering, open <a href={url}>{url}</a> (sign in again there).</p> }
        : { tone: "ok", title: "Saved. Limits, hosts and the allowlist apply to the next request." });
      void qc.invalidateQueries({ queryKey: ["core-settings"] });
      void qc.invalidateQueries({ queryKey: ["panel-info"] });
    },
    onError: (e) => setOutcome(refused(e)),
  });
  if (q.isError) return <LoadError what="The panel's address settings" error={q.error} retry={() => void q.refetch()} />;
  if (!s || !orig) return <Card title="Address"><Loading /></Card>;
  const mode = enumName(TLS_MODES, s.tls.mode, "self-signed");
  const engine = enumName(TLS_ENGINES, s.tls.engine, "auto");
  const set = (patch: Partial<ServerSettings>) => { setOutcome(null); setDraft({ ...s, ...patch }); };
  const setTls = (patch: Partial<ServerSettings["tls"]>) => set({ tls: { ...s.tls, ...patch } });
  const bindChoice = custom || !BINDS.some((b) => b.value === s.bind) ? "custom" : s.bind;
  const isPublic = !isLoopback(s.bind);
  const plainPublic = isPublic && mode === "off" && !s.reverseProxy.enabled;
  const listenerChange = s.bind !== orig.bind || s.port !== orig.port || mode !== enumName(TLS_MODES, orig.tls.mode, "self-signed") ||
    engine !== enumName(TLS_ENGINES, orig.tls.engine, "auto") || s.tls.hostnames.join() !== orig.tls.hostnames.join() || s.tls.certFile !== orig.tls.certFile || s.tls.keyFile !== orig.tls.keyFile;
  return (
    <form className="wa-tabpanel" noValidate onSubmit={(e) => {
      e.preventDefault();
      save.mutate({ ...s, tls: { ...s.tls, mode: enumValue(TLS_MODES, mode, orig.tls.mode), engine: enumValue(TLS_ENGINES, engine, orig.tls.engine) } });
    }}>
      {q.data!.problems.length > 0 && (
        <Banner tone="warn" title="settings.json has problems: the panel uses the last good settings">
          <ul className="wa-problems">{q.data!.problems.map((p) => <li key={p}>{sentence(p)}</li>)}</ul>
        </Banner>
      )}
      <fieldset className="wa-plain wa-tabpanel" disabled={!edit}>
        <Card title="Address" sub="Where browsers reach the panel.">
          <div className="wa-form wa-form-wide">
            <div className="wa-form-grid">
              <SelectField label="Listen on" value={bindChoice} options={BINDS}
                onChange={(v) => { if (v === "custom") { setCustom(true); } else { setCustom(false); set({ bind: v }); } }} />
              {bindChoice === "custom" && <TextField label="Address" value={s.bind} onChange={(v) => set({ bind: v.trim() })} mono hint="An IP address of this machine, such as 192.168.1.20." />}
              <NumberField label="Port" value={s.port} min={0} max={65535} onChange={(v) => set({ port: v })} hint="0: the game's query port + 100 (15737 with the default 15637)." />
            </div>
            {isPublic && !plainPublic && (
              <Banner tone="warn" title="Reachable from other machines">
                Anyone who can reach this address sees the sign-in page. Keep TLS on, use long passwords and two-factor sign-in, and limit who may connect below if you can.
                A public address takes effect once the owner account exists.
              </Banner>
            )}
            {plainPublic && (
              <Banner tone="err" title="TLS is off on a public address">
                Passwords and sign-in cookies would cross the network unencrypted, so the panel refuses this and keeps listening on 127.0.0.1 only.
                Turn TLS on, or switch on "I have a reverse proxy" if one in front of the panel does TLS.
              </Banner>
            )}
            <ListField label="Host names the panel answers to" value={s.allowedHosts} onChange={(v) => set({ allowedHosts: v })}
              hint="One per line. Protects against DNS rebinding. Empty: localhost, IP addresses and the TLS host names. A public address needs its DNS names here." />
            <ListField label="Who may connect" value={s.ipAllowlist} onChange={(v) => set({ ipAllowlist: v })}
              hint="Addresses or ranges such as 192.168.1.0/24, one per line. Empty: everyone (within the limits). This machine is always allowed." />
          </div>
        </Card>
        <Card title="TLS" sub="How the connection is encrypted.">
          <div className="wa-form wa-form-wide">
            <div className="wa-form-grid">
              <SelectField label="Certificate" value={mode} onChange={(v) => setTls({ mode: v })} options={TLS_MODES.map((m) => ({ value: m, label: MODE_LABELS[m] }))} hint={MODE_HINTS[mode]} />
              <SelectField label="TLS engine" value={engine} onChange={(v) => setTls({ engine: v })} options={TLS_ENGINES.map((m) => ({ value: m, label: ENGINE_LABELS[m] }))} hint={ENGINE_HINT} />
            </div>
            {mode === "file" && (
              <div className="wa-form-grid">
                <TextField label="Certificate file (full chain, PEM)" value={s.tls.certFile} onChange={(v) => setTls({ certFile: v })} mono hint="Absolute, or relative to the panel's data folder." />
                <TextField label="Key file (PEM)" value={s.tls.keyFile} onChange={(v) => setTls({ keyFile: v })} mono hint="Absolute, or relative to the panel's data folder." />
              </div>
            )}
            {mode === "off" && !isPublic && <Banner tone="warn" title="Plain HTTP">Fine on this machine only. Anything else should reach the panel over TLS.</Banner>}
            {mode !== "off" && (
              <ListField label="Names in the self-signed certificate" value={s.tls.hostnames} onChange={(v) => setTls({ hostnames: v })}
                hint="Besides localhost, 127.0.0.1 and ::1, one per line. Also counts as host names the panel answers to." />
            )}
            {mode !== "off" && <Toggle label="Tell browsers to use HTTPS only (HSTS)" checked={s.hsts} onChange={(v) => set({ hsts: v })}
              hint="Only for DNS names, never for localhost or IP addresses." />}
          </div>
        </Card>
        <Card title="Reverse proxy" sub="A web server in front of the panel (nginx, Caddy...).">
          <div className="wa-form wa-form-wide">
            <Toggle label="I have a reverse proxy" checked={s.reverseProxy.enabled} onChange={(v) => set({ reverseProxy: { ...s.reverseProxy, enabled: v } })}
              hint="Allows TLS off on a public address and trusts X-Forwarded-For, -Proto and -Host from the addresses below." />
            {s.reverseProxy.enabled && (
              <ListField label="Trusted proxies" value={s.reverseProxy.trustedProxies} onChange={(v) => set({ reverseProxy: { ...s.reverseProxy, trustedProxies: v } })}
                hint="The proxy's addresses or ranges, one per line." />
            )}
          </div>
        </Card>
        <Card title="Limits" sub="Connection and request limits. The defaults suit almost every server.">
          <details className="wa-details">
            <summary>Show the limits</summary>
            <div className="wa-form-grid">
              {LIMITS.map(([k, label, min, max, unit]) => (
                <NumberField key={k} label={label} unit={unit} min={min} max={max} step={k.includes("PerSecond") ? 0.1 : 1} value={s.limits[k] ?? 0}
                  onChange={(v) => set({ limits: { ...s.limits, [k]: v } })} />
              ))}
            </div>
          </details>
        </Card>
      </fieldset>
      {edit && (
        <div className="wa-save-row">
          <Button kind="primary" type="submit" busy={save.isPending} disabled={!draft}>Save</Button>
          {draft && <Button kind="ghost" onClick={() => { setDraft(null); setCustom(false); setOutcome(null); }}>Discard changes</Button>}
          {listenerChange && <span className="k-muted">Saving restarts the panel's listener: this page may need to be opened at the new address.</span>}
        </div>
      )}
      <OutcomeBanner o={outcome} />
    </form>
  );
}

// ------------------------------------------------------------------------------------------------ certificate

const CHALLENGES: { value: AcmeChallenge; label: string; hint: string }[] = [
  { value: "http-01", label: "HTTP (port 80)", hint: "Let's Encrypt connects to port 80 on each name. The panel answers there while an order is checked, so port 80 must reach this machine." },
  { value: "tls-alpn-01", label: "TLS (port 443)", hint: "Let's Encrypt connects to port 443, so the panel must listen on 443 or have 443 forwarded to it. The panel's own listener answers." },
  { value: "dns-01", label: "DNS (through the relay)", hint: "Needs hosted access (the relay): it sets the DNS record for your registered name. Nothing has to be reachable from outside." },
];

function CertificateTab({ edit }: { edit: boolean }) {
  const me = useSession((s) => s.me)!;
  const status = useQuery({ queryKey: ["acme-status"], queryFn: panel.acmeStatus, refetchInterval: 15_000, enabled: can(me, "acme.view") });
  const info = useQuery({ queryKey: ["panel-info"], queryFn: ep.info });
  const form = useSettingsFile("acme");
  const [renew, setRenew] = useState<Outcome | null>(null);
  const go = useMutation({
    mutationFn: panel.acmeRenew,
    onSuccess: () => { setRenew({ tone: "ok", title: "Renewal started. The status below updates within a minute." }); void status.refetch(); },
    onError: (e) => setRenew(isApiError(e) && e.code === "acme_blocked" ? { tone: "warn", title: `Not started: ${sentence(e.message)}` } : refused(e)),
  });
  const cert = info.data?.tls.certificate;
  const a = status.data;
  return (
    <>
      <Card title="Certificate in use">
        {info.isError ? <LoadError what="The certificate" error={info.error} retry={() => void info.refetch()} />
          : !info.data ? <Loading />
          : !cert ? <Empty title="No certificate">TLS is off: the panel answers over plain HTTP.</Empty>
          : (
            <dl className="wa-kv">
              <dt>From</dt><dd>{cert.source}</dd>
              <dt>Names</dt><dd className="k-mono wa-break">{cert.names.join(", ")}</dd>
              <dt>Valid until</dt><dd>{when(cert.notAfter)}</dd>
              <dt>Key</dt><dd>{cert.keyType}, chain of {cert.chain}</dd>
              <dt>Engine</dt><dd>{info.data.tls.engine}</dd>
              <dt>Fingerprint</dt><dd><Copyable text={cert.sha256} name="the fingerprint" /></dd>
            </dl>
          )}
      </Card>
      {can(me, "acme.view") && (
        <Card title="Let's Encrypt" sub="Ordered and renewed by the panel itself while the TLS certificate is set to Let's Encrypt."
          actions={can(me, "acme.renew") && a?.active && <Button size="s" busy={go.isPending} onClick={() => go.mutate()}>Renew now</Button>}>
          {status.isError ? <LoadError what="The certificate's status" error={status.error} retry={() => void status.refetch()} />
            : !a ? <Loading />
            : (
              <div className="wa-form wa-form-wide">
                <div className="wa-row">
                  {a.active ? <Pill tone={a.lastError ? "warn" : "ok"}>active</Pill> : <Pill tone="idle">not in use</Pill>}
                  <span>{a.blocked === "tls.mode is not acme" ? "The panel's certificate is not set to Let's Encrypt." : sentence(a.blocked ? `Waiting: ${a.blocked}` : a.phase)}</span>
                </div>
                {!a.active && a.blocked?.includes("tls.mode") && <p className="wa-note">Set the certificate to Let's Encrypt under Address and TLS to use it.</p>}
                <dl className="wa-kv">
                  {a.certificate && <><dt>Certificate</dt><dd>{a.certificate.names.join(", ")}, until {when(a.certificate.notAfter)} ({a.certificate.keyType})</dd></>}
                  <dt>Last try</dt><dd>{a.lastAttempt ? `${when(a.lastAttempt)} (${ago(a.lastAttempt)})` : "never"}</dd>
                  <dt>Last success</dt><dd>{when(a.lastSuccess)}</dd>
                  {a.nextAttempt && <><dt>Next try</dt><dd>{when(a.nextAttempt)}</dd></>}
                  <dt>Orders this week</dt><dd>{a.ordersThisWeek} of {a.maxOrdersPerWeek}</dd>
                  {a.failures > 0 && <><dt>Failures in a row</dt><dd>{a.failures}</dd></>}
                </dl>
                {a.lastError && <Banner tone="err" title="The last try failed">{sentence(a.lastError)}</Banner>}
                <OutcomeBanner o={renew} />
              </div>
            )}
        </Card>
      )}
      <FileCard form={form} edit={edit} title="Let's Encrypt settings" sub="Used while the certificate is set to Let's Encrypt. Nothing is ordered until you accept the terms.">
        {(v) => {
          const ch = CHALLENGES.find((c) => c.value === v.challenge) ?? CHALLENGES[0];
          return (
            <>
              <ListField label="Names to certify" value={v.names} onChange={(names) => form.set({ names })}
                hint="DNS names that point at this server, one per line (no wildcards, no IP addresses, at most 20)." />
              <div className="wa-form-grid">
                <TextField label="Contact email (optional)" type="email" value={v.email} onChange={(email) => form.set({ email })} hint="For expiry notices from the CA." autoComplete="email" />
                <SelectField label="Challenge" value={v.challenge} onChange={(challenge) => form.set({ challenge })}
                  options={CHALLENGES.map((c) => ({ value: c.value, label: c.label }))} />
              </div>
              <Banner tone="info" title={ch.label}>{ch.hint}</Banner>
              {v.challenge === "http-01" && (
                <div className="wa-form-grid">
                  <TextField label="Answer on address (optional)" value={v.http01Bind} onChange={(http01Bind) => form.set({ http01Bind: http01Bind.trim() })} mono hint="Empty: every address." />
                  <NumberField label="Answer on port" value={v.http01Port} min={0} max={65535} onChange={(http01Port) => form.set({ http01Port })} hint="80 unless port 80 is forwarded to another port." />
                </div>
              )}
              {v.challenge === "dns-01" && (
                <NumberField label="Wait for the DNS record" unit="seconds" value={v.dns01PropagationSeconds} min={5} max={300} onChange={(n) => form.set({ dns01PropagationSeconds: n })} />
              )}
              <Toggle label="I accept the certificate authority's terms of service" checked={v.acceptTerms} onChange={(acceptTerms) => form.set({ acceptTerms })}
                hint={<>For Let's Encrypt: the subscriber agreement at <a href="https://letsencrypt.org/repository/" target="_blank" rel="noreferrer">letsencrypt.org/repository</a>.</>} />
              <details className="wa-details">
                <summary>The CA, renewal and limits</summary>
                <div className="wa-sections">
                  <TextField label="ACME directory" value={v.directory} onChange={(directory) => form.set({ directory: directory.trim() })} mono hint="An https URL. Let's Encrypt's is the default." />
                  <div className="wa-form-grid">
                    <NumberField label="Renew when fewer days are left" value={v.renewDaysBefore} min={1} max={60} onChange={(n) => form.set({ renewDaysBefore: n })} />
                    <NumberField label="Wait after a failure, at least" unit="minutes" value={v.minRetryMinutes} min={1} max={1440} onChange={(n) => form.set({ minRetryMinutes: n })} />
                    <NumberField label="Wait after failures, at most" unit="hours" value={v.maxRetryHours} min={1} max={168} onChange={(n) => form.set({ maxRetryHours: n })} />
                    <NumberField label="Orders per week, at most" value={v.maxOrdersPerWeek} min={1} max={50} onChange={(n) => form.set({ maxOrdersPerWeek: n })} />
                  </div>
                </div>
              </details>
            </>
          );
        }}
      </FileCard>
    </>
  );
}

// ------------------------------------------------------------------------------------------------ sign-in

function SignInTab({ edit }: { edit: boolean }) {
  const me = useSession((s) => s.me)!;
  const status = useQuery({ queryKey: ["identity-status"], queryFn: panel.identityStatus, enabled: can(me, "users.view") });
  const identity = useSettingsFile("identity");
  const auth = useSettingsFile("auth");
  const st = status.data;
  return (
    <>
      {can(me, "users.view") && (
        <Card title="Sign-in providers" sub="Steam and Discord only sign in accounts whose user linked them (My account). They never make an account or give a role.">
          {status.isError ? <LoadError what="The providers' status" error={status.error} retry={() => void status.refetch()} />
            : !st ? <Loading />
            : (
              <div className="wa-form wa-form-wide">
                <dl className="wa-kv">
                  <dt>Steam</dt><dd>{st.steam.active ? <Pill tone="ok">on</Pill> : st.steam.enabled ? <Pill tone="warn">switched on, not working</Pill> : <Pill tone="idle">off</Pill>}</dd>
                  <dt>Discord</dt><dd>{st.discord.active ? <Pill tone="ok">on</Pill> : st.discord.enabled ? <Pill tone="warn">switched on, not working</Pill> : <Pill tone="idle">off</Pill>}</dd>
                  {st.discord.enabled && <><dt>Discord server</dt><dd className="k-mono">{st.discord.guildId || "not set"}</dd></>}
                  <dt>This panel's id</dt><dd><Copyable text={st.panelId} name="the panel's id" /></dd>
                </dl>
                {st.discord.enabled && !st.discord.keyPinned && (
                  <Banner tone="warn" title="Discord sign-in stays off">This build has no key for the broker {st.discord.broker}, so its answers cannot be checked.</Banner>
                )}
              </div>
            )}
        </Card>
      )}
      <FileCard form={identity} edit={edit} title="Steam and Discord" sub="Both are off until you switch them on.">
        {(v) => (
          <>
            <Toggle label="Sign in with Steam" checked={v.steam.enabled} onChange={(on) => identity.set({ steam: { ...v.steam, enabled: on } })}
              hint="Straight to steamcommunity.com, no broker. Linking a Steam account also marks its player as you on Player activity." />
            {v.steam.enabled && (
              <RoleMapEditor title="Panel roles from Kiln permission groups" value={v.steam.roleMap ?? []} onChange={(roleMap) => identity.set({ steam: { ...v.steam, roleMap } })}
                fromLabel="Permission group" fromHint="Group names come from kiln/config/permissions.json." mono={false}
                sentence="At each Steam sign-in, an account whose linked SteamID is in one of these groups gets the panel role beside it. These roles are kept apart from the ones set on Users and roles and go at once when you remove the line. The owner role is never given." />
            )}
            <Toggle label="Sign in with Discord" checked={v.discord.enabled} onChange={(on) => identity.set({ discord: { ...v.discord, enabled: on } })}
              hint="Through the frostdev.io broker, which tells the panel the user's roles on your Discord server." />
            {v.discord.enabled && (
              <div className="wa-sections">
                <div className="wa-form-grid">
                  <TextField label="Discord server (guild) id" value={v.discord.guildId} onChange={(guildId) => identity.set({ discord: { ...v.discord, guildId: guildId.trim() } })} mono inputMode="numeric"
                    hint="Right-click the server in Discord (developer mode on) and copy its id." />
                  <TextField label="Broker" value={v.discord.broker} onChange={(broker) => identity.set({ discord: { ...v.discord, broker: broker.trim() } })} mono hint="An https origin. Only a broker this build has a key for works." />
                </div>
                <ListField label="Discord roles that may sign in" value={v.discord.allowedRoles} onChange={(allowedRoles) => identity.set({ discord: { ...v.discord, allowedRoles } })}
                  hint="Role ids, one per line. Empty: any member of the server. A role here only lets a linked account sign in; it never gives a panel role." />
                <RoleMapEditor title="Panel roles from Discord roles" value={v.discord.roleMap ?? []} onChange={(roleMap) => identity.set({ discord: { ...v.discord, roleMap } })}
                  fromLabel="Discord role id" fromHint="Role ids are digits: right-click the role in Discord (developer mode on) and copy its id." mono
                  sentence="At each Discord sign-in, an account whose user has one of these Discord roles gets the panel role beside it. These roles are kept apart from the ones set on Users and roles and go at once when you remove the line. The owner role is never given." />
              </div>
            )}
          </>
        )}
      </FileCard>
      <FileCard form={auth} edit={edit} title="Sessions and passwords">
        {(v) => (
          <>
            <div className="wa-form-grid">
              <NumberField label="Sign out after idle" unit="minutes" value={v.sessionIdleMinutes} min={5} max={1440} onChange={(n) => auth.set({ sessionIdleMinutes: n })} />
              <NumberField label="Sign out after, whatever the use" unit="hours" value={v.sessionAbsoluteHours} min={1} max={720} onChange={(n) => auth.set({ sessionAbsoluteHours: n })} />
              <NumberField label="Sessions per account" value={v.maxSessionsPerAccount} min={1} max={100} onChange={(n) => auth.set({ maxSessionsPerAccount: n })} hint="The oldest ends when one more signs in." />
              <NumberField label="Shortest password" unit="characters" value={v.passwordMinLength} min={8} max={128} onChange={(n) => auth.set({ passwordMinLength: n })} />
              <NumberField label="Live connections per account" value={v.maxSocketsPerAccount} min={1} max={64} onChange={(n) => auth.set({ maxSocketsPerAccount: n })} />
            </div>
            <Toggle label="One-time sign-in from the Kiln Launcher" checked={v.launcherLogin} onChange={(launcherLogin) => auth.set({ launcherLogin })}
              hint={`"Open web panel" in the launcher signs in as the owner with a link that works once.`} />
            <details className="wa-details">
              <summary>Sign-in attempts and lockouts</summary>
              <div className="wa-form-grid">
                <NumberField label="Attempts per minute per address" value={v.signIn.attemptsPerMinutePerIp} min={1} max={600} onChange={(n) => auth.set({ signIn: { ...v.signIn, attemptsPerMinutePerIp: n } })} />
                <NumberField label="Attempts per minute per account" value={v.signIn.attemptsPerMinutePerAccount} min={1} max={60} onChange={(n) => auth.set({ signIn: { ...v.signIn, attemptsPerMinutePerAccount: n } })} />
                <NumberField label="Failures before an account locks" value={v.signIn.accountFreeFailures} min={1} max={100} onChange={(n) => auth.set({ signIn: { ...v.signIn, accountFreeFailures: n } })} />
                <NumberField label="Failures before an address locks" value={v.signIn.ipFreeFailures} min={1} max={1000} onChange={(n) => auth.set({ signIn: { ...v.signIn, ipFreeFailures: n } })} />
                <NumberField label="First lockout" unit="seconds" value={v.signIn.lockoutBaseSeconds} min={1} max={3600} onChange={(n) => auth.set({ signIn: { ...v.signIn, lockoutBaseSeconds: n } })} />
                <NumberField label="Longest lockout" unit="seconds" value={v.signIn.lockoutMaxSeconds} min={1} max={86400} onChange={(n) => auth.set({ signIn: { ...v.signIn, lockoutMaxSeconds: n } })} />
              </div>
            </details>
            <details className="wa-details">
              <summary>Password hashing (applies at the next start)</summary>
              <div className="wa-form-grid">
                <NumberField label="PBKDF2 iterations" value={v.pbkdf2Iterations} min={600000} max={10000000} onChange={(n) => auth.set({ pbkdf2Iterations: n })} />
                <NumberField label="Password checks at once" value={v.maxConcurrentHashes} min={1} max={16} onChange={(n) => auth.set({ maxConcurrentHashes: n })} hint="Each uses one core for a moment." />
              </div>
            </details>
          </>
        )}
      </FileCard>
    </>
  );
}

/** Lines of "this provider group gives that panel role" (at most 32; never the owner role). */
function RoleMapEditor({ title, value, onChange, fromLabel, fromHint, sentence: line, mono }: {
  title: string; value: RoleMapping[]; onChange: (v: RoleMapping[]) => void; fromLabel: string; fromHint: string; sentence: string; mono: boolean;
}) {
  const me = useSession((s) => s.me)!;
  const roles = useQuery({ queryKey: ["roles"], queryFn: ep.roles, enabled: can(me, "users.view") });
  const options = (roles.data ?? []).filter((r) => r.id !== "owner").map((r) => ({ value: r.id, label: r.name }));
  const set = (i: number, patch: Partial<RoleMapping>) => onChange(value.map((m, j) => (j === i ? { ...m, ...patch } : m)));
  return (
    <fieldset className="wa-fieldset">
      <legend>{title}</legend>
      <p className="wa-note">{line} {fromHint}</p>
      {!value.length && <p className="k-muted wa-p">None: sign-in gives no roles.</p>}
      {value.map((m, i) => (
        <div key={i} className="wa-key-row wa-map-row">
          <TextField label={fromLabel} value={m.from} onChange={(from) => set(i, { from: from.trim() })} mono={mono} />
          {options.length ? (
            <SelectField label="Panel role" value={m.role} onChange={(role) => set(i, { role })}
              options={[...(m.role && !options.some((o) => o.value === m.role) ? [{ value: m.role, label: m.role }] : []), ...(m.role ? [] : [{ value: "", label: "Choose…" }]), ...options]} />
          ) : <TextField label="Panel role id" value={m.role} onChange={(role) => set(i, { role: role.trim() })} mono />}
          <Button kind="ghost" aria-label={`Remove the line for ${m.from || `line ${i + 1}`}`} onClick={() => onChange(value.filter((_, j) => j !== i))}>Remove</Button>
        </div>
      ))}
      <div className="wa-row"><Button size="s" disabled={value.length >= 32} onClick={() => onChange([...value, { from: "", role: options[0]?.value ?? "" }])}>Add a line</Button></div>
    </fieldset>
  );
}

// ------------------------------------------------------------------------------------------------ hosted access

const RELAY_STATE: Record<string, [Tone, string]> = {
  off: ["idle", "off"], waiting: ["warn", "waiting"], connected: ["ok", "connected"], error: ["err", "error"],
};

function RelayTab({ edit }: { edit: boolean }) {
  const status = useQuery({ queryKey: ["relay-status"], queryFn: panel.relay, refetchInterval: 10_000 });
  const form = useSettingsFile("relay");
  const r = status.data;
  const [tone, label] = RELAY_STATE[r?.state ?? "off"] ?? ["idle", r?.state ?? "?"];
  return (
    <>
      <Card title="Hosted access" sub="Reach the panel at a kiln.nexus name without forwarding a port." actions={r && <Pill tone={tone}>{label}</Pill>}>
        <div className="wa-form wa-form-wide">
          <p className="wa-p">The panel keeps one connection open to the relay, and the relay passes browsers' encrypted traffic through to it by name.
            <strong> The relay never decrypts the traffic:</strong> TLS ends here, with a certificate whose key never leaves this server.</p>
          {status.isError ? <LoadError what="The relay's status" error={status.error} retry={() => void status.refetch()} />
            : !r ? <Loading />
            : (
              <>
                <dl className="wa-kv">
                  <dt>Name</dt><dd className="k-mono">{r.host || "not set"}</dd>
                  <dt>Relay</dt><dd className="k-mono">{r.origin}</dd>
                  {r.since && <><dt>Since</dt><dd>{when(r.since)}</dd></>}
                </dl>
                {r.enabled && r.waitingFor.length > 0 && (
                  <Banner tone="warn" title="Waiting for">
                    <ul className="wa-problems">{r.waitingFor.map((w) => <li key={w}>{sentence(w)}</li>)}</ul>
                  </Banner>
                )}
                {r.lastError && <Banner tone="err" title="The last connection failed">{sentence(r.lastError)}</Banner>}
                {r.publicKey ? (
                  <div className="wa-stack">
                    <span className="k-label">This panel's public key</span>
                    <Copyable text={r.publicKey.trim()} block name="the public key" />
                    <p className="wa-note">Not a secret. To register the name by hand instead of claiming it, the owner sends this key to the relay's operator.</p>
                  </div>
                ) : <p className="wa-note">The panel makes its key pair when hosted access is first switched on, or with the first claim link; its public key then shows here.</p>}
              </>
            )}
        </div>
      </Card>
      {r && <ClaimCard r={r} edit={edit} />}
      <FileCard form={form} edit={edit} title="Relay settings">
        {(v) => (
          <>
            <Toggle label="Use hosted access" checked={v.enabled} onChange={(enabled) => form.set({ enabled })} hint="Needs TLS on and the name in the certificate's names." />
            <div className="wa-form-grid">
              <TextField label="Registered name" value={v.host} onChange={(host) => form.set({ host: host.trim().toLowerCase() })} mono hint="Such as myserver.kiln.nexus, in lower case: the name you claim (or the relay's operator registered)." />
              <TextField label="Relay" value={v.origin} onChange={(origin) => form.set({ origin: origin.trim() })} mono hint="An https origin." />
            </div>
          </>
        )}
      </FileCard>
    </>
  );
}

/** A claim refused: the dialog for a recent proof, what to fix, or the message. */
function claimRefused(e: unknown): Outcome {
  if (isApiError(e) && e.code === "reauth_required") return { tone: "warn", title: "Confirm it is you in the dialog, then press Make a claim link again." };
  if (isApiError(e) && e.status === 409) return { tone: "warn", title: sentence(e.message) };
  if (isApiError(e) && e.status === 403) return { tone: "err", title: "Your account may not make claim links." };
  return { tone: "err", title: `No claim link. ${explain(e)}` };
}

/** Claim this name (lane L1): a one-time link to the relay's Discord claim pages, signed with this panel's relay key. */
function ClaimCard({ r, edit }: { r: RelayStatus; edit: boolean }) {
  const [link, setLink] = useState<RelayClaimLink | null>(null);
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  const qc = useQueryClient();
  const make = useMutation({
    mutationFn: panel.relayClaim,
    onSuccess: (l) => {
      setLink(l);
      setOutcome(null);
      void qc.invalidateQueries({ queryKey: ["relay-status"] });
    },
    onError: (e) => {
      setLink(null);
      setOutcome(claimRefused(e));
    },
  });
  const where = r.namesPage ? new URL(r.namesPage).host : "the relay";
  return (
    <Card title="Claim this name" sub="With your Discord account, without waiting for the relay's operator.">
      <div className="wa-form wa-form-wide">
        <p className="wa-p">Members of the Kiln Discord server claim the name above for this server themselves, on {where}'s claim page (one name per
          Discord account). The link proves the claim comes from this server's key; it works once, for 15 minutes. Once the name is claimed, the
          panel connects within seconds if hosted access is on; an unused claim lapses after an hour.</p>
        <dl className="wa-kv">
          <dt>Name</dt><dd className="k-mono">{r.host || "not set"}</dd>
          {r.fingerprint && <><dt>Key fingerprint</dt><dd className="k-mono" title={r.fingerprint}>{shortFingerprint(r.fingerprint)}</dd></>}
        </dl>
        {!r.host && <p className="wa-note">Set the registered name below (such as myserver.kiln.nexus) and save first.</p>}
        {edit ? (
          <div className="wa-row">
            <Button kind="primary" onClick={() => make.mutate()} busy={make.isPending} disabled={!r.host}>Make a claim link</Button>
          </div>
        ) : <p className="wa-note">Only the owner can make a claim link.</p>}
        <OutcomeBanner o={outcome} />
        {link && (
          <Banner tone="ok" title="Claim link ready">
            <div className="wa-stack">
              {isClaimUrl(link.url) && (
                <p><a className="k-btn k-btn-primary" href={link.url} target="_blank" rel="noopener noreferrer">Open the claim page</a></p>
              )}
              <Copyable text={link.url} block name="the claim link" />
              <p className="wa-note">Open it yourself and sign in with the Discord account that should own the name: whoever opens it first can claim
                the name for this server. It works until {when(link.expires)}.</p>
              {link.warnings.length > 0 && <ul className="wa-problems">{link.warnings.map((w) => <li key={w}>{sentence(w)}</li>)}</ul>}
            </div>
          </Banner>
        )}
        {r.namesPage && isClaimUrl(`${r.namesPage}/claim?v=1&`) && (
          <p className="wa-note">Your name, its state, releasing it and moving it to a new key: <a href={r.namesPage} target="_blank" rel="noopener noreferrer">My panel name</a>.</p>
        )}
      </div>
    </Card>
  );
}

// ------------------------------------------------------------------------------------------------ branding

function BrandingTab({ edit }: { edit: boolean }) {
  const form = useSettingsFile("branding");
  const branding = useSession((s) => s.branding);
  const qc = useQueryClient();
  const refreshBranding = async () => {
    const b = await ep.branding().catch(() => null);
    if (b) session.setState({ branding: b });
    void qc.invalidateQueries({ queryKey: FILES });
  };
  // the shell shows the new name and colour as soon as they are saved
  useEffect(() => {
    if (form.outcome?.tone === "ok") void refreshBranding();
  }, [form.outcome]); // eslint-disable-line react-hooks/exhaustive-deps
  return (
    <>
      <FileCard form={form} edit={edit} title="Name and colour" sub="Shown in the top bar, on the sign-in page and on the public page.">
        {(v) => <NameAndColour v={v} set={form.set} fallback={branding.name} />}
      </FileCard>
      <Card title="Logo and favicon" sub="A PNG, WebP or ICO image, at most 512 KiB. SVG is refused.">
        <div className="wa-sections">
          <BrandImage kind="logo" title="Logo" url={branding.logo} edit={edit} onChange={refreshBranding} />
          <BrandImage kind="favicon" title="Favicon (the browser tab's icon)" url={branding.favicon} edit={edit} onChange={refreshBranding} />
        </div>
      </Card>
    </>
  );
}

function NameAndColour({ v, set, fallback }: { v: SettingsFiles["branding"]; set: (p: Partial<SettingsFiles["branding"]>) => void; fallback: string }) {
  const id = useId();
  const accentOk = !v.accent || /^#[0-9a-f]{6}$/i.test(v.accent);
  return (
    <>
      <TextField label="Server name" value={v.name ?? ""} onChange={(name) => set({ name: name === "" ? null : name })} maxLength={64}
        hint={`Empty: the game server's name (now "${fallback}").`} />
      <div className="k-field">
        <div className="k-field-head"><label className="k-label" htmlFor={id}>Accent colour</label></div>
        <div className="wa-color-row">
          <input type="color" className="wa-swatch" aria-label="Pick the accent colour" value={accentOk && v.accent ? v.accent : "#ff6a1a"} onChange={(e) => set({ accent: e.target.value })} />
          <input id={id} className={"k-input k-input-mono" + (accentOk ? "" : " invalid")} value={v.accent ?? ""} placeholder="#ff6a1a" maxLength={7} spellCheck={false}
            onChange={(e) => set({ accent: e.target.value.trim() === "" ? null : e.target.value.trim() })} aria-invalid={!accentOk || undefined} />
          {v.accent && <Button kind="ghost" size="s" onClick={() => set({ accent: null })}>Use the default</Button>}
        </div>
        {accentOk ? <div className="k-hint"># and six hex digits. Empty: the Kiln ember.</div> : <div className="k-error-text" role="alert">A colour like #ff6a1a (# and six hex digits).</div>}
      </div>
    </>
  );
}

function BrandImage({ kind, title, url, edit, onChange }: { kind: "logo" | "favicon"; title: string; url: string | null; edit: boolean; onChange: () => Promise<void> }) {
  const id = useId();
  const [file, setFile] = useState<File | null>(null);
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  const [v, setV] = useState(0);
  const type = file ? imageType(file) : null;
  const tooBig = !!file && file.size > 512 * 1024;
  const upload = useMutation({
    mutationFn: () => panel.uploadBrandImage(kind, file!, type!),
    onSuccess: async () => { setOutcome({ tone: "ok", title: `${title}: uploaded.` }); setFile(null); setV((n) => n + 1); await onChange(); },
    onError: (e) => setOutcome(refused(e)),
  });
  const remove = useMutation({
    mutationFn: () => panel.removeBrandImage(kind),
    onSuccess: async () => { setOutcome({ tone: "ok", title: `${title}: removed.` }); setFile(null); setV((n) => n + 1); await onChange(); },
    onError: (e) => setOutcome(refused(e)),
  });
  const preview = useMemo(() => (file && type ? URL.createObjectURL(file) : null), [file, type]);
  useEffect(() => () => { if (preview) URL.revokeObjectURL(preview); }, [preview]);
  const shown = preview ?? (url ? `${url}?v=${v}` : null);
  return (
    <fieldset className="wa-fieldset">
      <legend>{title}</legend>
      <div className="wa-brand-img">
        <div className={"wa-brand-preview" + (kind === "favicon" ? " small" : "")}>
          {shown ? <img src={shown} alt={preview ? `The new ${kind}` : `The current ${kind}`} /> : <span className="k-muted">none</span>}
        </div>
        {edit ? (
          <div className="wa-file">
            <label className="k-label" htmlFor={id}>{url ? "Replace it" : "Upload one"}</label>
            <input key={v} id={id} type="file" accept="image/png,image/webp,image/x-icon,.ico,.png,.webp" onChange={(e) => { setOutcome(null); setFile(e.target.files?.[0] ?? null); }} />
            {file && !type && <span className="k-error-text" role="alert">Choose a PNG, WebP or ICO file.</span>}
            {tooBig && <span className="k-error-text" role="alert">That file is larger than 512 KiB.</span>}
            <div className="wa-row">
              <Button kind="primary" size="s" disabled={!file || !type || tooBig} busy={upload.isPending} onClick={() => upload.mutate()}>Upload</Button>
              {url && <Button kind="ghost" size="s" busy={remove.isPending} onClick={() => remove.mutate()}>Remove</Button>}
            </div>
          </div>
        ) : !url && <span className="k-muted">The default Kiln {kind} is used.</span>}
      </div>
      <OutcomeBanner o={outcome} />
    </fieldset>
  );
}

// ------------------------------------------------------------------------------------------------ modules

function ModulesTab() {
  const me = useSession((s) => s.me)!;
  const signIns = useSession((s) => s.signIns);
  const qc = useQueryClient();
  // every module with its switch, switched-off ones too (core.settings.view, which this page needs anyway)
  const all = useQuery({ queryKey: ["module-switches", me.id, signIns], queryFn: panel.moduleSwitches });
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  const editable = can(me, "core.modules.edit");
  const list: ModuleSwitch[] = useMemo(
    () => [...(all.data?.modules ?? [])].sort((a, b) => a.title.localeCompare(b.title)),
    [all.data],
  );
  const set = useMutation({
    mutationFn: ({ m, on }: { m: ModuleSwitch; on: boolean }) => panel.setModule(m.id, on),
    onSuccess: (_, { m, on }) => {
      setOutcome({ tone: "ok", title: `${m.title} is ${on ? "on" : "off"}.` });
      void qc.invalidateQueries({ queryKey: ["module-switches"] });
      void qc.invalidateQueries({ queryKey: ["modules"] });
    },
    onError: (e, { m }) => setOutcome(isApiError(e) && e.status === 409 ? { tone: "warn", title: `${m.title}: ${sentence(e.message)}` } : refused(e)),
  });
  const q = all;
  return (
    <Card title="Modules" sub="Every page, widget and background job belongs to a module. A module switched off hides its pages and stops its routes for everyone.">
      {q.isError ? <LoadError what="The module list" error={q.error} retry={() => void q.refetch()} />
        : q.isLoading ? <Loading />
        : (
          <div className="wa-form wa-form-wide">
            {!editable && <p className="wa-note">Only the owner can switch modules.</p>}
            <div className="wa-module-list">
              {list.map((m) => {
                const fixed = m.canDisable === false || (m.canDisable === null && ALWAYS_ON.has(m.id));
                return (
                  <Toggle key={m.id} label={m.title} checked={m.enabled} disabled={!editable || (fixed && m.enabled) || set.isPending}
                    hint={fixed ? "Always on: the panel needs it." : <span className="k-mono">{m.id}</span>}
                    onChange={(on) => { setOutcome(null); set.mutate({ m, on }); }} />
                );
              })}
            </div>
            <OutcomeBanner o={outcome} />
          </div>
        )}
    </Card>
  );
}

// ------------------------------------------------------------------------------------------------ map art

function MapArtTab() {
  const q = useQuery({ queryKey: ["map-assets"], queryFn: panel.mapAssets });
  return (
    <Card title="Map art" sub="The terrain tiles behind the Map page.">
      {q.isError ? <LoadError what="The map art's status" error={q.error} retry={() => void q.refetch()} />
        : !q.data ? <Loading />
        : q.data.present ? (
          <div className="wa-form wa-form-wide">
            <div className="wa-row"><Pill tone="ok">present</Pill><span>The map shows the game's terrain.</span></div>
            <p className="wa-note">From <span className="k-mono">{q.data.folder}</span> on the server.</p>
          </div>
        ) : (
          <div className="wa-form wa-form-wide">
            <div className="wa-row"><Pill tone="idle">not on the server</Pill><span>The map shows the live overlays (players, zones) on a plain background.</span></div>
            <p className="wa-p">The terrain art is the game's own, so the panel never ships it. To add it:</p>
            <ol className="wa-list">
              <li>In the Kiln Launcher, open Settings, then Game art. It makes an <span className="k-mono">admin-assets</span> folder from your copy of the game.</li>
              <li>Copy that folder's contents (its <span className="k-mono">map</span> and <span className="k-mono">data</span> folders) into <span className="k-mono">{q.data.folder}</span> in the server's folder.</li>
              <li>Reload the Map page. No restart is needed.</li>
            </ol>
          </div>
        )}
    </Card>
  );
}
