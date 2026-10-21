import React from 'react';
import { Navigate } from 'react-router-dom';
import { ProtectedRoute } from './ProtectedRoute';
import { useAuth } from '../contexts/AuthContext';
import { hasRole, isAdmin } from '../utils/roles';

interface RoleRouteProps {
  /** Role names, any of which grants access (SysAdmin always passes) */
  roles: string[];
  children: React.ReactNode;
}

/**
 * Wraps ProtectedRoute with a role check. The backend enforces the real
 * permission policy; this gate only prevents users from landing on pages
 * they have no permission to use.
 */
export const RoleRoute: React.FC<RoleRouteProps> = ({ roles, children }) => {
  const { user } = useAuth();

  const allowed = isAdmin(user) || roles.some((role) => hasRole(user, role));

  return (
    <ProtectedRoute>
      {allowed ? (
        children
      ) : (
        <Navigate to="/dashboard" replace />
      )}
    </ProtectedRoute>
  );
};

export default RoleRoute;
