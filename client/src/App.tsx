import { Suspense, lazy } from "react";
import { Route, Routes } from "react-router-dom";
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
                path="tee-times"
                element={
                  <Suspense fallback={<PageFallback />}>
                    <TeeTimes />
                  </Suspense>
                }
              />
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
              <Route
                path="courses"
                element={
                  <Suspense fallback={<PageFallback />}>
                    <Courses />
                  </Suspense>
                }
              />
              <Route
                path="staff"
                element={
                  <Suspense fallback={<PageFallback />}>
                    <Staff />
                  </Suspense>
                }
              />
              <Route
                path="pro-shop"
                element={
                  <Suspense fallback={<PageFallback />}>
                    <ProShop />
                  </Suspense>
                }
              />
              <Route
                path="tabs"
                element={
                  <Suspense fallback={<PageFallback />}>
                    <Tabs />
                  </Suspense>
                }
              />
              <Route
                path="tournaments"
                element={
                  <Suspense fallback={<PageFallback />}>
                    <Tournaments />
                  </Suspense>
                }
              />
              <Route
                path="maintenance"
                element={
                  <Suspense fallback={<PageFallback />}>
                    <Maintenance />
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
          </Routes>
        </StoreProvider>
      </ToasterProvider>
    </ErrorBoundary>
  );
}
