/** A flattened locale file: dotted key → text. */
export type Table = Readonly<Record<string, string>>;
/** Placeholder values: positional (`{0}`, `{1}`) or one object of named values (`{name}`, `{count}`). */
export type Args = readonly unknown[];
/** The plural categories a key may end in (Intl.PluralRules / CLDR). */
export declare const PLURAL_FORMS: readonly ["zero", "one", "two", "few", "many", "other"];
/** The language every other falls back to. */
export declare const FALLBACK = "en";
/** "zh_CN", "ZH-cn" and " zh-cn " are all "zh-cn". Empty gives "en". */
export declare function normalizeTag(tag: string | null | undefined): string;
/** The tags tried for `tag`, in order: the tag, its closest relatives, its main language, English.
 * "zh-CN" gives zh-cn, zh, en; "es-MX" gives es-mx, es-419, es, en (as Locale.cs's LookupOrder). Script tags a
 * browser may report map to the region files translators write: zh-Hans → zh-cn, zh-Hant / zh-HK / zh-MO → zh-tw. */
export declare function lookupOrder(tag: string): string[];
/** A locale file's object as dotted keys. Non-string values are skipped and named in `bad` (for the check script). */
export declare function flatten(obj: unknown, prefix?: string, into?: Record<string, string>, bad?: string[]): Record<string, string>;
/** The placeholders of a text as written in it, without formats: "{0} of {total:N0}" gives ["0", "total"]. */
export declare function placeholders(template: string): string[];
/** The placeholders of a text with their formats, once per use: "{0} of {total:N0} {0}" gives ["0", "total:N0", "0"]. */
export declare function placeholderTokens(template: string): string[];
/** A number in a .NET standard numeric format, in `locale`: N (grouped), F (fixed), D (whole, zero-padded), P (percent
 * of a fraction), each with an optional digit count ("N0", "F1", "D3", "P0"). An unknown format gives null. */
export declare function formatNumber(n: number, spec: string, locale: string): string | null;
/** Fills `{0}`, `{name}`, `{name:format}`; `{{` and `}}` are braces. A placeholder without a value stays as written. */
export declare function fill(template: string, args: Args, locale?: string): string;
/** The number that picks a plural form: the named `count`, else the first positional value when it is a number. */
export declare function pluralCount(args: Args): number | null;
/** The CLDR plural category of `n` in `locale` ("one", "other", ...). */
export declare function pluralCategory(n: number, locale: string): string;
/** The raw text of `key` in one table: the key itself, or for a count its plural form in that language
 * (`key.one`, then `key.other`). */
export declare function rawIn(table: Table | undefined, key: string, count: number | null, locale: string): string | undefined;
/** Keys that are plural forms of one base key ("mods.count.one" → "mods.count"), else the key. */
export declare function pluralBase(key: string): string;
