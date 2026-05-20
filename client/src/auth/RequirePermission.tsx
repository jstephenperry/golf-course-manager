import type { ReactNode } from "react";
import { useAuth } from "./AuthContext";

/**
 * Conditionally render `children` based on whether the signed-in user has
 * the given permission. Used to gate buttons / inline affordances inside
 * pages the user can otherwise view.
 *
 * The server is the security boundary — this is purely a UX nicety so we
 * don't show actions that would 403 if invoked.
 */
export function RequirePermission({
  permission,
  children,
  fallback = null,
}: {
  permission: string;
  children: ReactNode;
  /** Rendered when the user lacks `permission`. Defaults to null. */
  fallback?: ReactNode;
}): JSX.Element | null {
  const { hasPermission } = useAuth();
  if (!hasPermission(permission)) return <>{fallback}</>;
  return <>{children}</>;
}
