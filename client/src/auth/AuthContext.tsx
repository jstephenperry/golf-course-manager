import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { User, UserManager } from "oidc-client-ts";
import { setAuth } from "../api/client";
import { buildUserManagerSettings, readAuthConfig } from "./oidcSettings";
import { permissionsFor } from "./rolePermissions";

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

export interface AuthUser {
  /** Subject claim — Keycloak user id. */
  sub: string;
  /** Username (usually `preferred_username`). */
  username: string;
  /** Display name if Keycloak supplied it. */
  name?: string;
  /** Email if present in the id token. */
  email?: string;
}

/**
 * State key payload we hand to `signinRedirect` so the callback page can
 * route the user back where they started after Keycloak bounces them home.
 */
export interface AuthRedirectState {
  returnTo?: string;
}

export interface AuthApi {
  /** The authenticated user, or null if signed out / not yet loaded. */
  user: AuthUser | null;
  /** Current access token (in-memory only). */
  accessToken: string | null;
  /** Realm roles the user holds (from `realm_access.roles`). */
  roles: string[];
  /** True iff the user has the given permission, derived locally from roles. */
  hasPermission: (perm: string) => boolean;
  /** Start an interactive PKCE login flow (redirect). */
  login: () => Promise<void>;
  /** End the session at Keycloak and clear local state. */
  logout: () => Promise<void>;
  /** True while the initial silent-renew attempt is in flight. */
  isLoading: boolean;
  /**
   * True once a login or logout redirect has been initiated, until the
   * browser navigates away. Consumers (e.g. `ProtectedRoute`) must NOT
   * trigger an interactive login while this is set — during logout,
   * `signoutRedirect` clears the user before it navigates, and an
   * auto-login fired in that window would race (and beat) the logout
   * navigation, silently signing the user back in.
   */
  isRedirecting: boolean;
  /** True iff we currently hold a non-expired access token. */
  isAuthenticated: boolean;
  /**
   * True iff OIDC is configured (env vars present). When false, auth is
   * disabled (local dev / tests) and consumers should not gate on it.
   */
  isEnabled: boolean;
  /**
   * Most recent auth error message, or null. Surface in the login page so
   * the user can see why a redirect/renew failed.
   */
  error: string | null;
  /**
   * Complete an OIDC redirect by exchanging the auth code for tokens.
   * Returns the `returnTo` path stashed on the original redirect (or null
   * if none was set). The callback page calls this after Keycloak returns.
   */
  completeSignin: () => Promise<string | null>;
}

const AuthContext = createContext<AuthApi | null>(null);

// ---------------------------------------------------------------------------
// Role / claim extraction
// ---------------------------------------------------------------------------

interface RealmAccessClaim {
  roles?: unknown;
}

/**
 * Decode a JWT's payload without verifying the signature. The crypto
 * check is the API's job — we already trust the token because the OIDC
 * client received it from the configured Keycloak via PKCE. We just
 * need the claims to drive UI gating.
 */
function decodeJwtPayload(token: string): Record<string, unknown> | null {
  const parts = token.split(".");
  if (parts.length !== 3) return null;
  try {
    const padded = parts[1] + "=".repeat((4 - (parts[1].length % 4)) % 4);
    const json = atob(padded.replace(/-/g, "+").replace(/_/g, "/"));
    return JSON.parse(decodeURIComponent(escape(json)));
  } catch {
    return null;
  }
}

function extractRoles(user: User | null): string[] {
  if (!user || !user.access_token) return [];
  // Roles live in the ACCESS token's `realm_access.roles` (Keycloak's
  // default). The id_token does NOT contain them unless you add an
  // explicit mapper, which we don't. Decoding the access token here
  // is the source-of-truth read — matches what the API sees.
  const claims = decodeJwtPayload(user.access_token);
  if (!claims) return [];

  const collected: string[] = [];
  // Shape 1: realm_access.roles (Keycloak realm roles — primary)
  const realmAccess = claims["realm_access"] as RealmAccessClaim | undefined;
  if (Array.isArray(realmAccess?.roles)) {
    for (const r of realmAccess.roles) {
      if (typeof r === "string") collected.push(r);
    }
  }
  // Shape 2: bare `roles` claim (some IdPs / mapped tokens)
  const flat = claims["roles"];
  if (Array.isArray(flat)) {
    for (const r of flat) {
      if (typeof r === "string" && !collected.includes(r)) collected.push(r);
    }
  }
  return collected;
}

