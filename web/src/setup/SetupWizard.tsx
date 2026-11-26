// The setup wizard (0.1.2) at /setup: one question per page, in plain words, until the panel opens at its address.
// Before the owner exists: welcome, then the account (the one-time link from kiln.log, the console or the launcher).
// After: how you open the panel (hosted access, the Kiln Launcher, your own address), that route's pages with a live
// checklist, Discord sign-in for helpers (optional), done. The place is kept on the server (setup.json): the wizard
// comes back to it after a restart, and Panel settings runs it again. Anything technical sits behind "Details".
import { useEffect, useMemo, useRef, useState, type FormEvent, type ReactNode } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { api, ep, useSession } from "../app/context";
import { isApiError } from "../api/http";
import { Splash, takeFragment } from "../pages/Public";
import { useBranding } from "../shell/useBranding";
import { setupSeen } from "./SetupPending";
import { Banner, Button, TextField, errorText } from "../ui/kit";
import {
  allDone, checkLabel, firstProblem, nameProblem, normalizeName, position, reasonText, resumeAt, setupApi, suggestName,
  type Route, type SetupState, type SetupStep, type StepId,
} from "./wizard";

const setup = setupApi(api);
const KILN_DISCORD = "https://discord.gg/7pjr8SMTfU";
const LE_TERMS = "https://letsencrypt.org/repository/";

const wait = (s: number | null) => (s ? ` Try again in ${s >= 60 ? `${Math.ceil(s / 60)} min` : `${s} s`}.` : "");
const say = (e: unknown) => (isApiError(e) ? e.message.replace(/^./, (c) => c.toUpperCase()) + (/[.!?]$/.test(e.message) ? "" : ".") + wait(e.retryAfter) : errorText(e));

/** The wizard's frame: the server's name, the progress dots, one page. */
function Frame({ step, route, title, children, back }: { step: StepId; route: Route | ""; title: string; children: ReactNode; back?: () => void }) {
  const b = useSession((s) => s.branding);
  useBranding(b, "Setup");
  const { at, of } = position(route, step);
  return (
    <div className="wa-auth">
      <main className="wa-auth-card wa-wizard" aria-labelledby="wa-wizard-title" data-step={step}>
        <div className="wa-wizard-top">
          <div className="wa-auth-brand">
            {b.logo ? <img className="wa-auth-logo" src={b.logo} alt="" /> : <span className="wa-mark wa-mark-l" aria-hidden />}
            <div className="wa-auth-server">{b.name}</div>
          </div>
          <ol className="wa-dots" aria-label={`Step ${at} of ${of}`}>
            {Array.from({ length: of }, (_, i) => <li key={i} className={i + 1 < at ? "done" : i + 1 === at ? "now" : ""} aria-hidden />)}
          </ol>
        </div>
        <h1 id="wa-wizard-title" className="wa-auth-title">{title}</h1>
        {children}
        {back && <button type="button" className="k-link wa-link-btn wa-back" onClick={back}>Back</button>}
      </main>
    </div>
  );
}

function Details({ children, label = "Details" }: { children: ReactNode; label?: string }) {
  return (
    <details className="wa-details wa-wizard-details">
      <summary>{label}</summary>
      <div className="wa-stack">{children}</div>
    </details>
  );
}

export function SetupWizard() {
  const phase = useSession((s) => s.phase);
  const status = useSession((s) => s.status);
  const me = useSession((s) => s.me);
  const loc = useLocation() as { state?: { setupToken?: string } };
  const token = useRef<string | null>(null);
  if (token.current === null) token.current = loc.state?.setupToken ?? takeFragment("token") ?? "";
  const [started, setStarted] = useState(false);

  if (phase === "loading") return <Splash />;
  if (phase === "signedIn") {
    if (!me?.owner) return <Navigate to="/" replace />;
    return <OwnerWizard />;
  }
  if (status?.ownerExists) {
    return (
      <Frame step="owner" route="" title="Sign in to go on">
        <p className="wa-lead">Your account exists. Sign in, and the setup carries on.</p>
        <SignInLink />
      </Frame>
    );
  }
  if (!token.current) return <NoLink />;
  if (!started) {
    return (
      <Frame step="welcome" route="" title="Set up your panel">
        <p className="wa-lead">Run your server from any browser. This takes about 2 minutes.</p>
        <Button kind="primary" onClick={() => setStarted(true)}>Start</Button>
      </Frame>
    );
  }
  return <OwnerStep token={token.current} back={() => setStarted(false)} />;
}

