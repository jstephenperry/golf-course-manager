import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";

/**
 * OIDC redirect landing page (`/oidc/callback`). Calls
 * `completeSignin()` exactly once on mount to exchange the auth code for
 * tokens, then sends the browser to the `returnTo` stashed when the user
 * started the flow (or `/` if none).
 */
export function AuthCallback(): JSX.Element {
  const { completeSignin, error } = useAuth();
  const navigate = useNavigate();
  // StrictMode mounts effects twice in dev; signinRedirectCallback is
  // one-shot (the code is invalidated server-side after first use), so
  // gate on a ref to avoid hitting the second mount with a stale code.
  const ranRef = useRef(false);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (ranRef.current) return;
    ranRef.current = true;
    void (async () => {
      const returnTo = await completeSignin();
      if (returnTo === null) {
        // The provider already set `error`; let the user retry from the
        // login page rather than being stuck on a blank callback URL.
        setFailed(true);
        return;
      }
      navigate(returnTo || "/", { replace: true });
    })();
  }, [completeSignin, navigate]);

  if (failed) {
    return (
      <div className="login-shell">
        <div className="login-card">
          <div className="brand">
            <div className="brand-mark">F</div>
            <div className="brand-name">Fairway HQ</div>
          </div>
          <div className="login-error" role="alert">
            {error ?? "Sign-in failed."}
          </div>
          <button
            className="btn"
            style={{ marginTop: 12 }}
            onClick={() => navigate("/login", { replace: true })}
          >
            Back to sign-in
          </button>
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
        </div>
        <div
          className="page-loading"
          role="status"
          aria-live="polite"
          style={{ padding: "24px 0 0" }}
        >
          <div className="spinner" aria-hidden />
          <span>Completing sign-in…</span>
        </div>
      </div>
    </div>
  );
}
