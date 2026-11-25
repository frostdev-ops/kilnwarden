import type { ReactNode } from "react";
export interface EmptyStateProps {
    /** Kept for old callers: empty states have no icons. */
    icon?: ReactNode;
    title: ReactNode;
    hint?: ReactNode;
    action?: ReactNode;
    /** The whole page has nothing yet. */
    page?: boolean;
}
export declare function EmptyState({ title, hint, action, page }: EmptyStateProps): import("react").JSX.Element;
/** Placeholder rows while a query loads. */
export declare function Skeleton({ lines, width, height }: {
    lines?: number;
    width?: number | string;
    height?: number;
}): import("react").JSX.Element;