function SignInLink() {
  const nav = useNavigate();
  return <Button kind="primary" onClick={() => nav("/signin?next=/setup")}>Sign in</Button>;
}

function NoLink() {
  return (
    <Frame step="welcome" route="" title="Open your setup link">
      <p className="wa-lead">This page needs the setup link from your server.</p>
      <Details label="Where is it?">
        <p className="wa-note">On the server's computer: in <code>kiln/logs/kiln.log</code>, the line with <code>/setup#token=</code>.</p>
        <p className="wa-note">A rented server: type <code>kilnwarden setup hosted &lt;a name&gt;</code> in its console.</p>
        <p className="wa-note">Or press Open web panel in the Kiln Launcher.</p>
      </Details>
    </Frame>
  );
}

/** The account: a name and a password (twice). The link's token creates it; then this browser is signed in. */
function OwnerStep({ token, back }: { token: string; back: () => void }) {
  const accept = useSession((s) => s.accept);
  const [name, setName] = useState("");
  const [password, setPassword] = useState("");
  const [again, setAgain] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ReactNode | null>(null);
  const short = password !== "" && password.length < 12;
  const differ = again !== "" && again !== password;
  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const r = await ep.setup(token, name.trim(), password);
      // the settings the next pages save ask for a recent proof: the password just typed is that proof
      await ep.reauth({ password }).catch(() => undefined);
      await accept(r);
    } catch (x) {
      if (isApiError(x) && x.code === "invalid_setup_token") setError(<>This setup link was used or is too old. For a new one, type <code>kilnwarden setup</code> in the server console.</>);
      else if (isApiError(x) && x.code === "setup_link_this_machine") setError(<>This link only works on the server's own computer. Over the internet, type <code>kilnwarden setup</code> in the server console for a link that works here.</>);
      else setError(say(x));
    } finally {
      setBusy(false);
    }
  };
  return (
    <Frame step="owner" route="" title="Create your account" back={back}>
      <p className="wa-lead">You sign in with this. There is no default password.</p>
      <form className="wa-form" onSubmit={submit}>
        <TextField label="Your name" value={name} onChange={setName} autoComplete="username" autoFocus name="username" />
        <TextField label="Password" type="password" value={password} onChange={setPassword} autoComplete="new-password" name="password"
          error={short ? "At least 12 characters." : undefined} />
        <TextField label="Password again" type="password" value={again} onChange={setAgain} autoComplete="new-password" name="password-again"
          error={differ ? "The passwords differ." : undefined} />
        {error && <Banner tone="err" title={error} />}
        <Button kind="primary" type="submit" busy={busy} disabled={name.trim().length < 3 || short || !password || password !== again}>Next</Button>
      </form>
    </Frame>
  );
}

