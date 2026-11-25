// The module and page icons the server names (lucide names, ModuleSpec.Icon) as lucide-react components; a fixed set,
// so the bundle carries only these. Unknown names get a dot.
import {
  Activity, BookOpen, Bookmark, Bug, CalendarClock, Circle, Database, Globe, Hammer, History, Key, KeyRound, LogIn, Map as MapIcon, Package, PackageCheck,
  Puzzle, Save, Scale, Scroll, ScrollText, Server, Settings, Shield, ShieldCheck, Sliders, Sparkles, Swords, Terminal, TrendingUp, User, Users, UsersRound,
  Webhook, type LucideIcon,
} from "lucide-react";

const ICONS: Record<string, LucideIcon> = {
  activity: Activity, "book-open": BookOpen, bookmark: Bookmark, bug: Bug, "calendar-clock": CalendarClock, database: Database, globe: Globe, hammer: Hammer,
  history: History, key: Key, "key-round": KeyRound, "log-in": LogIn, map: MapIcon, package: Package, "package-check": PackageCheck, puzzle: Puzzle, save: Save,
  scale: Scale, scroll: Scroll, "scroll-text": ScrollText, server: Server, settings: Settings, shield: Shield, "shield-check": ShieldCheck, sliders: Sliders,
  sparkles: Sparkles, swords: Swords, terminal: Terminal, "trending-up": TrendingUp, user: User, users: Users, "users-round": UsersRound, webhook: Webhook,
};

/** Page ids whose module icon is not the page's own (players' map). */
const BY_PATH: Record<string, LucideIcon> = { "/servers/this/admin/map": MapIcon, "/servers/this/admin/crashes": Bug, "/commands": Terminal, "/metrics": TrendingUp };

export function NavIcon({ name, path }: { name: string; path?: string }) {
  const I = (path && BY_PATH[path]) || ICONS[name] || Circle;
  return <I aria-hidden className="wa-icon" size={18} strokeWidth={1.75} />;
}
