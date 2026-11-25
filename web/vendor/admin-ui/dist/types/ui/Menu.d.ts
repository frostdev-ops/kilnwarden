import { type ReactNode } from "react";
import { type ButtonSize, type ButtonVariant } from "./Button";
export interface MenuItem {
    label: ReactNode;
    icon?: ReactNode;
    onSelect: () => void;
    danger?: boolean;
    disabled?: boolean;
    /** A rule above this item (a group of its own: the danger at the end). */
    sep?: boolean;
}
export declare function Menu({ label, icon, items, align, variant, size }: {
    label: string;
    icon: ReactNode;
    items: MenuItem[];
    align?: "left" | "right";
    variant?: ButtonVariant;
    size?: ButtonSize;
}): import("react").JSX.Element;
/** Mount once (the desktop shell): a right click on a row (a list item, a table row) that holds a Menu opens that menu
 *  at the pointer instead of the webview's own menu. Fields keep their own (copy, paste). */
export declare function useRowContextMenu(): void;
