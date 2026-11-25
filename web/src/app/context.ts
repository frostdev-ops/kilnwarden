// The app's singletons: the API client, the typed endpoints, the session store, and the hook to read it.
import { useStore } from "zustand";
import { createApi } from "../api/http";
import { endpoints } from "../api/endpoints";
import { createSession, type SessionActions, type SessionState } from "../state/session";

export const api = createApi();
export const ep = endpoints(api);
export const session = createSession(ep);

api.onAuthSignal((kind) => void session.getState().authSignal(kind));

export function useSession<T>(select: (s: SessionState & SessionActions) => T): T {
  return useStore(session, select);
}
