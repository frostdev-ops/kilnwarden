export interface JsonViewProps {
    value: unknown;
    /** Levels open at first (default 1). */
    open?: number;
    /** Max body height in px. */
    maxHeight?: number;
}
export declare function JsonView({ value, open, maxHeight }: JsonViewProps): import("react").JSX.Element;
