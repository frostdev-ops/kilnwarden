// Lane L1b (0.1.1): the relay has scheduled a move or release of this panel's name (asked on the claim pages with the
// Discord account that holds it). It waits 24 hours; until then the name still reaches this server, and the owner can
// undo it here with the panel's relay key, which a Discord account alone can't do. Shown on every page to whoever may
// see the panel's settings (the shell), and in Panel settings > Hosted access.
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { panel } from "../app/panel";
import { useSession } from "../app/context";
import { isApiError } from "../api/http";
import { isClaimPages, scheduledText } from "../api/panel";
import { can } from "../state/session";
import { sentence } from "../ui/format";
import { Banner, Button, explain, when } from "../ui/kit";

export const RELAY_STATUS = ["relay-status"];

export function MoveBanner({ poll = 30_000 }: { poll?: number }) {
  const me = useSession((s) => s.me);
  const view = can(me, "core.settings.view");
  const edit = can(me, "core.settings.edit");
  const qc = useQueryClient();
  const status = useQuery({ queryKey: RELAY_STATUS, queryFn: panel.relay, refetchInterval: poll, enabled: view });
  const [done, setDone] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const undo = useMutation({
    mutationFn: panel.relayUndo,
    onSuccess: (r) => {
      setError(null);
      setDone(r.host);
      void qc.invalidateQueries({ queryKey: RELAY_STATUS });
    },
    onError: (e) => {
      if (isApiError(e) && e.code === "reauth_required") setError("Confirm it is you in the dialog, then press Undo again.");
      else if (isApiError(e) && e.status === 409) {
        setError(sentence(e.message));
        void qc.invalidateQueries({ queryKey: RELAY_STATUS });
      } else setError(`Not undone. ${explain(e)}`);
    },
  });
  const r = status.data;
  if (done)
    return (
      <Banner tone="ok" title={`Undone: ${done} stays with this server.`} actions={<Button size="s" kind="ghost" onClick={() => setDone(null)}>Close</Button>}>
        It is no longer tied to the Discord account that asked, so that account can't ask again. To manage the name on the claim pages again,
        make a claim link (Panel settings &gt; Hosted access) and claim it with a Discord account you trust.
      </Banner>
    );
  const s = r?.enabled ? r.scheduled : null;
  if (!r || !s) return null;
  const pages = isClaimPages(r.namesPage) ? r.namesPage : null;
  return (
    <Banner
      tone="warn"
      title={<>{scheduledText(r.host, s, when(s.at))} Not you?</>}
      actions={edit ? <Button kind="danger" busy={undo.isPending} onClick={() => undo.mutate()}>Undo</Button> : undefined}
    >
      <p className="wa-p">
        {s.to === null ? "The Discord account that holds the name asked for the release" : "The Discord account that holds the name asked to move it to a server with another key"}
        {" "}on {when(s.requested)}. Until then the name stays with this server{edit ? "; Undo keeps it here, with this panel's own key." : "; only the owner can undo it."}
      </p>
      <p className="wa-p">
        If you did not ask for this, undo it, then secure your Discord account: whoever asked was signed in with it.
        {pages && <> You can also see it on the claim pages, <a href={pages} target="_blank" rel="noopener noreferrer">{new URL(pages).host}</a>, signed in with Discord.</>}
      </p>
      {error && <p className="k-error-text wa-p" role="alert">{error}</p>}
    </Banner>
  );
}
