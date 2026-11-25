import { type ReactNode } from "react";
export interface ModalProps {
    open: boolean;
    onClose: () => void;
    title: ReactNode;
    children?: ReactNode;
    footer?: ReactNode;
    /** px, default 520 (working dialogs: 480-640). */
    width?: number;
    /** Backdrop click and Esc do nothing (a task is running). */
    locked?: boolean;
    /** The pointed arch: a warning or a typed confirmation. */
    arch?: boolean;
}
/** Sent on `window` when a dialog starts to cover the page. */
export declare const COVERED = "k-covered";
/** While a dialog covers the page, `<html data-covered>` is set (counted: dialogs can open over dialogs). WebKitGTK
 *  paints the page's thin overlay scrollbars after everything else, so the mods list's scrollbar was drawn across a
 *  confirmation (the owner's report of 13 Nov 2026); theme/base.css hides the page's scrollbars while it is set. */
export declare function useCoverPage(open: boolean): void;
export declare function Modal({ open, onClose, title, children, footer, width, locked, arch }: ModalProps): import("react").ReactPortal | null;
/** The text block of an arch dialog (one to three lines). */
export declare function ArchText({ children }: {
    children: ReactNode;
}): import("react").JSX.Element;
export interface ConfirmDangerProps {
    open: boolean;
    onClose: () => void;
    title: ReactNode;
    /** One or two short lines: what happens. */
    message?: ReactNode;
    /** The word the user types to confirm (e.g. the profile id, "switch"). */
    word: string;
    /** Button label, e.g. "Delete". */
    action: string;
    /** Report action failures to the user, then reject to keep this dialog open for retry. */
    onConfirm: () => void | Promise<void>;
    /** Keeps the action disabled (e.g. while what the message names is still loading). */
    disabled?: boolean;
    /** This confirmation is a plain button unless the player turned typing on in Settings (removing a mod: the word is
     * the mod's id, which can be sixty characters long). Not for uninstalling, profiles, servers or saves. */
    relaxable?: boolean;
}
/** Settings: "Type a mod's name to remove it" (off unless turned on; kept per launcher window, like other view choices).
 *  Removing a mod is one click with a plain confirmation by default. */
export declare function typeToConfirm(): boolean;
export declare function setTypeToConfirm(on: boolean): void;
/** A destructive action behind a typed confirmation, in the arch. The danger fill stays disabled until the word matches. */
export declare function ConfirmDanger({ open, onClose, title, message, word, action, onConfirm, disabled, relaxable }: ConfirmDangerProps): import("react").JSX.Element;
