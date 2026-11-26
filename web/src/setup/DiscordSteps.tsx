// The setup wizard's Discord pages (0.1.3). First the owner's own sign-in: one button links the owner's Discord (the
// panel's usual link flow: a recent proof, the broker's pinned keys, a one-use state and nonce, one panel account per
// Discord user), after which "Sign in with Discord" works for them and the password stays as a fallback. Discord
// sign-in is switched on for the owner alone if it was off (identity.json discord.ownerOnly), checking the Discord
// server already named or else the Kiln Discord. Then, optionally, helpers: their Discord server's ID, after which
// anyone with an account here who links their Discord may sign in with it. Both pages can be skipped.
import { useEffect, useRef, useState, type FormEvent } from "react";
import { api } from "../app/context";
import { isApiError } from "../api/http";
import { identityEndpoints, safeProviderUrl, type LinkAnswer, takeExternalFragment } from "../identity/external";
import { Banner, Button, TextField } from "../ui/kit";
import { Details, Frame } from "./Frame";
import { discordProblem, setupApi, type Route, type SetupState } from "./wizard";

const setup = setupApi(api);
const identity = identityEndpoints(api);
const KILN_DISCORD = "https://discord.gg/7pjr8SMTfU";

type ExtAnswer = ReturnType<typeof takeExternalFragment>;

/** The answer from Discord is taken once per page load (a re-render must not complete it twice). */
let taken = false;

export function DiscordStep({ state, route, ext, reload, back, next }: {
  state: SetupState; route: Route | ""; ext: ExtAnswer; reload: () => Promise<unknown>; back: () => void; next: () => void;
}) {
  // the Discord server the link checks: the one named already, else the Kiln Discord (the server's choice, /discord/me)
  const d = state.discord;
  const kiln = !d || !!d.kilnGuild || (!d.enabled && !/^[0-9]{5,20}$/.test(d.guildId ?? ""));
  const [busy, setBusy] = useState<"link" | "complete" | null>(ext && "state" in ext && !taken ? "complete" : null);
  const [error, setError] = useState<{ code: string; detail?: string } | null>(ext && "error" in ext && !taken ? { code: ext.error === "expired" ? "ext_expired" : ext.error } : null);
  const [linked, setLinked] = useState(!!state.discord?.linked);
  const available = state.discord?.available !== false;
  const once = useRef(false);

  useEffect(() => {
    if (!available) next(); // a build without the broker's keys: nothing to offer
  }, [available]); // eslint-disable-line react-hooks/exhaustive-deps

  // back from Discord: complete the link from this browser and this session (the server checks both)
  useEffect(() => {
    if (once.current || taken || !ext) return;
    once.current = true;
    taken = true;
    if (!("state" in ext)) return;
    identity.complete<LinkAnswer>(ext.state)
      .then(async () => {
        setLinked(true);
        await reload();
      })
      .catch((x) => setError({ code: isApiError(x) ? x.code : "failed", detail: isApiError(x) ? x.message : undefined }))
      .finally(() => setBusy(null));
  }, [ext]); // eslint-disable-line react-hooks/exhaustive-deps

  const link = async (anyway = false) => {
    setBusy("link");
    setError(null);
    try {
      await setup.discordMe(anyway);
      const { url } = await identity.start("discord", "link", "setup");
      const safe = safeProviderUrl(url);
      if (!safe) throw new Error("the panel returned an unusable address");
      window.location.assign(safe);
    } catch (x) {
      setError({ code: isApiError(x) ? x.code : "failed", detail: isApiError(x) ? x.message : x instanceof Error ? x.message : undefined });
      setBusy(null);
    }
  };

  if (linked) {
    return (
      <Frame step="discord" route={route} title="Your Discord is linked" back={back}>
        <p className="wa-lead">Next time, press <strong>Sign in with Discord</strong>. Your password still works.</p>
        <Button kind="primary" onClick={next}>Next</Button>
      </Frame>
    );
  }
  return (
    <Frame step="discord" route={route} title="Sign in with Discord" back={back} skip={next}>
      <p className="wa-lead">Use it instead of your password. Your password still works.</p>
      {busy === "complete" && <p className="wa-lead" role="status"><span className="k-spin wa-spin" aria-hidden /> Linking…</p>}
      {error && (
        <Banner tone="err" title={discordProblem(error.code, kiln)}>
          {error.code === "not_member" && kiln && <a href={KILN_DISCORD} target="_blank" rel="noopener noreferrer">Join the Kiln Discord</a>}
        </Banner>
      )}
      {busy !== "complete" && <Button kind="primary" busy={busy === "link"} onClick={() => void link()}>{error ? "Try again" : "Link my Discord"}</Button>}
      <Details>
        <p className="wa-note">Discord tells this panel who you are, through frostdev.io. Your Discord password and tokens never reach it.</p>
        <p className="wa-note">{kiln ? "You need to be in the Kiln Discord." : "You need to be in your Discord server."} If Discord's page doesn't open, frostdev.io may be down: come back and skip this.</p>
        {error?.detail && <code className="wa-break">{error.detail}</code>}
        {error?.code === "broker_unreachable" && <button type="button" className="k-link wa-link-btn" onClick={() => void link(true)}>Try Discord anyway</button>}
      </Details>
    </Frame>
  );
}

export function HelpersStep({ state, route, reload, back, next }: { state: SetupState; route: Route | ""; reload: () => Promise<unknown>; back: () => void; next: () => void }) {
  const [open, setOpen] = useState(false);
  const [guild, setGuild] = useState(state.discord?.kilnGuild ? "" : state.discord?.guildId ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const available = state.discord?.available !== false;
  const on = !!state.discord?.enabled && state.discord.ownerOnly === false;
  useEffect(() => {
    if (!available) next();
  }, [available]); // eslint-disable-line react-hooks/exhaustive-deps
  const save = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await setup.discord(guild.trim());
      await reload();
      next();
    } catch (x) {
      setError(isApiError(x) && x.code === "reauth_required" ? "Confirm it's you in the box that opened, then press Save again." : isApiError(x) ? x.message : String(x));
    } finally {
      setBusy(false);
    }
  };
  const title = state.discord?.linked ? "Let others sign in with Discord too?" : "Let helpers sign in with Discord?";
  if (on && !open) {
    return (
      <Frame step="helpers" route={route} title={title} back={back}>
        <p className="wa-lead">It's on, for your Discord server <span className="k-mono">{state.discord?.guildId}</span>.</p>
        <Button kind="primary" onClick={next}>Next</Button>
      </Frame>
    );
  }
  return (
    <Frame step="helpers" route={route} title={title} back={back} skip={next}>
      <p className="wa-lead">Optional. Helpers need an account here first.</p>
      {!open ? (
        <Button kind="primary" onClick={() => setOpen(true)}>Set it up</Button>
      ) : (
        <form className="wa-form" onSubmit={(e) => void save(e)} noValidate>
          <TextField label="Your Discord server's ID" value={guild} onChange={(v) => setGuild(v.replace(/[^0-9]/g, ""))} inputMode="numeric" mono autoFocus />
          <Details label="How do I find it?">
            <p className="wa-note">In Discord: Settings &gt; Advanced &gt; Developer Mode on. Then right-click your server and choose Copy Server ID.</p>
            <p className="wa-note">Each helper links Discord once, in My account. Roles that may sign in are in Panel settings &gt; Sign-in.</p>
          </Details>
          {error && <Banner tone="err" title={error} />}
          <Button kind="primary" type="submit" busy={busy} disabled={guild.length < 5}>Save</Button>
        </form>
      )}
    </Frame>
  );
}
