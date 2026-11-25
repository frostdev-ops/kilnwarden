// The pages before sign-in: sign-in (password, then the two-factor code or a recovery code), the first-run setup from
// the one-time link in kiln.log (/setup#token=...), and the launcher's one-time login (/launcher#secret=...). Secrets
// come in the URL's fragment (never sent to a server, never in a Referer) and are removed from the address bar at once.
import { useEffect, useRef, useState, type FormEvent, type ReactNode } from "react";
import { Navigate, useLocation, useNavigate, useSearchParams } from "react-router-dom";
import { ep, useSession } from "../app/context";
import { isApiError } from "../api/http";
import { useBranding } from "../shell/useBranding";
import { ProviderButtons, useExternalCompletion } from "../identity/ExternalSignIn";
import { Banner, Button, TextField, errorText } from "../ui/kit";

/** The value of `key` in the location's fragment (#token=...&x=...), and the fragment wiped from the address bar. */
export function takeFragment(key: string): string | null {
  const hash = location.hash.replace(/^#/, "");
  if (!hash) return null;
  const v = new URLSearchParams(hash).get(key);
  history.replaceState(history.state, "", location.pathname + location.search);
  return v;
}

function AuthFrame({ title, children, section }: { title: string; children: ReactNode; section: string }) {
  const b = useSession((s) => s.branding);
  useBranding(b, section);
  return (
    <div className="wa-auth">
      <main className="wa-auth-card" aria-labelledby="wa-auth-title">
        <div className="wa-auth-brand">
          {b.logo ? <img className="wa-auth-logo" src={b.logo} alt="" /> : <span className="wa-mark wa-mark-l" aria-hidden />}
          <div className="wa-auth-server">{b.name}</div>
        </div>
        <h1 id="wa-auth-title" className="wa-auth-title">{title}</h1>
        {children}
      </main>
    </div>
  );
}

const wait = (s: number | null) => (s ? ` Try again in ${s >= 60 ? `${Math.ceil(s / 60)} min` : `${s} s`}.` : "");

function useNext() {
  const [params] = useSearchParams();
  const next = params.get("next");
  // only a path on this panel
  return next && next.startsWith("/") && !next.startsWith("//") && !next.startsWith("/signin") ? next : "/";
}

export function SignInPage() {
  const phase = useSession((s) => s.phase);
  const status = useSession((s) => s.status);
  const notice = useSession((s) => s.notice);
  const signIn = useSession((s) => s.signIn);
  const twoFactor = useSession((s) => s.twoFactor);
  const cancelTwoFactor = useSession((s) => s.cancelTwoFactor);
  const next = useNext();
  const [name, setName] = useState("");
  const [password, setPassword] = useState("");
  const [code, setCode] = useState("");
  const [recovery, setRecovery] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const external = useExternalCompletion();

  if (phase === "signedIn") return <Navigate to={next} replace />;
  if (phase === "loading" || external.busy) return <Splash />;

  const run = async (e: FormEvent, f: () => Promise<unknown>) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await f();
    } catch (x) {
      setError(isApiError(x) ? x.message + wait(x.retryAfter) : errorText(x));
    } finally {
      setBusy(false);
    }
  };

  if (phase === "twoFactor") {
    return (
      <AuthFrame title="Two-factor sign-in" section="Sign in">
        <form className="wa-form" onSubmit={(e) => run(e, () => (recovery ? twoFactor(null, code.trim()) : twoFactor(code.replace(/\s/g, ""), null)))}>
          {recovery ? (
            <TextField label="Recovery code" value={code} onChange={setCode} autoComplete="one-time-code" mono autoFocus hint="One of the codes you saved when you set up two-factor. Each works once." />
          ) : (
            <TextField label="Code from your authenticator app" value={code} onChange={setCode} autoComplete="one-time-code" inputMode="numeric" mono autoFocus maxLength={9} placeholder="123 456" />
          )}
          {error && <Banner tone="err" title={error} />}
          <Button kind="primary" type="submit" busy={busy} disabled={!code.trim()}>Sign in</Button>
          <button type="button" className="k-link wa-link-btn" onClick={() => { setRecovery((r) => !r); setCode(""); setError(null); }}>
            {recovery ? "Use the code from my app" : "Use a recovery code instead"}
          </button>
          <button type="button" className="k-link wa-link-btn" onClick={() => { setCode(""); setPassword(""); setError(null); cancelTwoFactor(); }}>Start over</button>
        </form>
      </AuthFrame>
    );
  }

  const providers = (status?.providers ?? []).filter((p): p is { id: string; title: string } => typeof p === "object" && p !== null);
  return (
    <AuthFrame title="Sign in" section="Sign in">
      {notice && <Banner tone="info" title={notice} />}
      {status && !status.ownerExists ? (
        <Banner tone="warn" title="No owner account yet">
          Open the one-time setup link from the server's kiln.log on this machine (it looks like <code>…/setup#token=…</code>), or use "Open web panel" in a Kiln Launcher that knows Kilnwarden.
        </Banner>
      ) : (
        <form className="wa-form" onSubmit={(e) => run(e, () => signIn(name.trim(), password))}>
          <TextField label="Name" value={name} onChange={setName} autoComplete="username" autoFocus name="username" />
          <TextField label="Password" type="password" value={password} onChange={setPassword} autoComplete="current-password" name="password" />
          {(error ?? external.error) && <Banner tone="err" title={error ?? external.error} />}
          <Button kind="primary" type="submit" busy={busy} disabled={!name.trim() || !password}>Sign in</Button>
          <ProviderButtons providers={providers} />
        </form>
      )}
      {!status && <Banner tone="err" title="The panel did not answer">Reload the page in a moment.</Banner>}
    </AuthFrame>
  );
}

