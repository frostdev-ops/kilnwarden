// The signed-in frame: the module switcher (every module's pages the user may see, from /api/core/modules), the server's
// name and logo, the account menu, and the page. On a phone the switcher is a drawer behind the menu button.
import { useQuery } from "@tanstack/react-query";
import { reconnectAdmin, useAdminHub } from "@kiln/admin-ui";
import { useEffect, useMemo, useRef, useState } from "react";
import { Link, NavLink, Outlet, useLocation } from "react-router-dom";
import { ep, useSession } from "../app/context";
import { SERVER_ID } from "../api/kapSocket";
import { buildNav, isActive, type NavItem } from "../state/nav";
import { NavIcon } from "./icons";
import { useBranding } from "./useBranding";

const GROUPS: { id: NavItem["group"]; title: string }[] = [
  { id: "server", title: "Server" },
  { id: "panel", title: "Panel" },
];

export function Shell() {
  const branding = useSession((s) => s.branding);
  const me = useSession((s) => s.me)!;
  const signIns = useSession((s) => s.signIns);
  const loc = useLocation();
  const [open, setOpen] = useState(false);
  const modules = useQuery({ queryKey: ["modules", me.id, signIns], queryFn: ep.modules, staleTime: 60_000 });
  const nav = useMemo(() => buildNav(modules.data ?? []), [modules.data]);
  const current = nav.find((n) => isActive(n, loc.pathname));
  useBranding(branding, current?.title);

  // after a new sign-in, an admin session that ended (4401) connects again
  const firstSignIns = useRef(signIns);
  useEffect(() => {
    if (signIns === firstSignIns.current) return;
    if (useAdminHub.getState().conns[SERVER_ID]?.status === "error") reconnectAdmin(SERVER_ID);
  }, [signIns]);

  useEffect(() => setOpen(false), [loc.pathname]);
  // Escape closes the phone drawer and gives the focus back to its button
  useEffect(() => {
    if (!open) return;
    const key = (e: KeyboardEvent) => {
      if (e.key !== "Escape" || document.querySelector(".wa-modal")) return;
      setOpen(false);
      document.querySelector<HTMLElement>(".wa-menu-btn")?.focus();
    };
    document.addEventListener("keydown", key);
    return () => document.removeEventListener("keydown", key);
  }, [open]);

  return (
    <div className={"wa-shell" + (open ? " wa-nav-open" : "")}>
      <a className="wa-skip" href="#main" onClick={(e) => { e.preventDefault(); document.getElementById("main")?.focus(); }}>Skip to the page</a>
      <header className="wa-topbar">
        <button type="button" className="k-btn k-btn-ghost wa-menu-btn" aria-label="Menu" aria-expanded={open} aria-controls="wa-nav" onClick={() => setOpen((o) => !o)}>
          <span className="wa-burger" aria-hidden />
        </button>
        <Link to="/" className="wa-brand">
          {branding.logo ? <img className="wa-logo" src={branding.logo} alt="" /> : <span className="wa-mark" aria-hidden />}
          <span className="wa-brand-name">{branding.name}</span>
        </Link>
        <span className="k-spacer" />
        <AccountMenu />
      </header>
      <nav id="wa-nav" className="wa-nav" aria-label="Modules">
        <NavLink to="/" end className={({ isActive: a }) => "wa-nav-item" + (a ? " active" : "")}>
          <NavIcon name="server" /> <span>Overview</span>
        </NavLink>
        {GROUPS.map((g) => {
          const items = nav.filter((n) => n.group === g.id);
          if (!items.length) return null;
          return (
            <div key={g.id} className="wa-nav-group">
              <div className="wa-nav-title k-sc">{g.title}</div>
              {items.map((n) => (
                <NavLink key={n.key} to={n.path} className={() => "wa-nav-item" + (isActive(n, loc.pathname) ? " active" : "")}>
                  <NavIcon name={n.icon} path={n.path} /> <span>{n.title}</span>
                </NavLink>
              ))}
            </div>
          );
        })}
        {modules.isLoading && <div className="wa-nav-note k-muted" role="status">Loading the pages…</div>}
        {modules.isError && (
          <div className="wa-nav-note">
            <p className="k-error-text wa-p">The page list did not load.</p>
            <button type="button" className="k-btn k-btn-s" onClick={() => void modules.refetch()}>Try again</button>
          </div>
        )}
      </nav>
      <div className="wa-scrim" onClick={() => setOpen(false)} aria-hidden />
      <main className="wa-main" id="main" tabIndex={-1}>
        <Outlet />
      </main>
    </div>
  );
}

function AccountMenu() {
  const me = useSession((s) => s.me)!;
  const signOut = useSession((s) => s.signOut);
  const [open, setOpen] = useState(false);
  const box = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const close = (e: MouseEvent) => {
      if (!box.current?.contains(e.target as Node)) setOpen(false);
    };
    const key = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("mousedown", close);
    document.addEventListener("keydown", key);
    return () => {
      document.removeEventListener("mousedown", close);
      document.removeEventListener("keydown", key);
    };
  }, [open]);
  return (
    <div className="wa-account" ref={box}>
      <button type="button" className="k-btn k-btn-ghost wa-account-btn" aria-haspopup="menu" aria-expanded={open} onClick={() => setOpen((o) => !o)}>
        <span className="wa-avatar" aria-hidden>{me.name.slice(0, 1).toUpperCase()}</span>
        <span className="wa-account-name">{me.name}</span>
      </button>
      {open && (
        <div className="k-popover wa-menu" role="menu">
          <div className="wa-menu-head">
            <strong>{me.name}</strong>
            <span className="k-muted">{me.roles.join(", ")}{me.twoFactor ? " · 2FA on" : ""}</span>
          </div>
          <Link role="menuitem" className="k-menu-item" to="/account" onClick={() => setOpen(false)}>My account</Link>
          <Link role="menuitem" className="k-menu-item" to="/account#sessions" onClick={() => setOpen(false)}>Sessions</Link>
          <Link role="menuitem" className="k-menu-item" to="/account#two-factor" onClick={() => setOpen(false)}>Two-factor sign-in</Link>
          <div className="k-menu-sep" />
          <button role="menuitem" type="button" className="k-menu-item" onClick={() => void signOut()}>Sign out</button>
        </div>
      )}
    </div>
  );
}
