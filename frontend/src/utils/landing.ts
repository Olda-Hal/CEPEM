import { Employee } from '../types';

/**
 * SPA routes an admin may assign as a role's landing page. Keep in sync with
 * `RoleDefaultPaths` in backend/DatabaseAPI/APIModels/RoleModels.cs.
 */
export const APP_LANDING_PATHS: readonly string[] = [
  '/patients',
  '/reservations',
  '/examinations',
  '/administration',
  '/admin',
  '/admin/activity-log',
  '/settings',
  '/vyhodnocovani'
];

export const DEFAULT_LANDING_PATH = '/patients';

/** Where the user lands after login; falls back to a role-aware default. */
export function getLandingPath(
  user: Pick<Employee, 'defaultPath' | 'roles'> | null | undefined
): string {
  const candidate = user?.defaultPath;
  if (candidate && APP_LANDING_PATHS.includes(candidate)) {
    return candidate;
  }

  // The evaluating doctor works from Vyhodnocování and has no patient agenda.
  if (user?.roles?.includes('Doctor')) {
    return '/vyhodnocovani';
  }

  // Center admins have no patient agenda either; the admin hub is their home.
  if (user?.roles?.includes('Center Admin')) {
    return '/administration';
  }

  return DEFAULT_LANDING_PATH;
}
