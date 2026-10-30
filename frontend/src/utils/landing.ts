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

/** Where the user lands after login; falls back to the app default. */
export function getLandingPath(user: Pick<Employee, 'defaultPath'> | null | undefined): string {
  const candidate = user?.defaultPath;
  return candidate && APP_LANDING_PATHS.includes(candidate) ? candidate : DEFAULT_LANDING_PATH;
}
