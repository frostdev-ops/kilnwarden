import { type ReactNode } from "react";
export type Tone = "ok" | "warn" | "err" | "info" | "idle" | "ember";
export interface StatusPillProps {
    tone?: Tone;
    /** Kept for old callers (no animation: nothing glows unless it is hot). */
    pulse?: boolean;
    title?: string;
    children: ReactNode;
}
/** A state word: Connected (ok), Vanilla (info), Updating (warn), Refused (err), Stopped (idle), Running (ember). */
export declare function StatusPill({ tone, title, children }: StatusPillProps): import("react").JSX.Element;
/** A smaller state word (a kind, a tag). */
export declare function Badge({ tone, title, children }: {
    tone?: Tone;
    title?: string;
    children: ReactNode;
}): import("react").JSX.Element;
export declare function Kbd({ children }: {
    children: ReactNode;
}): import("react").JSX.Element;
export interface BannerProps {
    tone?: Tone;
    /** The state word or short lead, in its glaze colour. */
    title?: ReactNode;
    /** One action on the right. */
    actions?: ReactNode;
    /** One sentence. */
    children?: ReactNode;
    /** The player may fold the strip to its title line (Hide / Show); remembered under this id across starts. Give an id
     *  that changes when the notice says something new (a file name, a version), so a new notice opens again. */
    fold?: string;
}
/** A flat Soot 3 strip at the top of the sheet: refusals, warnings, Steam updates. */
export declare function Banner({ tone, title, actions, children, fold }: BannerProps): import("react").JSX.Element;