/** After the account: the server keeps the place; every page reads its state. */
function OwnerWizard() {
  const [state, setState] = useState<SetupState | null>(null);
  const [failed, setFailed] = useState<string | null>(null);
  const [step, setStep] = useState<StepId | null>(null);
  const [route, setRoute] = useState<Route | "">("");
  const misses = useRef(0);

  const load = async () => {
    try {
      const s = await setup.state();
      misses.current = 0;
      setFailed(null);
      setState(s);
      return s;
    } catch (e) {
      // the listener restarts for a moment when the settings change: only say so when it lasts
      if (++misses.current >= 4) setFailed(say(e));
      return null;
    }
  };

  const initialize = async () => {
    const s = await load();
    if (s) {
      setRoute(s.route || "");
      setStep(resumeAt(s));
    }
    return s;
  };

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      // A listener restart can interrupt the first request too, before a live page starts polling.
      for (let attempt = 0; attempt < 4 && !cancelled; attempt++) {
        if (await initialize()) return;
        if (attempt < 3) await new Promise((resolve) => setTimeout(resolve, 1000));
      }
    })();
    return () => { cancelled = true; };
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // the live pages poll every 2 s
  const live = step === "claim" || step === "progress" || step === "own-progress";
  useEffect(() => {
    if (!live) return;
    const t = setInterval(() => void load(), 2000);
    return () => clearInterval(t);
  }, [live]); // eslint-disable-line react-hooks/exhaustive-deps

  const go = (next: StepId, r: Route | "" = route) => {
    setStep(next);
    setRoute(r);
    void setup.progress({ route: r, step: next }).catch(() => undefined);
  };

  if (!state || !step) {
    return failed ? (
      <Frame step="route" route="" title="The panel did not answer">
        <Banner tone="err" title={failed} />
        <Button onClick={() => void initialize()}>Try again</Button>
      </Frame>
    ) : <Splash />;
  }
  const lost = failed && <Banner tone="warn" title="Lost touch with the panel for a moment">This page keeps trying.</Banner>;
  switch (step) {
    case "route": return <RouteStep state={state} pick={(r) => go(r === "hosted" ? "name" : r === "own" ? "own" : "launcher", r)} skip={() => go("done", "")} />;
    case "name": return <NameStep state={state} back={() => go("route")} next={() => go("claim")} />;
    case "claim": return <ClaimStep state={state} lost={lost} back={() => go("name")} next={() => go("progress")} />;
    case "progress": return <ProgressStep state={state} lost={lost} route="hosted" back={() => go("claim")} next={() => go("discord")} recheck={() => void setup.check().then(load)} />;
    case "own": return <OwnStep state={state} back={() => go("route")} next={() => { void load(); go("own-progress"); }} />;
    case "own-progress": return <ProgressStep state={state} lost={lost} route="own" back={() => go("own")} next={() => go("discord")} recheck={() => void setup.check().then(load)} />;
    case "launcher": return <LauncherStep state={state} back={() => go("route")} next={() => go("discord")} />;
    case "discord": return <DiscordStep state={state} route={route} back={() => go(route === "hosted" ? "progress" : route === "own" ? "own-progress" : route === "launcher" ? "launcher" : "route")} next={() => go("done")} />;
    default: return <DoneStep state={state} route={route} again={() => go("route")} />;
  }
}

// ------------------------------------------------------------------------------------------------ how you open it

const ROUTES: { id: Route; title: string; line: (s: SetupState) => string }[] = [
  { id: "hosted", title: "From anywhere", line: (s) => `At yourname.${s.zone}, on any device. Nothing to set up on your router.` },
  { id: "launcher", title: "With the Kiln Launcher", line: () => "On this PC, or a server you reach over SSH." },
  { id: "own", title: "At my own web address", line: () => "For servers with an open port and a domain name." },
];

