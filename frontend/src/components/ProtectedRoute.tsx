import React from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { PasswordChangeModal } from './PasswordChangeModal';

interface ProtectedRouteProps {
  children: React.ReactNode;
}

export const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ children }) => {
  const { isAuthenticated, loading, requiresPasswordChange } = useAuth();

  if (loading) {
    return (
      <div className="loading-container">
        <div className="loading-spinner">Načítání...</div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return (
    <>
      {children}
      <PasswordChangeModal 
        isOpen={requiresPasswordChange} 
        isForced={true}
      />
    </>
  );
};