function toAuthUser(user: User | null): AuthUser | null {
  if (!user || !user.profile) return null;
  const profile = user.profile;
  const sub = typeof profile.sub === "string" ? profile.sub : "";
  if (!sub) return null;
  const username =
    typeof profile["preferred_username"] === "string"
      ? (profile["preferred_username"] as string)
      : sub;
  return {
    sub,
    username,
    name: typeof profile.name === "string" ? profile.name : undefined,
    email: typeof profile.email === "string" ? profile.email : undefined,
  };
}

// ---------------------------------------------------------------------------
// Provider
// ---------------------------------------------------------------------------

export function AuthProvider({ children }: { children: ReactNode }) {
  const config = useMemo(readAuthConfig, []);

  // Stable across renders. Built once; nulled out if env is missing.
  const userManagerRef = useRef<UserManager | null>(null);
  if (config && userManagerRef.current === null) {
    userManagerRef.current = new UserManager(buildUserManagerSettings(config));
  }
  if (!config && typeof window !== "undefined") {
    // Loud once per page load; helps when someone forgets to set env vars
    // but doesn't break tests that don't care about auth.
    // eslint-disable-next-line no-console
    console.warn(
      "[auth] VITE_KEYCLOAK_URL / VITE_KEYCLOAK_REALM / VITE_KEYCLOAK_CLIENT_ID not set — auth is disabled.",
    );
  }

  const [oidcUser, setOidcUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(Boolean(config));
  const [isRedirecting, setIsRedirecting] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  // Keep the latest token in a ref so the api module's sync token provider
  // can read it without a re-render race.
  const tokenRef = useRef<string | null>(null);
  tokenRef.current = oidcUser?.access_token ?? null;

  // Wire the api module to our auth state. Runs once: the provider/renewer
  // close over refs, so they stay valid across token changes.
  useEffect(() => {
    const um = userManagerRef.current;
    if (!um) {
      setAuth(null, null);
      return;
    }
    setAuth(
      () => tokenRef.current,
      async () => {
        try {
          const renewed = await um.signinSilent();
          return renewed?.access_token ?? null;
        } catch {
          return null;
        }
      },
    );
    return () => setAuth(null, null);
  }, []);

  // Initial silent renew on mount + subscribe to user-loaded / unloaded.
  useEffect(() => {
    const um = userManagerRef.current;
    if (!um) {
      setIsLoading(false);
      return;
    }

    let cancelled = false;

    const removeLoaded = um.events.addUserLoaded((u) => {
      if (!cancelled) {
        setOidcUser(u);
        setError(null);
      }
    });
    const removeUnloaded = um.events.addUserUnloaded(() => {
      if (!cancelled) setOidcUser(null);
    });
    const removeSilentRenewError = um.events.addSilentRenewError((err) => {
      // eslint-disable-next-line no-console
      console.warn("[auth] silent renew failed", err);
      if (!cancelled) setError(err?.message ?? "Silent renew failed");
    });

    void (async () => {
      try {
        const existing = await um.getUser();
        if (existing && !existing.expired) {
          if (!cancelled) setOidcUser(existing);
        } else {
          const renewed = await um.signinSilent();
          if (!cancelled && renewed) setOidcUser(renewed);
        }
      } catch {
        // Silent renew failing here is expected when the user has never
        // logged in — the login page (next slice) handles the interactive
        // flow. Just stop the loading spinner.
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    })();

    return () => {
      cancelled = true;
      removeLoaded();
      removeUnloaded();
      removeSilentRenewError();
    };
  }, []);

  // Belt-and-suspenders renew trigger. `automaticSilentRenew: true` on the
  // UserManager handles this in normal operation; this is a guard for
  // edge cases (tab woken from sleep, clock skew, etc.) where the event
  // didn't fire but the token is about to expire.
  useEffect(() => {
    const um = userManagerRef.current;
    if (!um || !oidcUser?.expires_at) return;

    const expiresAtMs = oidcUser.expires_at * 1000;
    const renewAtMs = expiresAtMs - 60_000;
    const now = Date.now();
    const delay = Math.max(0, renewAtMs - now);

    let cancelled = false;
    const timer = window.setTimeout(() => {
      if (cancelled) return;
      void um.signinSilent().catch((err) => {
        // eslint-disable-next-line no-console
        console.warn("[auth] proactive silent renew failed", err);
      });
    }, delay);

    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [oidcUser]);

  const login = useCallback(async () => {
    const um = userManagerRef.current;
    if (!um) {
      setError("Auth is not configured.");
      return;
    }
    setIsRedirecting(true);
    try {
      // Remember where the user was trying to go so the callback page can
      // restore it. `signinRedirect`'s `state` option round-trips through
      // Keycloak and is returned on `signinRedirectCallback`.
      const returnTo =
        typeof window !== "undefined" ? window.location.pathname : "/";
      await um.signinRedirect({
        state: { returnTo: returnTo === "/login" ? "/" : returnTo },
      });
    } catch (err) {
      setIsRedirecting(false);
      setError(err instanceof Error ? err.message : "Failed to start sign-in.");
    }
  }, []);

  const completeSignin = useCallback(async (): Promise<string | null> => {
    const um = userManagerRef.current;
    if (!um) {
      setError("Auth is not configured.");
      return null;
    }
    try {
      const user = await um.signinRedirectCallback();
      setOidcUser(user);
      setError(null);
      const state = (user.state ?? null) as AuthRedirectState | null;
      return state?.returnTo ?? null;
    } catch (err) {
      const msg =
        err instanceof Error ? err.message : "Sign-in callback failed.";
      setError(msg);
      return null;
    }
  }, []);

  const logout = useCallback(async () => {
    const um = userManagerRef.current;
    if (!um) return;
    // Set BEFORE signoutRedirect: it calls removeUser() (→ userUnloaded →
    // oidcUser=null) before navigating, and ProtectedRoute would otherwise
    // see !isAuthenticated and fire login(), racing past the logout.
    setIsRedirecting(true);
    try {
      await um.signoutRedirect();
    } catch (err) {
      setIsRedirecting(false);
      setError(err instanceof Error ? err.message : "Failed to sign out.");
    }
  }, []);

  const roles = useMemo(() => extractRoles(oidcUser), [oidcUser]);
  const permissions = useMemo(() => permissionsFor(roles), [roles]);
  const hasPermission = useCallback(
    (perm: string) => permissions.has(perm),
    [permissions],
  );

  const authUser = useMemo(() => toAuthUser(oidcUser), [oidcUser]);
  const accessToken = oidcUser?.access_token ?? null;
  const isEnabled = config !== null;
  const isAuthenticated =
    oidcUser !== null && !oidcUser.expired && Boolean(accessToken);

  const value = useMemo<AuthApi>(
    () => ({
      user: authUser,
      accessToken,
      roles,
      hasPermission,
      login,
      logout,
      isLoading,
      isRedirecting,
      isAuthenticated,
      isEnabled,
      error,
      completeSignin,
    }),
    [
      authUser,
      accessToken,
      roles,
      hasPermission,
      login,
      logout,
      isLoading,
      isRedirecting,
      isAuthenticated,
      isEnabled,
      error,
      completeSignin,
    ],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthApi {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside AuthProvider");
  return ctx;
}

/**
 * Like {@link useAuth} but returns null instead of throwing when there's no
 * surrounding `AuthProvider`. For components (e.g. the data store) that may
 * be mounted in isolation by tests and need to degrade to "auth disabled".
 */
export function useOptionalAuth(): AuthApi | null {
  return useContext(AuthContext);
}
