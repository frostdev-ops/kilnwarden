import type { Json, SchemaSnapshot, SnapshotEntry, ValidationError } from "../api/shared";
export interface SchemaFormProps {
    schema: SchemaSnapshot;
    /** The current values (JSON, nested by path). Missing entries show their default. */
    values: Json;
    onChange: (path: string, value: Json) => void;
    /** Back to the default (removes the key). */
    onReset?: (path: string) => void;
    errors?: ValidationError[];
    /** Paths whose new value waits for a restart (running servers). */
    pending?: string[];
    disabled?: boolean;
    /** Substring filter over path and comment. */
    filter?: string;
    /** Field labels by path, for a card whose key names do not read as words ("graphics.fpsCap": "Frame cap"); an entry
     *  without one is named from its key. */
    labels?: Record<string, string>;
    /** A long comment shows its first clause, with More for the rest (Settings: one line of help per row). */
    clamp?: boolean;
}
/** Seconds in a Kiln duration ("90s", "1h30m"), or null. */
export declare function parseDuration(text: string): number | null;
/** Value at a dotted path (`a.b`, `list[2]`). */
export declare function getPath(values: Json, path: string): Json | undefined;
/** The entry's slider step ([Range(min, max, step)]) when it suits the entry, else undefined (the slider keeps its
 * own increment). Mirrors Kiln.Api ConfigSchema.SliderStep: int/float with both bounds, a positive finite step no
 * larger than max - min that changes a value at either end, whole step and min for int. The range input steps from
 * min, so the slider gives min + n * step; the number box and checkValue never round to it. */
export declare function sliderStep(e: SnapshotEntry): number | undefined;
/** Client-side check mirroring Kiln.Api's rules (the backend validates again). */
export declare function checkValue(e: SnapshotEntry, v: Json): string | null;
export declare function SchemaForm({ schema, values, onChange, onReset, errors, pending, disabled, filter, labels, clamp }: SchemaFormProps): import("react").JSX.Element;
/** A long comment's line of help (null: the comment is short enough to show whole). The part before the colon is kept
 *  when it says what the entry is ("A small readout in a corner of the game: …"); when it only names the subject
 *  ('With the DLSS render size custom: render width…', "Kiln's frame cap: the game renders…", "Chat history: Up/Down…"),
 *  the line is the clause after it. Either way the line is cut at its first clause, parentheses left out. */
export declare function commentHead(text: string, label?: string): string | null;
