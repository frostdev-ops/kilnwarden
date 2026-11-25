import { type ReactNode } from "react";
export { fill, formatNumber, lookupOrder, placeholders, type Table } from "./format";
/** The preference: "system" or a language tag ("en", "zh-CN"). */
export type LanguagePref = "system" | (string & {});
/** The translation function: `t("key")`, `t("key", { name: "x", count: 3 })`, `t("key", a, b)` for `{0}`, `{1}`. */
export type TFunction = (key: string, ...args: unknown[]) => string;
/** Adds (or replaces) a built-in language: its tag as the file is named and the file's object. */
export declare function registerLocale(tag: string, file: unknown): void;
/** Replaces the languages read from the data folder (a translator's files; English stays en.json's). */
export declare function setUserLocales(files: readonly {
    tag: string;
    file: unknown;
}[]): void;
/** The languages with a file, English first, then by tag: `{ tag, name }` with the name in its own language. */
export declare function languages(): {
    tag: string;
    name: string;
}[];
/** A language's name in that language: its file's own `language.name`, else a known name, else the system's name for
 * it, else the tag. */
export declare function languageName(tag: string): string;
/** The language a preference shows: a tag, or for "system" the first system language with a file (else English). */
export declare function resolveLanguage(p: LanguagePref, system?: readonly string[]): string;
/** Sets the preference ("system" or a tag); every `useT` re-renders. */
export declare function setLanguage(p: LanguagePref | null | undefined): void;
/** The preference as set. */
export declare const languagePref: () => LanguagePref;
/** The tag of the language shown, normalized ("zh-cn"; "en" when none of the wanted ones has a file). */
export declare const currentLanguage: () => string;
/** The tag of the language shown, as its file is named ("zh-CN"), for Intl formatters. */
export declare const currentLocale: () => string;
/** The locale dates and numbers are written in (`toLocaleString(formatLocale(), ...)`): the shown translation's tag
 * ("zh-CN": 2026年11月18日), or undefined while English is shown (the system's own formats, as before translations). */
export declare const formatLocale: () => string | undefined;
/** The text of `key` in the current language with its placeholders filled. */
export declare function t(key: string, ...args: unknown[]): string;
/** Like `t`, for a text with elements in it: `rich("play.hint", { button: <b>{t("play.play")}</b> })` for
 * "Press {button} to start". Element values go in as they are; any other value is filled as `t` fills it. One named
 * object, or positional values. */
export declare function rich(key: string, ...args: unknown[]): ReactNode;
/** True when English (the fallback) has `key` (or its plural forms). */
export declare function hasKey(key: string): boolean;
/** True when the language shown has its own text for `key` (English: en.json has it); false when `t` would show
 * English's in its place. For a newer text that replaces an older one: the older one, translated, until the newer one is. */
export declare function hasOwnText(key: string): boolean;
/** The current language's tag; re-renders the component when the language or a table changes. */
export declare function useLanguage(): string;
/** `t` for a component: the same function, a new identity per change, and the component re-renders when the language
 * or a table changes (also inside React.memo). */
export declare function useT(): TFunction;
