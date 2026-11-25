export { AdminProvider } from "../api/admin";
export { ConfirmHost } from "./ui";
/** The admin pages (route segment, label (call it: the current language), page). */
export declare const ADMIN_PAGES: readonly [readonly ["players", () => string, (props: object) => import("react").JSX.Element], readonly ["map", () => string, (props: object) => import("react").JSX.Element], readonly ["zones", () => string, (props: object) => import("react").JSX.Element], readonly ["items", () => string, (props: object) => import("react").JSX.Element], readonly ["spawn", () => string, (props: object) => import("react").JSX.Element], readonly ["world", () => string, (props: object) => import("react").JSX.Element], readonly ["combat", () => string, (props: object) => import("react").JSX.Element], readonly ["creatures", () => string, (props: object) => import("react").JSX.Element], readonly ["gamedata", () => string, (props: object) => import("react").JSX.Element], readonly ["permissions", () => string, (props: object) => import("react").JSX.Element], readonly ["content", () => string, (props: object) => import("react").JSX.Element], readonly ["saves", () => string, (props: object) => import("react").JSX.Element], readonly ["crashes", () => string, (props: object) => import("react").JSX.Element]];
/** `/servers/<id>/admin` for absolute links (relative ones break inside the splat route). */
export declare function adminBase(serverId: string): string;
/** Every admin page of the server in the URL (`/servers/:id/admin/*`). */
export declare function AdminRoutes({ serverId }: {
    serverId?: string;
}): import("react").JSX.Element | null;
