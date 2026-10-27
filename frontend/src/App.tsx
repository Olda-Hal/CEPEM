import React, { useEffect } from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './contexts/AuthContext';
import { ToastProvider } from './contexts/ToastContext';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ProtectedRoute } from './components/ProtectedRoute';
import { RoleRoute } from './components/RoleRoute';
import { themeManager } from './themes/ThemeManager';
import './themes/colors.css';
import './themes/theme-overrides.css';
const LoginPage = React.lazy(() => import('./pages/LoginPage').then(m => { return { default: m.LoginPage }; }));
const DashboardPage = React.lazy(() => import('./pages/DashboardPage').then(m => { return { default: m.DashboardPage }; }));
const TestDashboard = React.lazy(() => import('./pages/TestDashboard'));
const AdminEmployeesPage = React.lazy(() => import('./pages/AdminEmployeesPage'));
const ActivityLogPage = React.lazy(() => import('./pages/ActivityLogPage'));
const RoleManagementPage = React.lazy(() => import('./pages/RoleManagementPage'));
const CenterManagementPage = React.lazy(() => import('./pages/CenterManagementPage'));
const PatientsPage = React.lazy(() => import('./pages/PatientsPage').then(m => { return { default: m.PatientsPage }; }));
const PatientDetailPage = React.lazy(() => import('./pages/PatientDetailPage').then(m => { return { default: m.PatientDetailPage }; }));
const AddPatientFormPage = React.lazy(() => import('./pages/AddPatientFormPage').then(m => { return { default: m.AddPatientFormPage }; }));
const EditPatientPage = React.lazy(() => import('./pages/EditPatientPage').then(m => { return { default: m.EditPatientPage }; }));
const ReservationSlotsPage = React.lazy(() => import('./pages/ReservationSlotsPage'));
const ExaminationsPage = React.lazy(() => import('./pages/ExaminationsPage').then(m => { return { default: m.ExaminationsPage }; }));
const IntakeFormByLinkPage = React.lazy(() => import('./pages/IntakeFormByLinkPage').then(m => { return { default: m.IntakeFormByLinkPage }; }));
import './App.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      staleTime: 30_000,
      refetchOnWindowFocus: false,
    },
  },
});

function App() {
  // Initialize theme system
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
          <React.Suspense fallback={<div className="loading-container"><div className="loading-spinner">...</div></div>}>
        <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/intake/:token" element={<IntakeFormByLinkPage />} />
            <Route 
              path="/dashboard" 
              element={
                <ProtectedRoute>
                  <DashboardPage />
                </ProtectedRoute>
              } 
            />
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
              path="/admin/centers" 
              element={
                <RoleRoute roles={['Center Admin', 'Country Admin']}>
                  <CenterManagementPage />
                </RoleRoute>
              } 
            />
            <Route 
              path="/patients" 
              element={
                <ProtectedRoute>
                  <PatientsPage />
                </ProtectedRoute>
              } 
            />
            <Route 
              path="/patients/add" 
              element={
                <ProtectedRoute>
                  <AddPatientFormPage />
                </ProtectedRoute>
              } 
            />
            <Route 
              path="/patients/:id" 
              element={
                <ProtectedRoute>
                  <PatientDetailPage />
                </ProtectedRoute>
              } 
            />
            <Route 
              path="/patients/:id/edit" 
              element={
                <ProtectedRoute>
                  <EditPatientPage />
                </ProtectedRoute>
              } 
            />
            <Route 
              path="/reservations" 
              element={
                <ProtectedRoute>
                  <ReservationSlotsPage />
                </ProtectedRoute>
              } 
            />
            <Route 
              path="/examinations" 
              element={
                <ProtectedRoute>
                  <ExaminationsPage />
                </ProtectedRoute>
              } 
            />
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
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
