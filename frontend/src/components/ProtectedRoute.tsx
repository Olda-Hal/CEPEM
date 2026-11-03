import React from 'react';
import { Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../contexts/AuthContext';
import { PasswordChangeModal } from './PasswordChangeModal';

interface ProtectedRouteProps {
  children: React.ReactNode;
}

export const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ children }) => {
  const { t } = useTranslation();
  const { isAuthenticated, loading, requiresPasswordChange } = useAuth();

  if (loading) {
    return (
      <div className="loading-container">
        <div className="loading-spinner">{t('common.loading')}</div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  // With an expired password the gateway only allows /me, change-password and
  // logout. Do not render the app shell (and its data queries) until the
  // password is changed.
  if (requiresPasswordChange) {
    return <PasswordChangeModal isOpen={true} isForced={true} />;
  }

  return <>{children}</>;
};
