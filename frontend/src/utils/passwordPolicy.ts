/** Keep in sync with backend/Shared/PasswordPolicy.cs. */
export const PASSWORD_MIN_LENGTH = 8;

export function isValidPassword(password: string): boolean {
  return password.length >= PASSWORD_MIN_LENGTH && /[A-Za-z]/.test(password) && /\d/.test(password);
}
