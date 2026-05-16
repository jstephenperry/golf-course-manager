import { Route, Routes } from "react-router-dom";
import { Layout } from "./components/Layout";
import { StoreProvider } from "./data/store";
import { Dashboard } from "./pages/Dashboard";
import { TeeTimes } from "./pages/TeeTimes";
import { Members } from "./pages/Members";
import { Courses } from "./pages/Courses";
import { Staff } from "./pages/Staff";
import { ProShop } from "./pages/ProShop";
import { Tabs } from "./pages/Tabs";
import { Tournaments } from "./pages/Tournaments";
import { Maintenance } from "./pages/Maintenance";

export default function App() {
  return (
    <StoreProvider>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<Dashboard />} />
          <Route path="tee-times" element={<TeeTimes />} />
          <Route path="members" element={<Members />} />
          <Route path="courses" element={<Courses />} />
          <Route path="staff" element={<Staff />} />
          <Route path="pro-shop" element={<ProShop />} />
          <Route path="tabs" element={<Tabs />} />
          <Route path="tournaments" element={<Tournaments />} />
          <Route path="maintenance" element={<Maintenance />} />
          <Route path="*" element={<Dashboard />} />
        </Route>
      </Routes>
    </StoreProvider>
  );
}
