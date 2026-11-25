import { type ReactNode } from "react";
import { type PlacementCleanup } from "../api/shared";
export type ToastKind = "ok" | "err" | "info";
export interface ToastAction {
    label: string;
    onClick: () => void;
}
export interface ToastItem {
    id: number;
    kind: ToastKind;
    title: ReactNode;
    detail?: ReactNode;
    actions?: ToastAction[];
    /** Persistent recovery instructions and errors are never evicted by later notifications. */
    persistent?: boolean;
    /** A persistent notice pushed again with the same text shows once, as the newest: how many times it came. */
    count?: number;
    /** The oldest persistent notices beyond MAX_PERSISTENT, folded into this one item, oldest first: its detail holds
     *  every one's title and detail, so nothing a notice said is lost. */
    folded?: ToastItem[];
    /** Runs when the player closes the notice (`close`): shell/notices.ts remembers its sentences as seen then. A notice
     *  that took in others (the same text again, a fold) runs theirs too. */
    onClosed?: () => void;
    /** What the notice says, as keys (shell/notices.ts: game and sentence): `openLines` tells what is open now. */
    lines?: string[];
}
export interface ToastOptions {
    actions?: ToastAction[];
    /** How long it stays (ms; 0: until closed). */
    ms?: number;
    /** Runs when the player closes it (see ToastItem.onClosed). */
    onClosed?: () => void;
    /** Its keys (see ToastItem.lines). */
    lines?: string[];
}
interface ToastStore {
    items: ToastItem[];
    push: (t: Omit<ToastItem, "id" | "persistent" | "count" | "folded">, ms?: number) => number;
    dismiss: (id: number) => void;
    close: (id: number) => void;
}
export declare const useToasts: import("zustand").UseBoundStore<import("zustand").StoreApi<ToastStore>>;
/** The keys (ToastItem.lines) of every notice open now, folded ones too. */
export declare function openLines(): Set<string>;
/** Imperative toasts. `error` takes anything a command rejects with (KilnError) and shows its message. */
export declare const toast: {
    ok: (title: ReactNode, detail?: ReactNode, o?: ToastOptions) => number;
    info: (title: ReactNode, detail?: ReactNode, o?: ToastOptions) => number;
    error: (e: unknown, title?: ReactNode) => number;
    dismiss: (id: number) => void;
};
/** Retained mod folders are a safety decision, not a clean removal. Keep the paths and recovery steps visible
 * until dismissed (also available in the uninstall task's structured result). */
export declare function notifyPlacementCleanup(report: PlacementCleanup | null | undefined): void;
export declare function Toaster(): import("react").JSX.Element;
export {};
