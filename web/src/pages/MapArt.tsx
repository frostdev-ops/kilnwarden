// The map art (0.1.3): the terrain behind the Map page is the game's own art, which the panel never ships. When the
// server has none, the Map page says so plainly with the one action that fixes it: where the Kiln Launcher runs on this
// same computer and has made its copy, one button copies it here (the server reads only the launcher's fixed folder);
// otherwise the steps, behind Details. The admin pages' own banner never says "Preparing…" here: the panel prepares
// nothing, so the art's state it reports says it cannot make art (useMapArtStatus).
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocation } from "react-router-dom";
import { useSession } from "../app/context";
import { panel } from "../app/panel";
import { SERVER_ID } from "../api/kapSocket";
import type { MapAssetsStatus } from "../api/panel";
import { Banner, Button, explain } from "../ui/kit";

export const MAP_ART_KEY = ["map-assets"];
export const MAP_PATH = `/servers/${SERVER_ID}/admin/map`;

/** The map art's status, polled while a copy runs. */
export function useMapArt(enabled = true) {
  return useQuery({
    queryKey: MAP_ART_KEY, queryFn: panel.mapAssets, enabled, staleTime: 30_000,
    refetchInterval: (q) => (q.state.data?.copy?.state === "copying" ? 1000 : false),
  });
}

/** The admin pages' host hook (main.tsx): the art's state as the Map page reads it. `canExtract: false` always (a
 * panel makes no art), so its banner never says "Preparing map art…"; `extracted` changes when a copy lands, so the
 * map looks for its tiles again. */
export function useMapArtStatus() {
  const q = useMapArt();
  const d = q.data;
  return d ? { map: d.present, extracted: d.present ? `panel:${d.copy?.files ?? 0}` : null, canExtract: false } : { canExtract: false };
}

/** How to add the art by hand, in three steps. */
export function MapArtSteps({ folder }: { folder: string }) {
  return (
    <ol className="wa-list">
      <li>In the Kiln Launcher, open Settings, then Game art. It makes an <span className="k-mono">admin-assets</span> folder from your copy of the game.</li>
      <li>Copy that folder's <span className="k-mono">map</span> and <span className="k-mono">data</span> folders into <span className="k-mono">{folder}</span> in the server's folder.</li>
      <li>Reload the Map page. No restart is needed.</li>
    </ol>
  );
}

/** The fix itself: the copy button where it can work, the copy's progress, else the steps. */
export function MapArtFix({ status }: { status: MapAssetsStatus }) {
  const owner = useSession((s) => s.me?.owner === true);
  const qc = useQueryClient();
  const copy = useMutation({ mutationFn: panel.copyMapAssets, onSettled: () => qc.invalidateQueries({ queryKey: MAP_ART_KEY }) });
  const state = status.copy?.state ?? "idle";
  const canCopy = owner && !!status.launcher;
  return (
    <div className="wa-stack wa-gap">
      {state === "copying" ? <p className="wa-p" role="status"><span className="k-spin wa-spin" aria-hidden /> Copying the map art… {status.copy?.files ?? 0} files so far.</p>
        : canCopy ? <div className="wa-row"><Button kind="primary" busy={copy.isPending} onClick={() => copy.mutate()}>Copy it from the Kiln Launcher</Button></div>
        : !owner ? <p className="wa-p">The server's owner can add it.</p> : null}
      {state === "failed" && status.copy?.error && <Banner tone="err" title={status.copy.error} />}
      {copy.isError && <Banner tone="err" title={explain(copy.error)} />}
      <details className="wa-details">
        <summary>{canCopy ? "Or add it by hand" : "How to add it"}</summary>
        <MapArtSteps folder={status.folder} />
      </details>
    </div>
  );
}

/** Above the Map page when the server has no map art (only there). */
export function MapArtNotice() {
  const loc = useLocation();
  const onMap = loc.pathname === MAP_PATH;
  const q = useMapArt(onMap);
  const d = q.data;
  if (!onMap || !d || d.present) return null;
  return (
    <div className="wa-map-note" data-testid="map-art-note">
      <Banner tone="info" title="This server has no map art yet">
        The map shows players and zones on a plain background until it has.
        <MapArtFix status={d} />
      </Banner>
    </div>
  );
}

/** Whether the Map page shows the notice (the wrapper hides the admin pages' own banner then). */
export function useNoMapArt(): boolean {
  const loc = useLocation();
  const onMap = loc.pathname === MAP_PATH;
  const q = useMapArt(onMap);
  return onMap && !!q.data && !q.data.present;
}
