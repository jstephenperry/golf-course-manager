/**
 * Renders a "you don't have access" panel inside the Layout. Used by
 * `ProtectedRoute` when a route requires a permission the signed-in user
 * lacks. Kept as a card so the sidebar nav stays visible — the user can
 * navigate to a page they do have access to.
 */
export function Forbidden({
  permission,
}: {
  permission?: string;
}): JSX.Element {
  return (
    <div className="card" role="alert" aria-live="polite">
      <h2>Access denied</h2>
      <p style={{ marginTop: 0 }}>
        You don't have permission to view this page.
        {permission ? (
          <>
            {" "}
            <span className="muted">
              (Requires <code>{permission}</code>.)
            </span>
          </>
        ) : null}
      </p>
      <p className="muted" style={{ marginBottom: 0 }}>
        Pick another section from the sidebar, or ask an owner / manager
        to grant the role you need.
      </p>
    </div>
  );
}
