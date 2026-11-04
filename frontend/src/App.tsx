import React, { useEffect } from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import { getLandingPath } from './utils/landing';
import { ToastProvider } from './contexts/ToastContext';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ProtectedRoute } from './components/ProtectedRoute';
import { RoleRoute } from './components/RoleRoute';
import { AppShell } from './components/layouts/AppShell';
import { ErrorBoundary } from './components/ErrorBoundary';
import { initializeTheme } from './themes/theme';
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
const EmailLogPage = React.lazy(() => import('./pages/EmailLogPage'));
const GdprPage = React.lazy(() => import('./pages/GdprPage'));
const AnalyticsPage = React.lazy(() => import('./pages/AnalyticsPage'));
const CommunicationTemplatesPage = React.lazy(() => import('./pages/CommunicationTemplatesPage'));
const MonitoringPage = React.lazy(() => import('./pages/MonitoringPage'));
const RoleManagementPage = React.lazy(() => import('./pages/RoleManagementPage'));
const CenterManagementPage = React.lazy(() => import('./pages/CenterManagementPage'));
const HealthDashboardPage = React.lazy(() => import('./pages/HealthDashboardPage'));
const AdministrationPage = React.lazy(() => import('./pages/AdministrationPage'));
const EvaluationPage = React.lazy(() => import('./pages/EvaluationPage'));
const MeasurementsPage = React.lazy(() => import('./pages/MeasurementsPage'));
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
const PublicGdprPage = React.lazy(() => import('./pages/PublicGdprPage'));
const NotFoundPage = React.lazy(() => import('./pages/NotFoundPage'));

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

/** Role-aware landing route: Doctor → Vyhodnocování, everyone else → Patients. */
const LandingRedirect: React.FC = () => {
  const { user, loading } = useAuth();
  if (loading) {
    return null;
  }
  return <Navigate to={getLandingPath(user)} replace />;
};

function App() {
  useEffect(() => {
    initializeTheme();
  }, []);

  return (
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <AuthProvider>
          <Router>
            <div className="App">
              <ErrorBoundary>
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
                    <Route path="/gdpr" element={<PublicGdprPage />} />
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
                          <RoleRoute roles={[]} sysAdminOnly>
                            <TestDashboard />
                          </RoleRoute>
                        }
                      />
                      <Route
                        path="/admin/employees"
                        element={
                          <RoleRoute roles={['Center Admin', 'Country Admin']}>
                            <AdminEmployeesPage />
                          </RoleRoute>
                        }
                      />
                      <Route
                        path="/admin/monitoring"
                        element={
                          <RoleRoute roles={[]} sysAdminOnly>
                            <MonitoringPage />
                          </RoleRoute>
                        }
                      />
                      <Route
                        path="/admin/activity-log"
                        element={
                          <RoleRoute roles={[]} sysAdminOnly>
                            <ActivityLogPage />
                          </RoleRoute>
                        }
                      />
                      <Route
                        path="/admin/email-log"
                        element={
                          <RoleRoute roles={[]} sysAdminOnly>
                            <EmailLogPage />
                          </RoleRoute>
                        }
                      />
                      <Route
                        path="/admin/gdpr"
                        element={
                          <RoleRoute roles={[]}>
                            <GdprPage />
                          </RoleRoute>
                        }
                      />
                      <Route
                        path="/admin/analytics"
                        element={
                          <RoleRoute roles={[]}>
                            <AnalyticsPage />
                          </RoleRoute>
                        }
                      />
                      <Route
                        path="/admin/communication"
                        element={
                          <RoleRoute roles={[]}>
                            <CommunicationTemplatesPage />
                          </RoleRoute>
                        }
                      />
                      <Route
                        path="/admin/roles"
                        element={
                          <RoleRoute roles={[]} sysAdminOnly>
                            <RoleManagementPage />
                          </RoleRoute>
                        }
                      />
                      <Route
                        path="/admin/health"
                        element={
                          <RoleRoute roles={[]} sysAdminOnly>
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
                      <Route
                        path="/administration"
                        element={
                          <RoleRoute roles={['Center Admin', 'Country Admin']}>
                            <AdministrationPage />
                          </RoleRoute>
                        }
                      />
                      <Route
                        path="/admin"
                        element={
                          <RoleRoute roles={[]}>
                            <AdminPage />
                          </RoleRoute>
                        }
                      />
                      <Route path="/settings" element={<SettingsPage />} />
                      {/* The evaluating doctor has no patient agenda. */}
                      <Route
                        path="/patients"
                        element={
                          <RoleRoute roles={['Country Admin']} allowAny excludeRoles={['Doctor']}>
                            <PatientsPage />
                          </RoleRoute>
                        }
                      />
                      <Route
                        path="/patients/add"
                        element={
                          <RoleRoute roles={['Country Admin']} allowAny excludeRoles={['Doctor']}>
                            <AddPatientFormPage />
                          </RoleRoute>
                        }
                      />
                      <Route
                        path="/patients/:id"
                        element={
                          <RoleRoute roles={['Country Admin']} allowAny excludeRoles={['Doctor']}>
                            <PatientDetailPage />
                          </RoleRoute>
                        }
                      />
                      <Route
                        path="/patients/:id/visits/:visitId"
                        element={
                          <RoleRoute roles={['Country Admin']} allowAny excludeRoles={['Doctor']}>
                            <VisitDetailPage />
                          </RoleRoute>
                        }
                      />
                      <Route
                        path="/patients/:id/edit"
                        element={
                          <RoleRoute roles={['Country Admin']} allowAny excludeRoles={['Doctor']}>
                            <EditPatientPage />
                          </RoleRoute>
                        }
                      />
                      <Route
                        path="/reservations"
                        element={
                          <RoleRoute roles={['Center Admin', 'Country Admin']}>
                            <ReservationSlotsPage />
                          </RoleRoute>
                        }
                      />
                      <Route
                        path="/examinations"
                        element={
                          <RoleRoute
                            roles={['Center Admin', 'Country Admin']}
                            allowAny
                            excludeRoles={['Doctor', 'Examiner']}
                          >
                            <ExaminationsPage />
                          </RoleRoute>
                        }
                      />
                      <Route
                        path="/mereni"
                        element={
                          <RoleRoute
                            roles={['Examiner', 'Digitiser']}
                            allowAny
                            excludeRoles={['Doctor']}
                          >
                            <MeasurementsPage />
                          </RoleRoute>
                        }
                      />
                      <Route
                        path="/vyhodnocovani"
                        element={
                          <RoleRoute roles={['Doctor', 'Country Admin']}>
                            <EvaluationPage />
                          </RoleRoute>
                        }
                      />
                      <Route path="*" element={<NotFoundPage />} />
                    </Route>
                    <Route path="/dashboard" element={<LandingRedirect />} />
                    <Route path="/" element={<LandingRedirect />} />
                  </Routes>
                </React.Suspense>
              </ErrorBoundary>
            </div>
          </Router>
        </AuthProvider>
      </ToastProvider>
    </QueryClientProvider>
  );
}

export default App;