function RouteStep({ state, pick, skip }: { state: SetupState; pick: (r: Route) => void; skip: () => void }) {
  const [choice, setChoice] = useState<Route>((state.route || state.suggested) as Route);
  const why = reasonText(state.reason);
  return (
    <Frame step="route" route={choice} title="How will you open your panel?">
      <div className="wa-choices" role="radiogroup" aria-label="How will you open your panel?">
        {ROUTES.map((r) => (
          <label key={r.id} className={"wa-choice" + (choice === r.id ? " on" : "")}>
            <input type="radio" name="route" value={r.id} checked={choice === r.id} onChange={() => setChoice(r.id)} />
            <span className="wa-choice-text">
              <span className="wa-choice-title">{r.title}{r.id === state.suggested && <span className="k-pill k-tone-ember wa-choice-tag">Recommended</span>}</span>
              <span className="wa-choice-line">{r.line(state)}</span>
            </span>
          </label>
        ))}
      </div>
      {why && choice === state.suggested && <p className="wa-note">{why}</p>}
      <Button kind="primary" onClick={() => pick(choice)}>Next</Button>
      <button type="button" className="k-link wa-link-btn" onClick={skip}>Skip setup for now</button>
    </Frame>
  );
}

// ------------------------------------------------------------------------------------------------ hosted access

