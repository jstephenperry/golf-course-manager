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
 *  - auth disabled (no OIDC env — local dev / tests / E2E) → render through
 *    with no gating. There's no IdP to authenticate against; the server is
 *    the real boundary (and in those modes runs with a synthesized owner).
 *    Production always has Keycloak configured (`isEnabled` true; AuthSetup
 *    throws without an authority), so this never opens a real deployment.
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
  const {
    isEnabled,
    isLoading,
    isRedirecting,
    isAuthenticated,
    hasPermission,
    login,
  } = useAuth();

  // Trigger interactive sign-in once when we discover the user isn't
  // authenticated. Doing this in an effect (not during render) avoids
  // calling a promise-returning side-effect from the render path.
  //
  // Skip while a redirect is already in flight: during logout,
  // signoutRedirect clears the user (→ !isAuthenticated) just before it
  // navigates to Keycloak, and an auto-login here would race past the
  // logout and silently sign the user back in.
  useEffect(() => {
    if (isEnabled && !isLoading && !isAuthenticated && !isRedirecting) {
      void login();
    }
  }, [isEnabled, isLoading, isAuthenticated, isRedirecting, login]);

  // Auth disabled → no gating (see header comment).
  if (!isEnabled) {
    return <Outlet />;
  }

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
