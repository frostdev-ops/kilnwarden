// My account (DESIGN §3): who I am, my password, two-factor sign-in (set up with a QR code, recovery codes shown once,
// turn off, new codes) and my sessions (revoke one, or every other). The password and two-factor parts are also the
// restricted session's way out (shell/Restricted.tsx).
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState, type FormEvent } from "react";
import { useLocation } from "react-router-dom";
import { ep, useSession } from "../app/context";
import { isApiError } from "../api/http";
import type { SessionInfo } from "../api/types";
import { Qr } from "../ui/Qr";
import { LinkedSignInsCard } from "../identity/ExternalSignIn";
import { Banner, Button, Card, Copyable, Page, Pill, TextField, errorText, when } from "../ui/kit";

const msg = (e: unknown) => (isApiError(e) ? e.message + (e.retryAfter ? ` (try again in ${e.retryAfter} s)` : "") : errorText(e));

export function AccountPage() {
  const me = useSession((s) => s.me)!;
  const loc = useLocation();
  useEffect(() => {
    if (!loc.hash) return;
    document.getElementById(loc.hash.slice(1))?.scrollIntoView({ block: "start" });
  }, [loc.hash]);
  return (
    <Page title="My account" sub={<>{me.name} · {me.roles.join(", ")} · commands up to <strong>{me.commandLevel}</strong></>}>
      <div className="wa-grid">
        <PasswordCard />
        <TwoFactorCard />
      </div>
      <LinkedSignInsCard />
      <SessionsCard />
    </Page>
  );
}

export function PasswordCard({ required }: { required?: boolean }) {
  const refresh = useSession((s) => s.refresh);
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [again, setAgain] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);
  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await ep.changePassword(current, next);
      setDone(true);
      setCurrent("");
      setNext("");
      setAgain("");
      await refresh();
    } catch (x) {
      setError(msg(x));
    } finally {
      setBusy(false);
    }
  };
  return (
    <Card title="Password" sub={required ? "Choose your own password before you go on." : undefined}>
      <form className="wa-form" onSubmit={submit}>
        <TextField label="Current password" type="password" value={current} onChange={setCurrent} autoComplete="current-password" />
        <TextField label="New password" type="password" value={next} onChange={setNext} autoComplete="new-password" error={next && next.length < 12 ? "At least 12 characters." : undefined} />
        <TextField label="New password again" type="password" value={again} onChange={setAgain} autoComplete="new-password" error={again && again !== next ? "The passwords differ." : undefined} />
        {error && <Banner tone="err" title={error} />}
        {done && !error && <Banner tone="ok" title="Password changed." />}
        <Button kind="primary" type="submit" busy={busy} disabled={!current || next.length < 12 || next !== again}>Change password</Button>
      </form>
    </Card>
  );
}

export function TwoFactorCard({ required }: { required?: boolean }) {
  const me = useSession((s) => s.me)!;
  const refresh = useSession((s) => s.refresh);
  const [step, setStep] = useState<"idle" | "password" | "scan" | "codes" | "disable" | "newCodes">("idle");
  const [password, setPassword] = useState("");
  const [code, setCode] = useState("");
  const [setup, setSetup] = useState<{ secret: string; uri: string } | null>(null);
  const [codes, setCodes] = useState<string[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const reset = () => {
    setPassword("");
    setCode("");
    setError(null);
  };
  const act = async (e: FormEvent | null, f: () => Promise<void>) => {
    e?.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await f();
    } catch (x) {
      setError(msg(x));
    } finally {
      setBusy(false);
    }
  };

  let body;
  if (step === "password" || step === "newCodes" || step === "disable") {
    body = (
      <form className="wa-form" onSubmit={(e) => act(e, async () => {
        if (step === "password") {
          setSetup(await ep.twoFactorBegin(password));
          setStep("scan");
        } else if (step === "newCodes") {
          setCodes((await ep.recoveryCodes(password)).recoveryCodes);
          setStep("codes");
          await refresh();
        } else {
          await ep.twoFactorDisable(password, code.replace(/\s/g, ""));
          setStep("idle");
          await refresh();
        }
        reset();
      })}>
        <TextField label="Your password" type="password" value={password} onChange={setPassword} autoComplete="current-password" autoFocus />
        {step === "disable" && <TextField label="Code from your authenticator app" value={code} onChange={setCode} inputMode="numeric" autoComplete="one-time-code" mono />}
        {error && <Banner tone="err" title={error} />}
        <div className="wa-row">
          <Button kind={step === "disable" ? "danger" : "primary"} type="submit" busy={busy} disabled={!password || step === "disable" && !code.trim()}>
            {step === "password" ? "Continue" : step === "newCodes" ? "Make new recovery codes" : "Turn off two-factor"}
          </Button>
          <Button kind="ghost" onClick={() => { setStep("idle"); reset(); }}>Cancel</Button>
        </div>
      </form>
    );
  } else if (step === "scan" && setup) {
    body = (
      <form className="wa-form" onSubmit={(e) => act(e, async () => {
        const r = await ep.twoFactorEnable(code.replace(/\s/g, ""));
        setCodes(r.recoveryCodes);
        setSetup(null);
        setStep("codes");
        reset();
        // Keep required setup mounted until the user saves the once-only codes. Refreshing here would clear the
        // restriction, unmount this page and discard the only copy of those codes before they could read them.
      })}>
        <p>Scan this with your authenticator app (any TOTP app), or type the key in by hand.</p>
        <div className="wa-qr-row">
          <Qr text={setup.uri} label="QR code for your authenticator app" />
          <div className="wa-stack">
            <span className="k-label">Key</span>
            <Copyable text={setup.secret.replace(/(.{4})/g, "$1 ").trim()} />
          </div>
        </div>
        <TextField label="The 6-digit code the app shows now" value={code} onChange={setCode} inputMode="numeric" autoComplete="one-time-code" mono autoFocus maxLength={9} />
        {error && <Banner tone="err" title={error} />}
        <div className="wa-row">
          <Button kind="primary" type="submit" busy={busy} disabled={code.replace(/\s/g, "").length < 6}>Turn on two-factor</Button>
          <Button kind="ghost" onClick={() => { setStep("idle"); setSetup(null); reset(); }}>Cancel</Button>
        </div>
      </form>
    );
  } else if (step === "codes" && codes) {
    const text = codes.join("\n");
    body = (
      <div className="wa-form">
        <Banner tone="warn" title="Save these recovery codes now">Each signs you in once if you lose your phone. They are not shown again.</Banner>
        <ol className="wa-codes" aria-label="Recovery codes">{codes.map((c) => <li key={c}><code>{c}</code></li>)}</ol>
        <div className="wa-row">
          <Button onClick={() => void navigator.clipboard?.writeText(text)}>Copy all</Button>
          <a className="k-btn" download="recovery-codes.txt" href={URL.createObjectURL(new Blob([text + "\n"], { type: "text/plain" }))}>Download</a>
          <Button kind="primary" onClick={() => { setCodes(null); setStep("idle"); void refresh(); }}>I saved them</Button>
        </div>
      </div>
    );
  } else {
    body = me.twoFactor ? (
      <div className="wa-form">
        <p><Pill tone="ok">On</Pill> {me.recoveryCodesLeft} recovery code{me.recoveryCodesLeft === 1 ? "" : "s"} left.</p>
        {me.recoveryCodesLeft <= 2 && <Banner tone="warn" title="Few recovery codes left">Make new ones (the old ones stop working).</Banner>}
        <div className="wa-row">
          <Button onClick={() => setStep("newCodes")}>New recovery codes</Button>
          <Button kind="danger" onClick={() => setStep("disable")}>Turn off</Button>
        </div>
      </div>
    ) : (
      <div className="wa-form">
        <p><Pill tone="idle">Off</Pill> Sign-in asks only for your password.</p>
        {required && <Banner tone="warn" title="One of your roles requires two-factor sign-in">Set it up to use the panel.</Banner>}
        <Button kind="primary" onClick={() => setStep("password")}>Set up two-factor</Button>
      </div>
    );
  }
  return <Card title={<span id="two-factor">Two-factor sign-in</span>}>{body}</Card>;
}

