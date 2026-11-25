// The page for an address the app does not have.
import { Link } from "react-router-dom";
import { Banner, Page } from "../ui/kit";

export function NotFound() {
  return (
    <Page title="Not found">
      <Banner tone="warn" title="There is no page at this address">
        It may belong to a module that is switched off. <Link to="/">Back to the overview</Link>
      </Banner>
    </Page>
  );
}
