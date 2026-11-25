// Lane C's endpoints on the app's API client (the integrator may fold them into app/context's ep).
import { api } from "../app/context";
import { laneC } from "../api/laneC";

export const lc = laneC(api);
