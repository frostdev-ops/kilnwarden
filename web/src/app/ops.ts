// Lane B's endpoints on the app's one API client (docs/step2-lane-b-ops.md).
import { api } from "./context";
import { opsEndpoints } from "../api/ops";

export const ops = opsEndpoints(api);
