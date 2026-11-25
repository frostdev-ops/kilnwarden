// The panel's own endpoints (settings, activity, dashboard...) on the app's one API client.
import { api } from "./context";
import { panelEndpoints } from "../api/panel";

export const panel = panelEndpoints(api);
