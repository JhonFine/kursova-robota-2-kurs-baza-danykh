import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider } from './auth/AuthContext';
import { RequireAuth, RequireRoles } from './auth/Guards';
import { AppShell } from './layout/AppShell';
import { AdminPage } from './pages/AdminPage';
import { ClientsPage } from './pages/ClientsPage';
import { DamagesPage } from './pages/DamagesPage';
import { FleetPage } from './pages/FleetPage';
import { ForbiddenPage } from './pages/ForbiddenPage';
import { HomeRedirectPage } from './pages/HomeRedirectPage';
import { LoginPage } from './pages/LoginPage';
import { MaintenancePage } from './pages/MaintenancePage';
import { ProkatBookingsPage } from './pages/ProkatBookingsPage';
import { ProkatPage } from './pages/ProkatPage';
import { ProkatProfilePage } from './pages/ProkatProfilePage';
import { ProkatSearchPage } from './pages/ProkatSearchPage';
import { RentalsPage } from './pages/RentalsPage';
import { ReportsPage } from './pages/ReportsPage';

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/forbidden" element={<ForbiddenPage />} />

          <Route
            path="/"
            element={(
              <RequireAuth>
                <AppShell />
              </RequireAuth>
            )}
          >
            <Route index element={<HomeRedirectPage />} />
            <Route
              path="prokat"
              element={(
                <RequireRoles roles={['User']}>
                  <ProkatPage />
                </RequireRoles>
              )}
            />
            <Route
              path="prokat/search"
              element={(
                <RequireRoles roles={['User']}>
                  <ProkatSearchPage />
                </RequireRoles>
              )}
            />
            <Route
              path="prokat/bookings"
              element={(
                <RequireRoles roles={['User']}>
                  <ProkatBookingsPage />
                </RequireRoles>
              )}
            />
            <Route
              path="prokat/profile"
              element={(
                <RequireRoles roles={['User']}>
                  <ProkatProfilePage />
                </RequireRoles>
              )}
            />
            <Route
              path="rentals"
              element={(
                <RequireRoles roles={['Admin', 'Manager']}>
                  <RentalsPage />
                </RequireRoles>
              )}
            />
            <Route
              path="fleet"
              element={(
                <RequireRoles roles={['Admin', 'Manager']}>
                  <FleetPage />
                </RequireRoles>
              )}
            />
            <Route
              path="clients"
              element={(
                <RequireRoles roles={['Admin', 'Manager']}>
                  <ClientsPage />
                </RequireRoles>
              )}
            />
            <Route
              path="maintenance"
              element={(
                <RequireRoles roles={['Admin', 'Manager']}>
                  <MaintenancePage />
                </RequireRoles>
              )}
            />
            <Route
              path="damages"
              element={(
                <RequireRoles roles={['Admin', 'Manager']}>
                  <DamagesPage />
                </RequireRoles>
              )}
            />
            <Route
              path="reports"
              element={(
                <RequireRoles roles={['Admin', 'Manager']}>
                  <ReportsPage />
                </RequireRoles>
              )}
            />
            <Route
              path="admin"
              element={(
                <RequireRoles roles={['Admin']}>
                  <AdminPage />
                </RequireRoles>
              )}
            />
            <Route path="*" element={<HomeRedirectPage />} />
          </Route>

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
