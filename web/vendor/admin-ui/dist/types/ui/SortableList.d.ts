import { type ReactNode } from "react";
export interface SortableListProps<T> {
    items: T[];
    itemKey: (item: T) => string;
    render: (item: T, index: number) => ReactNode;
    onReorder: (items: T[]) => void;
    disabled?: boolean;
    "aria-label"?: string;
}
export declare function SortableList<T>({ items, itemKey, render, onReorder, disabled, ...rest }: SortableListProps<T>): import("react").JSX.Element;
