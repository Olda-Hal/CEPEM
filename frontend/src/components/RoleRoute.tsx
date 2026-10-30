import React from 'react';
import { Navigate } from 'react-router-dom';
import { ProtectedRoute } from './ProtectedRoute';
import { useAuth } from '../contexts/AuthContext';
import { hasRole, isAdmin } from '../utils/roles';

interface RoleRouteProps {
  /** Role names, any of which grants access (SysAdmin/Admin always pass) */
  roles: string[];
  /** Role names that may never open the page (SysAdmin/Admin still pass) */
  excludeRoles?: string[];
  /** Allow every authenticated user not covered by excludeRoles */
  allowAny?: boolean;
  children: React.ReactNode;
}

/**
 * Wraps ProtectedRoute with a role check. The backend enforces the real
 * permission policy; this gate only prevents users from landing on pages
 * they have no permission to use.
 */
export const RoleRoute: React.FC<RoleRouteProps> = ({
  roles,
  excludeRoles = [],
  allowAny = false,
  children
}) => {
  const { user } = useAuth();

  const admin = isAdmin(user);
  const explicitlyAllowed = roles.some((role) => hasRole(user, role));
  // An explicitly allowed role wins over an exclusion (e.g. a Center Admin
  // who is also Country Admin keeps the patient agenda).
  const excluded = !admin && !explicitlyAllowed && excludeRoles.some((role) => hasRole(user, role));
  const allowed = !excluded && (admin || allowAny || explicitlyAllowed);

  return (
    <ProtectedRoute>{allowed ? children : <Navigate to="/dashboard" replace />}</ProtectedRoute>
  );
};

export default RoleRoute;
