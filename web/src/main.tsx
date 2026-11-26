// The web app's entry: the admin pages' transport (KAP/1 over the panel's signed-in WebSocket), the session, the app.
import { configureAdmin, createWebSocketTransport } from "@kiln/admin-ui";
import "@kiln/admin-ui/style.css";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { session } from "./app/context";
import { kapUrl, trackingWebSocket } from "./api/kapSocket";
import { App } from "./App";
import { useMapArtStatus } from "./pages/MapArt";
import "./app.css";

// the map art and catalogs: the owner's copy of the launcher's admin assets, served by the panel (MapAssetsComponent);
// its state as the Map page reads it (0.1.3: a panel never prepares art, so the page never says "Preparing…")
const HOST = { assetsRoot: () => "/api/core/map-assets/", useArtStatus: useMapArtStatus };

const transport = createWebSocketTransport({
    url: () => kapUrl(),
    WebSocket: trackingWebSocket((code, reason) => void session.getState().socketClosed(code, reason), undefined, () => void session.getState().authSignal("reauth")),
});
// A new identity/session must never inherit the previous user's socket, state or command permissions.
let identity = "";
session.subscribe(s => {
  const next = s.phase === "signedIn" && !s.me?.restriction ? `${s.me!.session.handle}:${s.signIns}:${s.me!.roles.join(",")}` : "";
  if (next === identity) return;
  identity = next;
  configureAdmin({ transport: next ? transport : null, host: HOST });
});
configureAdmin({ transport: null, host: HOST });

void session.getState().boot();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
