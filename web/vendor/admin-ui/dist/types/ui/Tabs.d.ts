import { type ReactNode } from "react";
export interface TabItem<T extends string = string> {
    id: T;
    label: ReactNode;
    count?: number;
    disabled?: boolean;
}
export interface TabsProps<T extends string> {
    items: TabItem<T>[];
    value: T;
    onChange: (id: T) => void;
    "aria-label"?: string;
}
export declare function Tabs<T extends string>({ items, value, onChange, ...rest }: TabsProps<T>): import("react").JSX.Element;
export interface NavTabItem {
    to: string;
    label: ReactNode;
    count?: number;
    end?: boolean;
}
/** Tabs that are links (route per tab). */
export declare function NavTabs({ items, "aria-label": label }: {
    items: NavTabItem[];
    "aria-label"?: string;
}): import("react").JSX.Element;
export interface SegmentedProps<T extends string> {
    options: {
        id: T;
        label: ReactNode;
    }[];
    value: T;
    onChange: (id: T) => void;
    "aria-label"?: string;
    /** "side": the large small-caps switch with a sliding gold diamond. */
    variant?: "line" | "side";
}
export declare function Segmented<T extends string>({ options, value, onChange, variant, ...rest }: SegmentedProps<T>): import("react").JSX.Element;
