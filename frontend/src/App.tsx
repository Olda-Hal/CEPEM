import React, { useEffect } from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './contexts/AuthContext';
import { ProtectedRoute } from './components/ProtectedRoute';
import { LoginPage } from './pages/LoginPage';
import { DashboardPage } from './pages/DashboardPage';
import TestDashboard from './pages/TestDashboard';
import AdminEmployeesPage from './pages/AdminEmployeesPage';
import { PatientsPage } from './pages/PatientsPage';
import { PatientDetailPage } from './pages/PatientDetailPage';
import { AddPatientFormPage } from './pages/AddPatientFormPage';
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
    <AuthProvider>
      <Router>
        <div className="App">
          <Routes>
            <Route path="/login" element={<LoginPage />} />
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
                <ProtectedRoute>
                  <TestDashboard />
                </ProtectedRoute>
              } 
            />
            <Route 
              path="/admin/employees" 
              element={
                <ProtectedRoute>
                  <AdminEmployeesPage />
                </ProtectedRoute>
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
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
          </Routes>
        </div>
      </Router>
    </AuthProvider>
  );
}

export default App;
