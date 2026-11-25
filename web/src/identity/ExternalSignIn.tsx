// The sign-in page's provider buttons and completion, and My account's "Linked sign-ins" card (docs/lanes/identity.md).
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { api, useSession } from "../app/context";
import { createApi, isApiError } from "../api/http";
import type { SignInAnswer } from "../api/types";
import { Banner, Button, Card, Pill, errorText } from "../ui/kit";
import { externalErrorText, identityEndpoints, PROVIDER_TITLES, safeProviderUrl, takeExternalFragment, type LinkAnswer } from "./external";

// Sign-in calls go through their own client: a "not linked" 401 is an answer, not a lost session.
const publicIdentity = identityEndpoints(createApi());
const signedInIdentity = identityEndpoints(api);

const errText = (x: unknown) => (isApiError(x) ? externalErrorText(x.code) : errorText(x));

function go(url: string) {
  const safe = safeProviderUrl(url);
  if (!safe) throw new Error("the panel returned an unusable provider address");
  window.location.assign(safe);
}

/** Buttons for the active providers (GET /api/auth/status), each starting a sign-in. */
export function ProviderButtons({ providers }: { providers: { id: string; title: string }[] }) {
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  if (providers.length === 0) return null;
  return (
    <div className="wa-providers" role="group" aria-label="Other ways to sign in">
      {providers.map((p) => (
        <Button key={p.id} busy={busy === p.id} disabled={busy !== null} onClick={async () => {
          setBusy(p.id);
          setError(null);
          try {
            go((await publicIdentity.start(p.id, "login")).url);
          } catch (x) {
            setError(errText(x));
            setBusy(null);
          }
        }}>Sign in with {p.title}</Button>
      ))}
      {error && <Banner tone="err" title={error} />}
    </div>
  );
}

/** On /signin: finishes a provider sign-in that came back with #ext=. Returns its state for the page. */
export function useExternalCompletion(): { busy: boolean; error: string | null } {
  const accept = useSession((s) => s.accept);
  const taken = useRef<ReturnType<typeof takeExternalFragment> | undefined>(undefined);
  if (taken.current === undefined) taken.current = takeExternalFragment();
  const [busy, setBusy] = useState(taken.current !== null && "state" in taken.current);
  const [error, setError] = useState<string | null>(taken.current && "error" in taken.current ? externalErrorText(taken.current.error) : null);
  useEffect(() => {
    const t = taken.current;
    if (!t || !("state" in t)) return;
    taken.current = null;
    publicIdentity.complete<SignInAnswer>(t.state)
      .then((r) => accept(r))
      .catch((x) => setError(errText(x)))
      .finally(() => setBusy(false));
  }, [accept]);
  return { busy, error };
}

/** My account: linked Steam/Discord sign-ins, link (after re-authenticating) and unlink. */
export function LinkedSignInsCard() {
  const qc = useQueryClient();
  const links = useQuery({ queryKey: ["identity", "links"], queryFn: signedInIdentity.links });
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState<string | null>(null);
  const taken = useRef<ReturnType<typeof takeExternalFragment> | undefined>(undefined);
  if (taken.current === undefined) taken.current = takeExternalFragment();
  useEffect(() => {
    const t = taken.current;
    if (!t) return;
    taken.current = null;
    if ("error" in t) return setError(externalErrorText(t.error));
    setBusy("complete");
    signedInIdentity.complete<LinkAnswer>(t.state)
      .then((r) => {
        setDone(`${PROVIDER_TITLES[r.linked.provider] ?? r.linked.provider} is linked: you can sign in with it now.`);
        return qc.invalidateQueries({ queryKey: ["identity", "links"] });
      })
      .catch((x) => setError(isApiError(x) && x.code === "reauth_required" ? "Confirm it is you, then link again." : errText(x)))
      .finally(() => setBusy(null));
  }, [qc]);

  const data = links.data;
  if (!data || (data.providers.length === 0 && data.links.length === 0)) return null;
  const linked = new Map(data.links.map((l) => [l.provider, l]));
  const ids = [...new Set([...data.providers.map((p) => p.id), ...data.links.map((l) => l.provider)])];
  const run = async (key: string, f: () => Promise<void>) => {
    setBusy(key);
    setError(null);
    setDone(null);
    try {
      await f();
    } catch (x) {
      // reauth_required opens the shell's "confirm it is you" dialog; the user then clicks again
      setError(isApiError(x) && x.code === "reauth_required" ? "Confirm it is you, then try again." : errText(x));
    } finally {
      setBusy(null);
    }
  };
  return (
    <Card title="Linked sign-ins" sub="Sign in with Steam or Discord instead of your password. Linking or unlinking asks you to confirm it is you.">
      <div className="wa-form">
        {ids.map((id) => {
          const l = linked.get(id);
          const title = PROVIDER_TITLES[id] ?? id;
          const active = data.providers.some((p) => p.id === id);
          return (
            <div key={id} className="wa-row">
              <span>{title}</span>
              {l ? <Pill tone={l.enabled ? "ok" : "idle"}>{l.enabled ? `linked (${l.subject})` : "linked, switched off"}</Pill> : <Pill tone="idle">not linked</Pill>}
              {l ? (
                <Button kind="ghost" busy={busy === id} disabled={busy !== null} onClick={() => run(id, async () => {
                  await signedInIdentity.unlink(id);
                  setDone(`${title} is no longer linked.`);
                  await qc.invalidateQueries({ queryKey: ["identity", "links"] });
                })}>Unlink</Button>
              ) : active ? (
                <Button busy={busy === id} disabled={busy !== null} onClick={() => run(id, async () => go((await signedInIdentity.start(id, "link")).url))}>Link {title}</Button>
              ) : null}
            </div>
          );
        })}
      </div>
      {error && <Banner tone="err" title={error} />}
      {done && !error && <Banner tone="ok" title={done} />}
    </Card>
  );
}
