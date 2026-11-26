// The routes. Public: /signin, /setup (the setup wizard: #token= before the owner exists, the owner's pages after), /launcher (#secret=). Everything else is behind the sign-in gate, in
// the shell: the overview, the admin pages (@kiln/admin-ui: /servers/this/admin/<page>, Configs at
// /servers/this/configs), the web-only pages (Commands, Player activity, Users and roles, Audit, Panel settings...) and
// extension modules' pages.
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AdminConfigsPage, AdminRoutes, ConfirmHost, Toaster } from "@kiln/admin-ui";
import { useMemo, type ReactNode } from "react";
import { BrowserRouter, Navigate, Route, Routes, useParams } from "react-router-dom";
import { SERVER_ID } from "./api/kapSocket";
import { AccountPage } from "./pages/Account";
import { AuditPage } from "./pages/Audit";
import { CommandsPage } from "./pages/Commands";
import { HomePage } from "./pages/Home";
import { ExtensionPage } from "./ext/ExtensionPage";
import { NotFound } from "./pages/Placeholder";
import { ActivityPage } from "./pages/Activity";
import { SettingsPage } from "./pages/Settings";
import { MetricsPage } from "./pages/Metrics";
import { PublicPageSettingsPage } from "./pages/PublicPageSettings";
import { WebhooksPage } from "./pages/Webhooks";
import { LauncherPage, SignInPage } from "./pages/Public";
import { SetupWizard } from "./setup/SetupWizard";
import { SetupPending } from "./setup/SetupPending";
import { ModsPage } from "./pages/Mods";
import { SavedCommandsPage } from "./pages/SavedCommands";
import { SchedulesPage } from "./pages/Schedules";
import { UsersPage } from "./pages/Users";
import { AdminArea, RequireSignIn } from "./shell/Gate";
import { useSession } from "./app/context";
import { Reauth } from "./shell/Reauth";
import { Shell } from "./shell/Shell";

/** The panel administers one server: another id in the URL goes to the overview. */
function OnlyThisServer({ children }: { children: ReactNode }) {
  return useParams().id === SERVER_ID ? <>{children}</> : <Navigate to="/" replace />;
}



export function App() {
  const handle = useSession(s => s.phase === "signedIn" ? `${s.me?.session.handle}:${s.signIns}` : "signed-out");
  // Scope cached user/audit/admin results to this sign-in, never the next browser user.
  const qc = useMemo(() => new QueryClient({ defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false } } }), [handle]);
  const reauthNeeded = useSession(s => s.reauthNeeded);
  const signedIn = useSession(s => s.phase === "signedIn");
  return (
    <QueryClientProvider client={qc}>
      <BrowserRouter>
        <Routes>
          <Route path="/signin" element={<SignInPage />} />
          <Route path="/setup" element={<SetupWizard />} />
          <Route path="/launcher" element={<LauncherPage />} />
          <Route element={<RequireSignIn><Shell /></RequireSignIn>}>
            <Route index element={<SetupPending><HomePage /></SetupPending>} />
            <Route path={`servers/${SERVER_ID}/admin/*`} element={<AdminArea><div className="wa-admin"><AdminRoutes serverId={SERVER_ID} /></div></AdminArea>} />
            {/* AdminConfigsPage takes its server from the route's :id */}
            <Route path="servers/:id/configs" element={<OnlyThisServer><AdminArea><div className="wa-admin"><AdminConfigsPage /></div></AdminArea></OnlyThisServer>} />
            <Route path={`servers/${SERVER_ID}/mods`} element={<Navigate to="/mods" replace />} />
            <Route path={`servers/${SERVER_ID}/console`} element={<Navigate to="/commands" replace />} />
            <Route path="servers/:id/*" element={<Navigate to="/" replace />} />
            <Route path="commands" element={<AdminArea><CommandsPage /></AdminArea>} />
            <Route path="users" element={<UsersPage />} />
            <Route path="audit" element={<AuditPage />} />
            <Route path="account" element={<AccountPage />} />
            <Route path="mods" element={<ModsPage />} />
            <Route path="schedules" element={<SchedulesPage />} />
            <Route path="saved-commands" element={<SavedCommandsPage />} />
            <Route path="metrics" element={<MetricsPage />} />
            <Route path="webhooks" element={<WebhooksPage />} />
            <Route path="public-page" element={<PublicPageSettingsPage />} />
            <Route path="activity" element={<ActivityPage />} />
            <Route path="settings" element={<SettingsPage />} />
            {/* extension modules' declarative and custom pages; unknown kinds say so there */}
            <Route path="m/:module/:page" element={<ExtensionPage />} />
            <Route path="*" element={<NotFound />} />
          </Route>
        </Routes>
        {reauthNeeded && signedIn && <Reauth />}
        <ConfirmHost />
        <Toaster />
      </BrowserRouter>
    </QueryClientProvider>
  );
}
