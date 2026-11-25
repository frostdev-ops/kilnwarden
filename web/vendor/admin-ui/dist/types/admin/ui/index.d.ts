import { type ReactNode } from "react";
import type { KilnError } from "../../api/shared";
export { Button, IconButton, Spinner } from "../../ui/Button";
export type { ButtonProps, ButtonVariant, ButtonSize, IconButtonProps } from "../../ui/Button";
export { Card, CardLevel, Section, Panel, PageHeader, Page, SplitPage, KeyValue } from "../../ui/Card";
export type { CardProps, PanelProps, PageHeaderProps, KeyValueRow } from "../../ui/Card";
export { StatusPill, Badge, Kbd, Banner } from "../../ui/StatusPill";
export type { Tone, StatusPillProps, BannerProps } from "../../ui/StatusPill";
export { EmptyState, Skeleton } from "../../ui/EmptyState";
export { Tabs, NavTabs, Segmented } from "../../ui/Tabs";
export type { TabItem, NavTabItem } from "../../ui/Tabs";
export { Table, NameId } from "../../ui/Table";
export type { Column, TableProps } from "../../ui/Table";
export { Field, TextInput, TextArea, Select, NumberInput, Slider, Toggle, WellInput, FlameToggle, escClearsFilter } from "../../ui/Form";
export type { FieldProps, ToggleProps, SliderProps, SelectProps, NumberInputProps, WellInputProps } from "../../ui/Form";
export { Modal, ConfirmDanger, ArchText, typeToConfirm, setTypeToConfirm } from "../../ui/Modal";
export type { ModalProps, ConfirmDangerProps } from "../../ui/Modal";
export { toast, Toaster, useToasts, openLines } from "../../ui/Toast";
export type { ToastAction, ToastOptions } from "../../ui/Toast";
export { LogView, classifyLine } from "../../ui/LogView";
export type { LogViewProps, LogLevel } from "../../ui/LogView";
export { JsonView } from "../../ui/JsonView";
export { SchemaForm, parseDuration, getPath, checkValue } from "../../ui/SchemaForm";
export type { SchemaFormProps } from "../../ui/SchemaForm";
export { SortableList } from "../../ui/SortableList";
export { Menu, useRowContextMenu } from "../../ui/Menu";
export type { MenuItem } from "../../ui/Menu";
export { PathText } from "../../ui/PathText";
export { fmtBytes, fmtAgo, fmtTime, shortHash, errorText, installNote, splitPath } from "../../ui/format";
export declare const cx: (...c: (string | false | null | undefined)[]) => string;
export declare const toastOk: (title: ReactNode, detail?: ReactNode) => number;
export declare const toastError: (e: unknown, title?: ReactNode) => number;
interface ConfirmReq {
    title: ReactNode;
    body?: ReactNode;
    ok?: string;
    danger?: boolean;
    /** the user types this word to confirm (ConfirmDanger) */
    typed?: string;
    prompt?: {
        placeholder?: string;
        initial?: string;
        maxLength?: number;
        label?: ReactNode;
    };
    resolve: (v: string | false) => void;
}
/** Asks; resolves true when confirmed. `typed` makes it a typed confirmation (ConfirmDanger). */
export declare function confirm(o: Omit<ConfirmReq, "resolve" | "prompt">): Promise<boolean>;
/** Asks for a short text (waypoint names, ...); resolves to it, or null. */
export declare function promptText(o: Omit<ConfirmReq, "resolve" | "typed"> & {
    prompt: NonNullable<ConfirmReq["prompt"]>;
}): Promise<string | null>;
/** Mounted once by AdminLayout: shows confirm() / promptText() requests. */
export declare function ConfirmHost(): import("react").JSX.Element;
export declare function ErrorBanner({ error, title }: {
    error: KilnError | null | undefined;
    title?: ReactNode;
}): import("react").JSX.Element | null;
export declare function HealthBar({ health, max, wide }: {
    health?: number;
    max?: number;
    wide?: boolean;
}): import("react").JSX.Element | null;
export declare function Stat({ k, v, s }: {
    k: ReactNode;
    v: ReactNode;
    s?: ReactNode;
}): import("react").JSX.Element;
export declare const Stats: ({ children }: {
    children: ReactNode;
}) => import("react").JSX.Element;
export declare const initials: (n: string) => string;
export declare function Avatar({ name, online }: {
    name: string;
    online?: boolean;
}): import("react").JSX.Element;
export interface Suggestion<T> {
    key: string;
    label: ReactNode;
    sub?: ReactNode;
    value: T;
}
/** A text input with a suggestion list under it: arrows + Enter/Tab pick, Escape closes. */
export declare function SuggestInput<T>({ value, onChange, suggestions, onPick, placeholder, onEnter, onTab, onHistory, autoFocus, className, mono, id, "aria-label": aria }: {
    value: string;
    onChange: (v: string) => void;
    suggestions: Suggestion<T>[];
    onPick: (s: Suggestion<T>) => void;
    placeholder?: string;
    onEnter?: () => void;
    /** Tab with the list closed (e.g. complete the command line) */
    onTab?: () => void;
    /** ArrowUp/ArrowDown with the list closed (command history) */
    onHistory?: (dir: -1 | 1) => void;
    autoFocus?: boolean;
    className?: string;
    mono?: boolean;
    id?: string;
    "aria-label"?: string;
}): import("react").JSX.Element;
export declare function dur(sec: number): string;
/** unix seconds -> "3 min ago" */
export declare const agoS: (at: number | null | undefined) => string;
export declare const whenS: (at: number | null | undefined) => string;
export declare const fmtPos: (p?: {
    x: number;
    y: number;
    z: number;
} | null) => string;
export declare const pretty: (s: string) => string;
/** Debounced value (search boxes). */
export declare function useDebounced<T>(value: T, ms: number): T;
/** Re-renders every `ms` (relative times, stale state). */
export declare function useNow(ms: number): number;
