import { useEffect } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";

/**
 * Public sign-in screen. Renders a single primary button that kicks off the
 * Keycloak PKCE redirect. If the user is already signed in we bounce them
 * straight to the `returnTo` query string (or `/`).
 */
export function Login(): JSX.Element {
  const { isAuthenticated, isLoading, login, error } = useAuth();
  const navigate = useNavigate();
  const [search] = useSearchParams();
  const returnTo = search.get("returnTo") ?? "/";

  useEffect(() => {
    if (!isLoading && isAuthenticated) {
      navigate(returnTo, { replace: true });
    }
  }, [isAuthenticated, isLoading, navigate, returnTo]);

  if (isLoading) {
    return (
      <div className="login-shell">
        <div className="login-card">
          <div className="page-loading" role="status" aria-live="polite">
            <div className="spinner" aria-hidden />
            <span>Loading…</span>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="login-shell">
      <div className="login-card">
        <div className="brand">
          <div className="brand-mark">F</div>
          <div className="brand-name">Fairway HQ</div>
          <div className="brand-sub">Golf Course Manager</div>
        </div>
        <p className="muted" style={{ marginTop: 0 }}>
          Sign in to manage tee times, members, and operations.
        </p>
        <button
          className="btn"
          onClick={() => {
            void login();
          }}
        >
          Sign in with Keycloak
        </button>
        {error && (
          <div className="login-error" role="alert">
            {error}
          </div>
        )}
      </div>
    </div>
  );
}
