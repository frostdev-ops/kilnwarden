// The owner whose setup is not finished comes back to it (0.1.2): the overview sends them to /setup, once per page load.
// Everyone else, and an owner who finished or skipped it, sees the overview.
import { useQuery } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { Navigate } from "react-router-dom";
import { api, useSession } from "../app/context";
import { setupApi } from "./wizard";

const setup = setupApi(api);
let shown = false;

/** The wizard was finished or skipped in this page load: the overview stays the overview. */
export function setupSeen() {
  shown = true;
}

export function SetupPending({ children }: { children: ReactNode }) {
  const owner = useSession((s) => s.me?.owner === true);
  const q = useQuery({ queryKey: ["setup-state"], queryFn: setup.state, enabled: owner && !shown, staleTime: 60_000, retry: 0 });
  if (owner && !shown && q.data && !q.data.done) {
    shown = true;
    return <Navigate to="/setup" replace />;
  }
  return <>{children}</>;
}