function SessionsCard() {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ["sessions"], queryFn: ep.sessions });
  const [error, setError] = useState<string | null>(null);
  const run = async (f: () => Promise<unknown>) => {
    setError(null);
    try {
      await f();
    } catch (x) {
      setError(msg(x));
    }
    await qc.invalidateQueries({ queryKey: ["sessions"] });
  };
  const others = (q.data ?? []).filter((s) => !s.current).length;
  return (
    <Card title={<span id="sessions">Sessions</span>} sub="Where you are signed in." actions={
      <Button size="s" disabled={!others} onClick={() => void run(ep.revokeOtherSessions)}>Sign out everywhere else</Button>
    }>
      {error && <Banner tone="err" title={error} />}
      {q.isError && <Banner tone="err" title={msg(q.error)} />}
      <SessionTable sessions={q.data ?? []} onRevoke={(s) => void run(() => ep.revokeSession(s.handle))} />
    </Card>
  );
}

export function SessionTable({ sessions, onRevoke }: { sessions: SessionInfo[]; onRevoke?: (s: SessionInfo) => void }) {
  if (!sessions.length) return <p className="k-muted">No sessions.</p>;
  return (
    <div className="wa-table-wrap">
      <table className="wa-table">
        <thead>
          <tr><th>Device</th><th>From</th><th>Signed in</th><th>Last seen</th><th>How</th>{onRevoke && <th><span className="k-sr-only">Actions</span></th>}</tr>
        </thead>
        <tbody>
          {sessions.map((s) => (
            <tr key={s.handle}>
              <td data-label="Device">{agent(s.userAgent)} {s.current && <Pill tone="ember">this one</Pill>}</td>
              <td data-label="From" className="k-mono">{s.ip}</td>
              <td data-label="Signed in">{when(s.created)}</td>
              <td data-label="Last seen">{when(s.lastSeen)}</td>
              <td data-label="How">{s.via}{s.twoFactor ? " + 2FA" : ""}</td>
              {onRevoke && <td className="wa-actions"><Button size="s" kind={s.current ? "danger" : "default"} onClick={() => onRevoke(s)}>{s.current ? "Sign out" : "Revoke"}</Button></td>}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** A short name for a user agent: the browser and the system. */
export function agent(ua: string): string {
  if (!ua) return "Unknown";
  const browser = /Edg\//.test(ua) ? "Edge" : /Firefox\//.test(ua) ? "Firefox" : /Chrome\//.test(ua) ? "Chrome" : /Safari\//.test(ua) ? "Safari" : /curl|python|Go-http/i.test(ua) ? "Script" : "Browser";
  const os = /Android/.test(ua) ? "Android" : /iPhone|iPad/.test(ua) ? "iOS" : /Windows/.test(ua) ? "Windows" : /Mac OS X/.test(ua) ? "macOS" : /Linux/.test(ua) ? "Linux" : "";
  return os ? `${browser} on ${os}` : browser;
}
