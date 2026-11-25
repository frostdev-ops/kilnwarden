import { type ReactNode } from "react";
export interface Column<R> {
    key: string;
    header: ReactNode;
    /** CSS grid track, default `minmax(0, 1fr)` (e.g. "120px", "2fr", "max-content"). */
    width?: string;
    align?: "left" | "right";
    render?: (row: R, index: number) => ReactNode;
    /** Makes the header clickable; ascending comparator. */
    sort?: (a: R, b: R) => number;
}
export interface TableProps<R> {
    columns: Column<R>[];
    rows: R[];
    rowKey: (row: R, index: number) => string;
    /** Default 46. */
    rowHeight?: number;
    /** Fixed body height; default: fits the rows up to maxHeight. */
    height?: number;
    /** Default 520. */
    maxHeight?: number;
    /** Fill the space the parent gives it and scroll inside (lists scroll inside the sheet). */
    fill?: boolean;
    onRowClick?: (row: R) => void;
    selectedKey?: string | null;
    /** Extra class per row ("fresh": a new row arrives flushed with ember). */
    rowClass?: (row: R) => string | undefined;
    empty?: ReactNode;
    "aria-label"?: string;
}
export declare function Table<R>({ columns, rows, rowKey, rowHeight, height, maxHeight, fill, onRowClick, selectedKey, rowClass, empty, ...rest }: TableProps<R>): import("react").JSX.Element;
/** A name with its id beside it, on one line. */
export declare function NameId({ name, id, dim }: {
    name: ReactNode;
    id?: ReactNode;
    dim?: boolean;
}): import("react").JSX.Element;
