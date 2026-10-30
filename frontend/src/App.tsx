import React, { useEffect } from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './contexts/AuthContext';
import { ToastProvider } from './contexts/ToastContext';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ProtectedRoute } from './components/ProtectedRoute';
import { RoleRoute } from './components/RoleRoute';
import { AppShell } from './components/layouts/AppShell';
import { themeManager } from './themes/ThemeManager';
import './themes/colors.css';
import './themes/theme-overrides.css';
import './components/ui/ui.css';
const LoginPage = React.lazy(() =>
  import('./pages/LoginPage').then((m) => {
    return { default: m.LoginPage };
  })
);
const ResetPasswordPage = React.lazy(() => import('./pages/ResetPasswordPage'));
const TestDashboard = React.lazy(() => import('./pages/TestDashboard'));
const AdminEmployeesPage = React.lazy(() => import('./pages/AdminEmployeesPage'));
const ActivityLogPage = React.lazy(() => import('./pages/ActivityLogPage'));
const MonitoringPage = React.lazy(() => import('./pages/MonitoringPage'));
const RoleManagementPage = React.lazy(() => import('./pages/RoleManagementPage'));
const CenterManagementPage = React.lazy(() => import('./pages/CenterManagementPage'));
const HealthDashboardPage = React.lazy(() => import('./pages/HealthDashboardPage'));
const AdministrationPage = React.lazy(() => import('./pages/AdministrationPage'));
const AdminPage = React.lazy(() => import('./pages/AdminPage'));
const SettingsPage = React.lazy(() => import('./pages/SettingsPage'));
const PatientsPage = React.lazy(() =>
  import('./pages/PatientsPage').then((m) => {
    return { default: m.PatientsPage };
  })
);
const PatientDetailPage = React.lazy(() =>
  import('./pages/PatientDetailPage').then((m) => {
    return { default: m.PatientDetailPage };
  })
);
const VisitDetailPage = React.lazy(() => import('./pages/VisitDetailPage'));
const AddPatientFormPage = React.lazy(() =>
  import('./pages/AddPatientFormPage').then((m) => {
    return { default: m.AddPatientFormPage };
  })
);
const EditPatientPage = React.lazy(() =>
  import('./pages/EditPatientPage').then((m) => {
    return { default: m.EditPatientPage };
  })
);
const ReservationSlotsPage = React.lazy(() =>
  import('./pages/ReservationSlotsPage').then((m) => {
    return { default: m.ReservationSlotsPage };
  })
);
const ExaminationsPage = React.lazy(() =>
  import('./pages/ExaminationsPage').then((m) => {
    return { default: m.ExaminationsPage };
  })
);
const IntakeFormByLinkPage = React.lazy(() =>
  import('./pages/IntakeFormByLinkPage').then((m) => {
    return { default: m.IntakeFormByLinkPage };
  })
);

import './App.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
      staleTime: 30000
    }
  }
});

function App() {
  useEffect(() => {
    // Theme manager automatically handles initialization
    themeManager.getCurrentTheme();
  }, []);

  return (
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <AuthProvider>
          <Router>
            <div className="App">
              <React.Suspense
                fallback={
                  <div className="loading-container">
                    <div className="loading-spinner">...</div>
                  </div>
                }
              >
                <Routes>
                  <Route path="/login" element={<LoginPage />} />
                  <Route path="/reset-password" element={<ResetPasswordPage />} />
                  <Route path="/intake/:token" element={<IntakeFormByLinkPage />} />
                  <Route
                    element={
                      <ProtectedRoute>
                        <AppShell />
                      </ProtectedRoute>
                    }
                  >
                    <Route
                      path="/tests"
                      element={
                        <RoleRoute roles={[]}>
                          <TestDashboard />
                        </RoleRoute>
                      }
                    />
                    <Route
                      path="/admin/employees"
                      element={
                        <RoleRoute roles={['Country Admin']}>
                          <AdminEmployeesPage />
                        </RoleRoute>
                      }
                    />
                    <Route
                      path="/admin/monitoring"
                      element={
                        <RoleRoute roles={[]}>
                          <MonitoringPage />
                        </RoleRoute>
                      }
                    />
                    <Route
                      path="/admin/activity-log"
                      element={
                        <RoleRoute roles={[]}>
                          <ActivityLogPage />
                        </RoleRoute>
                      }
                    />
                    <Route
                      path="/admin/roles"
                      element={
                        <RoleRoute roles={[]}>
                          <RoleManagementPage />
                        </RoleRoute>
                      }
                    />
                    <Route
                      path="/admin/health"
                      element={
                        <RoleRoute roles={[]}>
                          <HealthDashboardPage />
                        </RoleRoute>
                      }
                    />
                    <Route
                      path="/admin/centers"
                      element={
                        <RoleRoute roles={['Center Admin', 'Country Admin']}>
                          <CenterManagementPage />
                        </RoleRoute>
                      }
                    />
                    <Route path="/administration" element={<AdministrationPage />} />
                    <Route path="/admin" element={<AdminPage />} />
                    <Route path="/settings" element={<SettingsPage />} />
                    <Route path="/patients" element={<PatientsPage />} />
                    <Route path="/patients/add" element={<AddPatientFormPage />} />
                    <Route path="/patients/:id" element={<PatientDetailPage />} />
                    <Route path="/patients/:id/visits/:visitId" element={<VisitDetailPage />} />
                    <Route path="/patients/:id/edit" element={<EditPatientPage />} />
                    <Route path="/reservations" element={<ReservationSlotsPage />} />
                    <Route path="/examinations" element={<ExaminationsPage />} />
                  </Route>
                  <Route path="/dashboard" element={<Navigate to="/patients" replace />} />
                  <Route path="/" element={<Navigate to="/patients" replace />} />
                </Routes>
              </React.Suspense>
            </div>
          </Router>
        </AuthProvider>
      </ToastProvider>
    </QueryClientProvider>
  );
}

export default App;