function NameStep({ state, back, next }: { state: SetupState; back: () => void; next: () => void }) {
  const branding = useSession((s) => s.branding);
  const [typed, setTyped] = useState(() => state.hosted?.name ?? suggestName(branding.name));
  const name = normalizeName(typed, state.zone);
  const problem = typed === "" ? null : nameProblem(name);
  const [touched, setTouched] = useState(false);
  const ok = name !== "" && !nameProblem(name);
  const submit = (e: FormEvent) => {
    e.preventDefault();
    setTouched(true);
    if (ok) {
      sessionStorage.setItem("wa-setup-name", name);
      next();
    }
  };
  return (
    <Frame step="name" route="hosted" title="Pick a name for your panel" back={back}>
      <p className="wa-lead">It becomes your panel's address.</p>
      <form className="wa-form" onSubmit={submit} noValidate>
        <div className="k-field">
          <label className="k-label" htmlFor="wa-name">Name</label>
          <div className="wa-suffix">
            <input id="wa-name" className="k-input k-mono" value={typed} onChange={(e) => setTyped(e.target.value)} onBlur={() => setTouched(true)}
              autoFocus autoComplete="off" spellCheck={false} maxLength={60} aria-describedby="wa-name-hint" aria-invalid={!!problem && touched} />
            <span className="wa-suffix-zone" aria-hidden>.{state.zone}</span>
          </div>
          <div id="wa-name-hint" className={problem && (touched || name.length >= 3) ? "k-error-text" : "k-hint"} role={problem ? "alert" : undefined}>
            {problem && (touched || name.length >= 3) ? problem : ok ? <>Your address: <strong className="k-mono">https://{name}.{state.zone}</strong></> : "Letters, digits and dashes."}
          </div>
        </div>
        <Button kind="primary" type="submit" disabled={!ok}>Next</Button>
      </form>
    </Frame>
  );
}

/** One button: the settings are written, a claim link is made and opened in a new tab; the page notices the claim. */
function ClaimStep({ state, lost, back, next }: { state: SetupState; lost: ReactNode; back: () => void; next: () => void }) {
  const name = sessionStorage.getItem("wa-setup-name") ?? state.hosted?.name ?? "";
  const host = state.hosted?.host && state.hosted.name === name ? state.hosted.host : `${name}.${state.zone}`;
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [opened, setOpened] = useState<string | null>(null);
  const [fallback, setFallback] = useState<string | null>(null);
  const claimed = state.hosted?.host === host && state.hosted.steps?.find((s) => s.id === "claimed")?.state === "done";
  useEffect(() => {
    if (!name) back(); // came back here after a restart without a name: pick it again
    else if (claimed) next();
  }, [claimed, name]); // eslint-disable-line react-hooks/exhaustive-deps
  const claim = async () => {
    setBusy(true);
    setError(null);
    // a tab opened in the click itself (browsers block one opened later); it goes to the claim page once the link is made
    const tab = window.open("about:blank", "_blank");
    try {
      const link = state.hosted?.host === host && !state.hosted.off ? await setup.claim() : await setup.hosted(name);
      if (tab) {
        tab.opener = null;
        tab.location.href = link.claimUrl;
      } else setFallback(link.claimUrl);
      setOpened(link.claimUrl);
    } catch (x) {
      tab?.close();
      setError(isApiError(x) && x.code === "reauth_required" ? "Confirm it's you in the box that opened, then press Claim with Discord again." : say(x));
    } finally {
      setBusy(false);
    }
  };
  return (
    <Frame step="claim" route="hosted" title={opened ? "Waiting for your claim" : "Claim it with Discord"} back={back}>
      {!opened ? (
        <>
          <p className="wa-lead">Sign in with Discord to make <strong className="k-mono">{host}</strong> yours.</p>
          {error && <Banner tone="err" title={error} />}
          <Button kind="primary" busy={busy} onClick={() => void claim()}>Claim with Discord</Button>
          <p className="wa-fine">You need to be in the <a href={KILN_DISCORD} target="_blank" rel="noopener noreferrer">Kiln Discord</a>. Going on accepts the <a href={LE_TERMS} target="_blank" rel="noopener noreferrer">Let's Encrypt terms</a>.</p>
        </>
      ) : (
        <>
          <p className="wa-lead" role="status"><span className="k-spin wa-spin" aria-hidden /> Press Claim on the page that opened. This page moves on by itself.</p>
          {fallback && <p><a className="k-btn k-btn-primary" href={fallback} target="_blank" rel="noopener noreferrer">Open the claim page</a></p>}
          {lost}
          <Details label="It didn't work?">
            <p className="wa-note">The page said the name is taken: <button type="button" className="k-link wa-link-btn" onClick={back}>pick another name</button>.</p>
            <p className="wa-note">"Only for members": <a href={KILN_DISCORD} target="_blank" rel="noopener noreferrer">join the Kiln Discord</a>, then claim again.</p>
            <p className="wa-note">The link expired or was used: <button type="button" className="k-link wa-link-btn" onClick={() => void claim()}>make a new one</button>.</p>
          </Details>
        </>
      )}
    </Frame>
  );
}

function CheckIcon({ s }: { s: SetupStep }) {
  if (s.state === "done") return <span className="wa-check-icon done" aria-hidden>✓</span>;
  if (s.state === "failed") return <span className="wa-check-icon failed" aria-hidden>!</span>;
  if (s.state === "working" || (s.id === "claimed" && s.note === "claim")) return <span className="k-spin wa-spin wa-check-icon" aria-hidden />;
  return <span className="wa-check-icon idle" aria-hidden />;
}

function Checklist({ steps }: { steps: SetupStep[] }) {
  return (
    <ul className="wa-checklist" aria-live="polite">
      {steps.map((s) => (
        <li key={s.id} className={`wa-check ${s.state}`} data-step={s.id} data-state={s.state}>
          <CheckIcon s={s} />
          <span>{checkLabel(s)}</span>
        </li>
      ))}
    </ul>
  );
}

function ProgressStep({ state, lost, route, back, next, recheck }: { state: SetupState; lost: ReactNode; route: "hosted" | "own"; back: () => void; next: () => void; recheck: () => void }) {
  const steps = (route === "hosted" ? state.hosted?.steps : state.own?.steps) ?? [];
  const url = route === "hosted" ? state.hosted?.url : state.own?.url;
  const done = allDone(steps);
  const problem = firstProblem(steps);
  return (
    <Frame step={route === "hosted" ? "progress" : "own-progress"} route={route} title={done ? "Your panel is ready" : "Getting your panel ready"} back={back}>
      <Checklist steps={steps} />
      {problem?.problem && (
        <Banner tone="err" title={problem.problem.title}>
          {problem.problem.fix}
          {problem.problem.detail && <Details><code className="wa-break">{problem.problem.detail}</code></Details>}
        </Banner>
      )}
      {lost}
      {done ? (
        <>
          <p className="wa-lead">It opens at <a className="k-mono" href={url} target="_blank" rel="noopener noreferrer">{url}</a></p>
          <Button kind="primary" onClick={next}>Next</Button>
        </>
      ) : (
        <>
          <p className="wa-note">{problem ? "It keeps trying by itself." : "This takes a minute or two. You can leave this page open."}</p>
          {problem && <Button onClick={recheck}>Check again</Button>}
        </>
      )}
    </Frame>
  );
}

// ------------------------------------------------------------------------------------------------ own address

function OwnStep({ state, back, next }: { state: SetupState; back: () => void; next: () => void }) {
  const [host, setHost] = useState(state.own?.host ?? "");
  const [port, setPort] = useState(String(state.own?.port ?? 443));
  const [challenge, setChallenge] = useState<"http-01" | "tls-alpn-01">(state.own?.challenge === "tls-alpn-01" ? "tls-alpn-01" : "http-01");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const portNo = Number(port);
  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await setup.own(host.trim(), portNo, challenge);
      next();
    } catch (x) {
      setError(isApiError(x) && x.code === "reauth_required" ? "Confirm it's you in the box that opened, then press Next again." : say(x));
    } finally {
      setBusy(false);
    }
  };
  return (
    <Frame step="own" route="own" title="Your own address" back={back}>
      <p className="wa-lead">The name must already point at this server.</p>
      <form className="wa-form" onSubmit={(e) => void submit(e)} noValidate>
        <TextField label="Web address" value={host} onChange={setHost} placeholder="panel.example.com" mono autoFocus spellCheck={false} />
        <TextField label="Port" value={port} onChange={(v) => setPort(v.replace(/[^0-9]/g, ""))} inputMode="numeric" mono hint="The port people open, as your router forwards it." />
        <fieldset className="wa-plain wa-choices" aria-label="How Let's Encrypt checks your address">
          <legend className="k-label">How Let's Encrypt checks it</legend>
          <label className={"wa-choice" + (challenge === "http-01" ? " on" : "")}>
            <input type="radio" name="challenge" checked={challenge === "http-01"} onChange={() => setChallenge("http-01")} />
            <span className="wa-choice-text"><span className="wa-choice-title">Port 80 is open</span><span className="wa-choice-line">The usual way.</span></span>
          </label>
          <label className={"wa-choice" + (challenge === "tls-alpn-01" ? " on" : "")}>
            <input type="radio" name="challenge" checked={challenge === "tls-alpn-01"} onChange={() => setChallenge("tls-alpn-01")} />
            <span className="wa-choice-text"><span className="wa-choice-title">Port 443 reaches the panel</span><span className="wa-choice-line">When port 80 is blocked.</span></span>
          </label>
        </fieldset>
        {error && <Banner tone="err" title={error} />}
        <Button kind="primary" type="submit" busy={busy} disabled={!host.trim() || !(portNo >= 1 && portNo <= 65535)}>Next</Button>
        <p className="wa-fine">Going on accepts the <a href={LE_TERMS} target="_blank" rel="noopener noreferrer">Let's Encrypt terms</a>.</p>
      </form>
    </Frame>
  );
}

