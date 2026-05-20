import { Suspense, lazy } from "react";
import { Route, Routes } from "react-router-dom";
import { ProtectedRoute } from "./auth/ProtectedRoute";
import {
  COURSES_READ,
  IMPORT_RUN,
  MAINTENANCE_READ,
  MEMBERS_READ,
  PRODUCTS_READ,
  STAFF_READ,
  TABS_READ,
  TEE_TIMES_READ,
  TOURNAMENTS_READ,
} from "./auth/permissions";
import { ErrorBoundary } from "./components/ErrorBoundary";
import { Layout } from "./components/Layout";
import { ToasterProvider } from "./components/Toaster";
import { StoreProvider } from "./data/store";

const Dashboard = lazy(() =>
  import("./pages/Dashboard").then((m) => ({ default: m.Dashboard })),
);
const TeeTimes = lazy(() =>
  import("./pages/TeeTimes").then((m) => ({ default: m.TeeTimes })),
);
const Members = lazy(() =>
  import("./pages/Members").then((m) => ({ default: m.Members })),
);
const MemberDetail = lazy(() =>
  import("./pages/MemberDetail").then((m) => ({ default: m.MemberDetail })),
);
const Courses = lazy(() =>
  import("./pages/Courses").then((m) => ({ default: m.Courses })),
);
const Staff = lazy(() =>
  import("./pages/Staff").then((m) => ({ default: m.Staff })),
);
const ProShop = lazy(() =>
  import("./pages/ProShop").then((m) => ({ default: m.ProShop })),
);
const Tabs = lazy(() =>
  import("./pages/Tabs").then((m) => ({ default: m.Tabs })),
);
const Tournaments = lazy(() =>
  import("./pages/Tournaments").then((m) => ({ default: m.Tournaments })),
);
const Maintenance = lazy(() =>
  import("./pages/Maintenance").then((m) => ({ default: m.Maintenance })),
);
const ImportPage = lazy(() =>
  import("./pages/Import").then((m) => ({ default: m.Import })),
);
const Scorecard = lazy(() =>
  import("./pages/Scorecard").then((m) => ({ default: m.Scorecard })),
);
const Login = lazy(() =>
  import("./pages/Login").then((m) => ({ default: m.Login })),
);
const AuthCallback = lazy(() =>
  import("./pages/AuthCallback").then((m) => ({ default: m.AuthCallback })),
);

function PageFallback() {
  return (
    <div className="page-loading" role="status" aria-live="polite">
      <div className="spinner" aria-hidden />
      <span>Loading…</span>
    </div>
  );
}

export default function App() {
  return (
    <ErrorBoundary>
      <ToasterProvider>
        <StoreProvider>
          <Routes>
            {/* Public routes — no auth gate. */}
            <Route
              path="/login"
              element={
                <Suspense fallback={<PageFallback />}>
                  <Login />
                </Suspense>
              }
            />
            <Route
              path="/oidc/callback"
              element={
                <Suspense fallback={<PageFallback />}>
                  <AuthCallback />
                </Suspense>
              }
            />

            {/*
              Everything else requires a valid session. Each protected
              route additionally declares the permission it needs via a
              nested `ProtectedRoute requirePermission`, which renders a
              `<Forbidden>` panel (inside its own Layout) for users who
              are signed in but lack the permission. The permissions match
              Layout.tsx's nav filtering so a visible nav item always leads
              to a viewable page. This is defense-in-depth/UX only — the
              server is the real authorization boundary.

              Note: the permission-gated routes are NOT nested inside the
              shared `<Layout>` route; each gated `ProtectedRoute` provides
              its own Layout (on both the allowed and Forbidden paths), so
              the sidebar never double-renders.
            */}
            <Route element={<ProtectedRoute />}>
              {/* Routes available to any authenticated user. */}
              <Route element={<Layout />}>
                <Route
                  index
                  element={
                    <Suspense fallback={<PageFallback />}>
                      <Dashboard />
                    </Suspense>
                  }
                />
                <Route
                  path="*"
                  element={
                    <Suspense fallback={<PageFallback />}>
                      <Dashboard />
                    </Suspense>
                  }
                />
              </Route>

              <Route element={<ProtectedRoute requirePermission={TEE_TIMES_READ} />}>
                <Route element={<Layout />}>
                  <Route
                    path="tee-times"
                    element={
                      <Suspense fallback={<PageFallback />}>
                        <TeeTimes />
                      </Suspense>
                    }
                  />
                </Route>
              </Route>

              <Route element={<ProtectedRoute requirePermission={MEMBERS_READ} />}>
                <Route element={<Layout />}>
                  <Route
                    path="members"
                    element={
                      <Suspense fallback={<PageFallback />}>
                        <Members />
                      </Suspense>
                    }
                  />
                  <Route
                    path="members/:memberId"
                    element={
                      <Suspense fallback={<PageFallback />}>
                        <MemberDetail />
                      </Suspense>
                    }
                  />
                </Route>
              </Route>

              <Route element={<ProtectedRoute requirePermission={COURSES_READ} />}>
                <Route element={<Layout />}>
                  <Route
                    path="courses"
                    element={
                      <Suspense fallback={<PageFallback />}>
                        <Courses />
                      </Suspense>
                    }
                  />
                  <Route
                    path="courses/:courseId/scorecard"
                    element={
                      <Suspense fallback={<PageFallback />}>
                        <Scorecard />
                      </Suspense>
                    }
                  />
                </Route>
              </Route>

              <Route element={<ProtectedRoute requirePermission={STAFF_READ} />}>
                <Route element={<Layout />}>
                  <Route
                    path="staff"
                    element={
                      <Suspense fallback={<PageFallback />}>
                        <Staff />
                      </Suspense>
                    }
                  />
                </Route>
              </Route>

              <Route element={<ProtectedRoute requirePermission={PRODUCTS_READ} />}>
                <Route element={<Layout />}>
                  <Route
                    path="pro-shop"
                    element={
                      <Suspense fallback={<PageFallback />}>
                        <ProShop />
                      </Suspense>
                    }
                  />
                </Route>
              </Route>

              <Route element={<ProtectedRoute requirePermission={TABS_READ} />}>
                <Route element={<Layout />}>
                  <Route
                    path="tabs"
                    element={
                      <Suspense fallback={<PageFallback />}>
                        <Tabs />
                      </Suspense>
                    }
                  />
                </Route>
              </Route>

              <Route element={<ProtectedRoute requirePermission={TOURNAMENTS_READ} />}>
                <Route element={<Layout />}>
                  <Route
                    path="tournaments"
                    element={
                      <Suspense fallback={<PageFallback />}>
                        <Tournaments />
                      </Suspense>
                    }
                  />
                </Route>
              </Route>

              <Route element={<ProtectedRoute requirePermission={MAINTENANCE_READ} />}>
                <Route element={<Layout />}>
                  <Route
                    path="maintenance"
                    element={
                      <Suspense fallback={<PageFallback />}>
                        <Maintenance />
                      </Suspense>
                    }
                  />
                </Route>
              </Route>

              <Route element={<ProtectedRoute requirePermission={IMPORT_RUN} />}>
                <Route element={<Layout />}>
                  <Route
                    path="import"
                    element={
                      <Suspense fallback={<PageFallback />}>
                        <ImportPage />
                      </Suspense>
                    }
                  />
                </Route>
              </Route>
            </Route>
          </Routes>
        </StoreProvider>
      </ToasterProvider>
    </ErrorBoundary>
  );
}
