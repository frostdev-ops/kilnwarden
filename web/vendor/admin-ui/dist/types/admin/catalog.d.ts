/** The one base URL of the admin pages' local assets (layout: map/..., data/...), read when used (the host is set
 *  after the modules load):
 *  - VITE_KILN_ASSETS when set;
 *  - the host's (configureAdmin): a release build of the launcher uses the `kiln-asset` protocol, prefix admin/
 *    (src-tauri/src/assets.rs: $KILN_ADMIN_ASSETS, then <data>/admin-assets): kiln-asset://localhost/admin/, on Windows
 *    http://kiln-asset.localhost/admin/ (../api/adminTauri.ts); a web host serves its own;
 *  - else /assets-local/ (dev: vite, `tauri dev`, a plain browser; vite serves launcher/assets-local; under tauri dev
 *    WebKitGTK refuses custom-scheme images and CSS from the http://localhost:1420 page). */
export declare function assetsRoot(): string;
export interface CreatureTemplate {
    guid: string;
    template: string;
    name: string;
}
/** Creature template GUID -> name (the Enshrouded Interactive Map's data, MIT, Nicolas Ferreira), if fetched. */
export declare function useCreatureCatalog(): import("@tanstack/react-query").UseQueryResult<CreatureTemplate[], Error>;
/** Turns Enemy_Wildlife_Wolf_T2 into "Wolf" */
export declare const prettyTemplate: (template?: string | null) => string;
