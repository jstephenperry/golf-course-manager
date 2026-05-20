import { useEffect } from "react";
import { Outlet } from "react-router-dom";
import { Layout } from "../components/Layout";
import { Forbidden } from "./Forbidden";
import { useAuth } from "./AuthContext";

/**
 * Gate every authenticated route on a valid session and (optionally) a
 * specific permission. Designed to be used as the element of a parent
 * `<Route>` so its `<Outlet />` renders the child routes.
 *
 * Behavior:
 *  - while the auth context is hydrating → page-loading spinner
 *  - not signed in → kicks off `login()` (redirect to Keycloak) and
 *    renders null while the browser navigates away
 *  - signed in but lacks `requirePermission` → friendly Forbidden panel
 *    inside the app Layout so the sidebar stays usable
 *  - otherwise → render child routes
 */
export function ProtectedRoute({
  requirePermission,
}: {
  requirePermission?: string;
}): JSX.Element | null {
  const { isLoading, isAuthenticated, hasPermission, login } = useAuth();

  // Trigger interactive sign-in once when we discover the user isn't
  // authenticated. Doing this in an effect (not during render) avoids
  // calling a promise-returning side-effect from the render path.
  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      void login();
    }
  }, [isLoading, isAuthenticated, login]);

  if (isLoading) {
    return (
      <div className="page-loading" role="status" aria-live="polite">
        <div className="spinner" aria-hidden />
        <span>Loading…</span>
      </div>
    );
  }

  if (!isAuthenticated) {
    // login() above is redirecting us to Keycloak — show a quiet spinner
    // instead of flashing the sign-in card the user is about to leave.
    return (
      <div className="page-loading" role="status" aria-live="polite">
        <div className="spinner" aria-hidden />
        <span>Redirecting to sign-in…</span>
      </div>
    );
  }

  if (requirePermission && !hasPermission(requirePermission)) {
    return (
      <Layout>
        <Forbidden permission={requirePermission} />
      </Layout>
    );
  }

  return <Outlet />;
}
