export declare function fmtBytes(n: number | null | undefined): string;
/** "3 min ago", "2 h ago", "yesterday", or a date. Accepts RFC 3339 or ms. */
export declare function fmtAgo(at: string | number | null | undefined, now?: number): string;
export declare function fmtTime(at: string | number | null | undefined): string;
/** `kdh1:3f2a9c1d…` → `3f2a9c1d`; a sha256 → its first 8. */
export declare function shortHash(h: string | null | undefined): string;
/** An install's note for its toast: "1 changed file kept in backups" when it backed up files changed by hand, else
 * undefined. */
export declare function installNote(r: {
    changedBackedUp?: string[];
} | {
    placements: unknown;
} | null | undefined): string | undefined;
/** A path cut for the middle shortening of `PathText`: everything up to the last name, and the last name with the
 *  separator before it ("/mnt/games" + "/Kiln"). A root ("/", "D:\") is all tail. */
export declare function splitPath(path: string): [string, string];
export declare function errorText(e: unknown): string;
