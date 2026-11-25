import { type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from "react";
export interface FieldProps {
    label?: ReactNode;
    hint?: ReactNode;
    error?: ReactNode;
    /** Badges next to the label (restart, synced). */
    badges?: ReactNode;
    /** Id of the control (for the label); generated when absent. */
    htmlFor?: string;
    children: ReactNode;
}
export declare function Field({ label, hint, error, badges, htmlFor, children }: FieldProps): import("react").JSX.Element;
export interface TextInputProps extends InputHTMLAttributes<HTMLInputElement> {
    invalid?: boolean;
    mono?: boolean;
}
export declare const TextInput: import("react").ForwardRefExoticComponent<TextInputProps & import("react").RefAttributes<HTMLInputElement>>;
export declare const TextArea: import("react").ForwardRefExoticComponent<TextareaHTMLAttributes<HTMLTextAreaElement> & {
    invalid?: boolean;
} & import("react").RefAttributes<HTMLTextAreaElement>>;
export interface SelectProps extends Omit<SelectHTMLAttributes<HTMLSelectElement>, "onChange"> {
    options: (string | {
        value: string;
        label: ReactNode;
    })[];
    onChange: (value: string) => void;
    invalid?: boolean;
}
export declare const Select: import("react").ForwardRefExoticComponent<SelectProps & import("react").RefAttributes<HTMLSelectElement>>;
export interface NumberInputProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "value" | "onChange" | "min" | "max" | "step"> {
    value: number | null;
    /** Called on blur / Enter with a parsed number (not on every key). */
    onCommit: (value: number) => void;
    integer?: boolean;
    min?: number;
    max?: number;
    step?: number;
    invalid?: boolean;
}
/** A number box that commits on blur or Enter; keeps the text while typing. */
export declare function NumberInput({ value, onCommit, integer, min, max, step, invalid, className, ...rest }: NumberInputProps): import("react").JSX.Element;
export interface SliderProps {
    value: number;
    min: number;
    max: number;
    step?: number;
    integer?: boolean;
    onCommit: (value: number) => void;
    disabled?: boolean;
    id?: string;
    "aria-label"?: string;
}
/** Range + number box. The range commits on release; the box on blur/Enter. */
export declare function Slider({ value, min, max, step, integer, onCommit, disabled, id, ...rest }: SliderProps): import("react").JSX.Element;
export interface ToggleProps {
    checked: boolean;
    onChange: (checked: boolean) => void;
    label?: ReactNode;
    hint?: ReactNode;
    disabled?: boolean;
    id?: string;
    "aria-label"?: string;
}
export declare function Toggle({ checked, onChange, label, hint, disabled, id, ...rest }: ToggleProps): import("react").JSX.Element;
export interface WellInputProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "size"> {
    /** The small-caps word inside the well ("Filter", "Search items"). */
    label: ReactNode;
    /** Text after the input (a count). */
    note?: ReactNode;
    compact?: boolean;
    wellStyle?: React.CSSProperties;
    /** A filter box (controlled `value`): while it has text, a clear (X) button at its end, and Esc in it empties it;
     *  both leave the focus in the box. Esc in an empty box is left to the page (a dialog closes, as before). */
    onClear?: () => void;
    /** The clear button's name (and tooltip); default "Clear <label>" ("Clear filter"). */
    clearLabel?: string;
}
/** True when this Esc belongs to a filter box with text (a WellInput with onClear; its input carries
 *  data-esc-clears): the box empties itself, so a listener on the window that sees keys before React does
 *  (ui/Modal.tsx) must leave it alone. An empty box, a modifier or an IME composition: false, and Esc does what it did
 *  before. */
export declare function escClearsFilter(e: Pick<KeyboardEvent, "key" | "ctrlKey" | "altKey" | "metaKey" | "shiftKey" | "isComposing" | "target">): boolean;
/** An input set in a well with its label inside it. */
export declare const WellInput: import("react").ForwardRefExoticComponent<WellInputProps & import("react").RefAttributes<HTMLInputElement>>;
/** The flame toggle of mod lists: lit (on) or out. `label` is the mod's name. `asSwitch`: an on/off switch named by the
 *  mod (the main list), not a "Light" button. */
export declare function FlameToggle({ on, onChange, label, disabled, asSwitch, title }: {
    on: boolean;
    onChange: (on: boolean) => void;
    label: string;
    disabled?: boolean;
    asSwitch?: boolean;
    title?: string;
}): import("react").JSX.Element;
