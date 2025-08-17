import { Employee } from '../types';

export const hasRole = (user: Employee | null, roleName: string): boolean => {
  if (!user || !user.roles) return false;
  return user.roles.includes(roleName);
};

export const isAdmin = (user: Employee | null): boolean => {
  return hasRole(user, 'SysAdmin') || hasRole(user, 'Admin');
};
