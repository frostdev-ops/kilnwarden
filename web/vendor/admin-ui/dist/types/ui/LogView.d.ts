export type LogLevel = "err" | "warn" | "dbg" | null;
/** Kiln: `[E]`, `[W]`, `[D]`/`[T]`; also ERROR/WARN words (game, Wine). */
export declare function classifyLine(line: string): LogLevel;
export interface LogViewProps {
    /** Lines (or text, split on newlines). */
    lines?: string[];
    text?: string;
    /** Auto-scroll at the end (default true). */
    follow?: boolean;
    /** Body height in px (default 420). */
    height?: number;
    /** Fill the parent's height instead (the parent is a flex column). */
    fill?: boolean;
    /** Case-insensitive substring; non-matching lines are hidden. */
    filter?: string;
    lineNumbers?: boolean;
    classify?: (line: string) => LogLevel;
    "aria-label"?: string;
}
export declare function LogView({ lines, text, follow, height, fill, filter, lineNumbers, classify, ...rest }: LogViewProps): import("react").JSX.Element;