// ------------------------------------------------------------------------------------------------ the launcher

function LauncherStep({ state, back, next }: { state: SetupState; back: () => void; next: () => void }) {
  return (
    <Frame step="launcher" route="launcher" title="Use the Kiln Launcher" back={back}>
      <p className="wa-lead">In the Kiln Launcher, go to <strong>Servers</strong>, pick this server, and press <strong>Open web panel</strong>.</p>
      <Button kind="primary" onClick={next}>Next</Button>
      <Details>
        <p className="wa-note">The button needs Kiln {state.launcher} or newer. A server on another computer works through its SSH connection in the launcher.</p>
        {state.address && <p className="wa-note">On this computer, the panel is also at <span className="k-mono">{state.address}</span>.</p>}
      </Details>
    </Frame>
  );
}

// ------------------------------------------------------------------------------------------------ Discord (optional)

function DiscordStep({ state, route, back, next }: { state: SetupState; route: Route | ""; back: () => void; next: () => void }) {
  const [open, setOpen] = useState(false);
  const [guild, setGuild] = useState(state.discord?.guildId ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const available = state.discord?.available !== false;
  useEffect(() => {
    if (!available) next(); // a build without the broker's keys: nothing to offer
  }, [available]); // eslint-disable-line react-hooks/exhaustive-deps
  const save = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await setup.discord(guild.trim());
      next();
    } catch (x) {
      setError(isApiError(x) && x.code === "reauth_required" ? "Confirm it's you in the box that opened, then press Save again." : say(x));
    } finally {
      setBusy(false);
    }
  };
  return (
    <Frame step="discord" route={route} title="Let helpers sign in with Discord?" back={back}>
      <p className="wa-lead">Optional. You can do this later in Panel settings.</p>
      {!open ? (
        <div className="wa-row">
          <Button kind="primary" onClick={next}>Skip</Button>
          <Button onClick={() => setOpen(true)}>Set it up</Button>
        </div>
      ) : (
        <form className="wa-form" onSubmit={(e) => void save(e)} noValidate>
          <TextField label="Your Discord server's ID" value={guild} onChange={(v) => setGuild(v.replace(/[^0-9]/g, ""))} inputMode="numeric" mono autoFocus />
          <Details label="How do I find it?">
            <p className="wa-note">In Discord: Settings &gt; Advanced &gt; Developer Mode on. Then right-click your server and choose Copy Server ID.</p>
            <p className="wa-note">Helpers still need an account here (Users and roles). Each links Discord once, in My account.</p>
          </Details>
          {error && <Banner tone="err" title={error} />}
          <div className="wa-row">
            <Button kind="primary" type="submit" busy={busy} disabled={guild.length < 5}>Save</Button>
            <Button kind="ghost" onClick={next}>Skip</Button>
          </div>
        </form>
      )}
    </Frame>
  );
}

