import React, { useEffect } from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './contexts/AuthContext';
import { ToastProvider } from './contexts/ToastContext';
import { ProtectedRoute } from './components/ProtectedRoute';
import { RoleRoute } from './components/RoleRoute';
import { LoginPage } from './pages/LoginPage';
import { DashboardPage } from './pages/DashboardPage';
import TestDashboard from './pages/TestDashboard';
import AdminEmployeesPage from './pages/AdminEmployeesPage';
import RoleManagementPage from './pages/RoleManagementPage';
import CenterManagementPage from './pages/CenterManagementPage';
import { PatientsPage } from './pages/PatientsPage';
import { PatientDetailPage } from './pages/PatientDetailPage';
import { AddPatientFormPage } from './pages/AddPatientFormPage';
import { EditPatientPage } from './pages/EditPatientPage';
import { ReservationSlotsPage } from './pages/ReservationSlotsPage';
import { ExaminationsPage } from './pages/ExaminationsPage';
import { IntakeFormByLinkPage } from './pages/IntakeFormByLinkPage';
import { themeManager } from './themes/ThemeManager';
import './themes/colors.css';
import './themes/theme-overrides.css';
import './App.css';

function App() {
  // Initialize theme system
  useEffect(() => {
    // Theme manager automatically handles initialization
    themeManager.getCurrentTheme();
  }, []);

  return (
    <ToastProvider>
      <AuthProvider>
        <Router>
        <div className="App">
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
        </div>
        </Router>
      </AuthProvider>
    </ToastProvider>
  );
}

export default App;
