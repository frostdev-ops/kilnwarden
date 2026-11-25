// What stands between a visitor and the panel: the session's phase (loading, signed out -> /signin?next=...), a
// restricted session (only the page that fixes it: a new password, two-factor setup, or signing in again with the
// code) and the admin pages' "not allowed" view (the KAP socket closed with 4403).
import type { ReactNode } from "react";
import { reconnectAdmin } from "@kiln/admin-ui";
import { Navigate, useLocation } from "react-router-dom";
import { useSession } from "../app/context";
import { SERVER_ID } from "../api/kapSocket";
import { PasswordCard, TwoFactorCard } from "../pages/Account";
import { Splash } from "../pages/Public";
import { Banner, Button, Page } from "../ui/kit";

export function RequireSignIn({ children }: { children: ReactNode }) {
  const phase = useSession((s) => s.phase);
  const restriction = useSession((s) => s.me?.restriction ?? null);
  const loc = useLocation();
  if (phase === "loading") return <Splash />;
  if (phase !== "signedIn") {
    const next = loc.pathname + loc.search;
    return <Navigate to={next === "/" ? "/signin" : `/signin?next=${encodeURIComponent(next)}`} replace />;
  }
  if (restriction) return <Restricted />;
  return <>{children}</>;
}

function Restricted() {
  const me = useSession((s) => s.me)!;
  const signOut = useSession((s) => s.signOut);
  return (
    <div className="wa-restricted">
      <Page title={`Welcome, ${me.name}`} actions={<Button kind="ghost" onClick={() => void signOut()}>Sign out</Button>}>
        {me.restriction === "password_change_required" && (
          <>
            <Banner tone="warn" title="Choose your own password">Your account was made (or reset) with a temporary password. Change it to use the panel.</Banner>
            <PasswordCard required />
          </>
        )}
        {me.restriction === "2fa_setup_required" && <TwoFactorCard required />}
        {me.restriction === "2fa_signin_required" && (
          <Banner tone="warn" title="Sign in again with your two-factor code" actions={<Button kind="primary" onClick={() => void signOut()}>Sign out</Button>}>
            One of your roles now requires two-factor sign-in, and this session was opened without it.
          </Banner>
        )}
      </Page>
    </div>
  );
}

/** The admin pages, unless their socket was refused (4403): then why, and a way to try again. */
export function AdminArea({ children }: { children: ReactNode }) {
  const notAllowed = useSession((s) => s.notAllowed);
  const clear = useSession((s) => s.clearNotAllowed);
  if (!notAllowed) return <>{children}</>;
  return <NotAllowed reason={notAllowed} retry={() => { clear(); reconnectAdmin(SERVER_ID); }} />;
}

export function NotAllowed({ reason, retry }: { reason: string; retry?: () => void }) {
  return (
    <Page title="Not allowed">
      <Banner tone="err" title="The server does not let your account use this" actions={retry && <Button onClick={retry}>Try again</Button>}>
        {reason}
      </Banner>
      <p className="k-muted wa-gap">Ask the server's owner for a role that has it. Pages you may use are in the list.</p>
    </Page>
  );
}