// ------------------------------------------------------------------------------------------------ done

function DoneStep({ state, route, again }: { state: SetupState; route: Route | ""; again: () => void }) {
  const nav = useNavigate();
  const url = route === "hosted" || (route === "" && state.hosted?.ready) ? state.hosted?.url : route === "own" ? state.own?.url : null;
  const here = url && typeof location !== "undefined" && url.startsWith(location.origin);
  const finished = useRef<Promise<unknown> | null>(null);
  useEffect(() => {
    setupSeen();
    if (!state.done && !finished.current) finished.current = setup.done().catch(() => undefined);
  }, [state.done]);
  const toPanel = () => void (finished.current ?? Promise.resolve()).then(() => nav("/", { replace: true }));
  const title = route === "" && !state.hosted?.ready ? "Setup skipped" : "You're all set";
  const where = useMemo(() => route === "launcher" ? "the Kiln Launcher's Open web panel" : url, [route, url]);
  return (
    <Frame step="done" route={route} title={title}>
      {url ? (
        <>
          <p className="wa-lead">Your panel: <strong className="k-mono wa-break">{url}</strong></p>
          <p className="wa-note">Bookmark it. Change anything later in Panel settings.</p>
          {here ? <Button kind="primary" onClick={toPanel}>Go to the panel</Button>
            : <a className="k-btn k-btn-primary" href={url}>Open your panel</a>}
        </>
      ) : (
        <>
          <p className="wa-lead">{route === "launcher" ? <>Open your panel with {where}.</> : "You can run the setup again from Panel settings."}</p>
          <Button kind="primary" onClick={toPanel}>Go to the panel</Button>
        </>
      )}
      {(url && !here) && <button type="button" className="k-link wa-link-btn" onClick={toPanel}>Stay on this address</button>}
      <button type="button" className="k-link wa-link-btn" onClick={again}>Run the setup again</button>
    </Frame>
  );
}
