import { type HTMLAttributes, type ReactNode } from "react";
/** The level of a Card's title: 2 on a page; 3 where the page's own sections have an h2 above the cards (Settings: the
 *  section's title, then its groups). A Panel's title is one below. */
export declare const CardLevel: import("react").Context<2 | 3>;
export interface CardProps extends Omit<HTMLAttributes<HTMLElement>, "title"> {
    title?: ReactNode;
    /** One short line beside the title. */
    sub?: ReactNode;
    actions?: ReactNode;
    footer?: ReactNode;
    /** Kept for old callers; sections have no frame or glow. */
    glow?: boolean;
    /** Kept for old callers. */
    flush?: boolean;
}
/** A titled group on the sheet: small-caps head, the hint beside it, actions on the right. */
export declare function Card({ title, sub, actions, footer, glow: _glow, flush: _flush, className, children, ...rest }: CardProps): import("react").JSX.Element;
export { Card as Section };
export interface PanelProps extends Omit<HTMLAttributes<HTMLElement>, "title"> {
    title?: ReactNode;
    actions?: ReactNode;
}
/** A smaller titled group inside a section. */
export declare function Panel({ title, actions, className, children, ...rest }: PanelProps): import("react").JSX.Element;
export interface PageHeaderProps {
    /** The page's name (Grenze Gotisch). */
    title: ReactNode;
    /** The live sentence: what is the state? */
    sub?: ReactNode;
    actions?: ReactNode;
    /** Beside the name: an id, a state word. */
    meta?: ReactNode;
}
export declare function PageHeader({ title, sub, actions, meta }: PageHeaderProps): import("react").JSX.Element;
/** A page on the sheet. `fill`: the page does not scroll; its lists scroll inside it. */
export declare function Page({ wide, fill, className, children }: {
    wide?: boolean;
    fill?: boolean;
    className?: string;
    children: ReactNode;
}): import("react").JSX.Element;
/** Main column plus a fixed aside (facts 204-232 wide, forms 300). */
export declare function SplitPage({ children, aside, form }: {
    children: ReactNode;
    aside: ReactNode;
    form?: boolean;
}): import("react").JSX.Element;
export type KeyValueRow = [ReactNode, ReactNode] | null | false | undefined | "" | 0;
/** A two-column definition list; falsy rows are skipped. */
export declare function KeyValue({ rows }: {
    rows: KeyValueRow[];
}): import("react").JSX.Element;