export function Splash() {
  return (
    <div className="wa-splash" role="status" aria-live="polite">
      <span className="wa-mark wa-mark-l" aria-hidden />
      <span className="k-sr-only">Loading</span>
    </div>
  );
}

/** Owner name and password with a confirmation; the server checks the rules again (at least 12 characters, ...). */
function OwnerForm({ submit, busy, error }: { submit: (name: string, password: string) => void; busy: boolean; error: string | null }) {
  const [name, setName] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const mismatch = confirm !== "" && confirm !== password;
  const short = password !== "" && password.length < 12;
  return (
    <form className="wa-form" onSubmit={(e) => { e.preventDefault(); submit(name.trim(), password); }}>
      <TextField label="Owner name" value={name} onChange={setName} autoComplete="username" autoFocus hint="3 to 32 letters, digits, dots, dashes or underscores." />
      <TextField label="Password" type="password" value={password} onChange={setPassword} autoComplete="new-password" error={short ? "At least 12 characters." : undefined} />
      <TextField label="Password again" type="password" value={confirm} onChange={setConfirm} autoComplete="new-password" error={mismatch ? "The passwords differ." : undefined} />
      {error && <Banner tone="err" title={error} />}
      <Button kind="primary" type="submit" busy={busy} disabled={!name.trim() || short || !password || password !== confirm}>Create the owner account</Button>
    </form>
  );
}

export function SetupPage() {
  const phase = useSession((s) => s.phase);
  const status = useSession((s) => s.status);
  const accept = useSession((s) => s.accept);
  const nav = useNavigate();
  const loc = useLocation() as { state?: { setupToken?: string } };
  const token = useRef<string | null>(null);
  if (token.current === null) token.current = loc.state?.setupToken ?? takeFragment("token") ?? "";
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (phase === "loading") return <Splash />;
  if (phase === "signedIn") return <Navigate to="/" replace />;
  if (status?.ownerExists) {
    return (
      <AuthFrame title="Set up" section="Setup">
        <Banner tone="info" title="The owner account exists already">Sign in with it.</Banner>
        <Button kind="primary" onClick={() => nav("/signin")}>Go to sign-in</Button>
      </AuthFrame>
    );
  }
  if (!token.current) {
    return (
      <AuthFrame title="Set up" section="Setup">
        <Banner tone="warn" title="This page needs the one-time setup link">
          The server writes it to kiln.log at start while no owner exists. Open it on the server's machine; it works once.
        </Banner>
      </AuthFrame>
    );
  }
  return (
    <AuthFrame title="Create the owner account" section="Setup">
      <p className="wa-lead">The owner can do everything in this panel, including making other accounts. There is no default password: choose one now.</p>
      <OwnerForm busy={busy} error={error} submit={async (name, password) => {
        setBusy(true);
        setError(null);
        try {
          const r = await ep.setup(token.current!, name, password);
          await accept(r);
          nav("/", { replace: true });
        } catch (x) {
          setError(isApiError(x) ? x.message + wait(x.retryAfter) : errorText(x));
        } finally {
          setBusy(false);
        }
      }} />
    </AuthFrame>
  );
}

export function LauncherPage() {
  const accept = useSession((s) => s.accept);
  const phase = useSession((s) => s.phase);
  const nav = useNavigate();
  const secret = useRef<string | null>(null);
  if (secret.current === null) secret.current = takeFragment("secret") ?? "";
  const [error, setError] = useState<string | null>(null);
  const started = useRef(false);
  useEffect(() => {
    if (phase === "loading" || started.current) return;
    started.current = true;
    if (!secret.current) {
      setError("This link has no sign-in secret. Use \"Open web panel\" in the Kiln Launcher again.");
      return;
    }
    ep.launcher(secret.current)
      .then(async (r) => {
        if (!r.ok && r.next === "setup") return nav("/setup", { replace: true, state: { setupToken: r.setupToken } });
        await accept(r);
        nav("/", { replace: true });
      })
      .catch((x) => setError(isApiError(x) ? x.message + wait(x.retryAfter) : errorText(x)));
  }, [phase, accept, nav]);
  return (
    <AuthFrame title="Signing in from the launcher" section="Sign in">
      {error ? (
        <>
          <Banner tone="err" title="The launcher's sign-in did not work">{error} Each link works once, for a minute.</Banner>
          <Button onClick={() => nav("/signin")}>Sign in with a password</Button>
        </>
      ) : (
        <p className="wa-lead" role="status">One moment…</p>
      )}
    </AuthFrame>
  );
}
