import { Employee } from '../types';

export const hasRole = (user: Employee | null, roleName: string): boolean => {
  if (!user || !user.roles) return false;
  return user.roles.includes(roleName);
};

/** Full system administrator: the only role allowed in the system-only area. */
export const isSysAdmin = (user: Employee | null): boolean => {
  return hasRole(user, 'SysAdmin');
};

/**
 * Operator with full agenda access. "Admin" intentionally has no system-only
 * area (roles, tests, audit log, monitoring, health); it may open the
 * operational overview, GDPR and communication templates.
 */
export const isAdmin = (user: Employee | null): boolean => {
  return isSysAdmin(user) || hasRole(user, 'Admin');
};
