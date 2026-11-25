import { type ButtonHTMLAttributes, type ReactNode } from "react";
export type ButtonVariant = "primary" | "molten" | "secondary" | "ghost" | "danger" | "danger-fill";
export type ButtonSize = "s" | "m" | "l";
export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
    variant?: ButtonVariant;
    /** s: 32 px (table toolbars), m: 38 px, l: 44 px. */
    size?: ButtonSize;
    /** Shown only when the button has no words (IconButton). */
    icon?: ReactNode;
    /** Shows a spinner and disables the button. */
    loading?: boolean;
}
export declare const Button: import("react").ForwardRefExoticComponent<ButtonProps & import("react").RefAttributes<HTMLButtonElement>>;
export interface IconButtonProps extends Omit<ButtonProps, "children"> {
    /** Accessible name, also the tooltip. */
    label: string;
}
export declare const IconButton: import("react").ForwardRefExoticComponent<IconButtonProps & import("react").RefAttributes<HTMLButtonElement>>;
/** Only inside a busy button. */
export declare function Spinner({ size }: {
    size?: number;
}): import("react").JSX.Element;
