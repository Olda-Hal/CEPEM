import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import {
  Employee,
  AuthContextType,
  LoginResponse,
  CreateEmployeeRequest,
  CreateEmployeeResponse,
  ImpersonationResponse,
  ImpersonationState
} from '../types';
import {
  apiClient,
  ApiError,
  UNAUTHORIZED_EVENT,
  getAuthToken,
  IMPERSONATION_TOKEN_KEY
} from '../utils/api';
import { useTranslation } from 'react-i18next';

/**
 * SysAdmin preview ("náhled jako zaměstnanec") is tab-scoped: the preview
 * token lives in sessionStorage while the administrator's own token stays in
 * localStorage. Other tabs keep the admin identity, and closing the tab ends
 * the preview automatically.
 */
const IMPERSONATION_STORAGE_KEY = 'cepem:impersonation';

const readStoredImpersonation = (): ImpersonationState | null => {
  try {
    const raw = sessionStorage.getItem(IMPERSONATION_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<ImpersonationState>;
    if (typeof parsed.employeeId !== 'number' || typeof parsed.fullName !== 'string') {
      return null;
    }
    return {
      employeeId: parsed.employeeId,
      fullName: parsed.fullName,
      adminName: typeof parsed.adminName === 'string' ? parsed.adminName : ''
    };
  } catch {
    return null;
  }
};

const clearStoredImpersonation = () => sessionStorage.removeItem(IMPERSONATION_STORAGE_KEY);

const hasPreviewToken = () => Boolean(sessionStorage.getItem(IMPERSONATION_TOKEN_KEY));

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext);
  const { t } = useTranslation();
  if (!context) {
    throw new Error(t('auth.mustBeUsedInsideProvider'));
  }
  return context;
};

interface AuthProviderProps {
  children: ReactNode;
}

export const AuthProvider: React.FC<AuthProviderProps> = ({ children }) => {
  const [user, setUser] = useState<Employee | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [requiresPasswordChange, setRequiresPasswordChange] = useState(false);
  const [impersonation, setImpersonation] = useState<ImpersonationState | null>(null);
  const { t } = useTranslation();

  useEffect(() => {
    // A preview token without its metadata means the tab was reloaded/closed
    // in an inconsistent state — drop the preview and use the admin session.
    if (hasPreviewToken() && !readStoredImpersonation()) {
      sessionStorage.removeItem(IMPERSONATION_TOKEN_KEY);
    }

    const savedToken = getAuthToken();
    if (!savedToken) {
      clearStoredImpersonation();
      setLoading(false);
      return;
    }

    const storedImpersonation = readStoredImpersonation();
    if (storedImpersonation) {
      setImpersonation(storedImpersonation);
    }

    setToken(savedToken);
    loadCurrentUser();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Expired or invalid token mid-session: return to the admin session when a
  // preview is running, otherwise log out.
  useEffect(() => {
    const handleUnauthorized = () => {
      if (!getAuthToken()) {
        return;
      }
      if (hasPreviewToken()) {
        void stopImpersonation();
      } else {
        logout();
      }
    };
    window.addEventListener(UNAUTHORIZED_EVENT, handleUnauthorized);
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, handleUnauthorized);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const fetchCurrentUser = async (): Promise<Employee | null> => {
    try {
      return await apiClient.get<Employee>('/api/employees/me');
    } catch (error) {
      console.error(t('errors.loadingUser'), error);
      return null;
    }
  };

  const loadCurrentUser = async () => {
    const employee = await fetchCurrentUser();
    if (employee) {
      setUser(employee);
      setLoading(false);
      return;
    }

    // An expired preview token should return the admin to their own session
    // instead of dropping them at the login screen.
    if (hasPreviewToken() || readStoredImpersonation()) {
      await restoreAdminSession();
    } else {
      logout();
    }
    setLoading(false);
  };

  /** Drop the preview token and continue with the administrator's session. */
  const restoreAdminSession = async () => {
    sessionStorage.removeItem(IMPERSONATION_TOKEN_KEY);
    clearStoredImpersonation();
    setImpersonation(null);
    setRequiresPasswordChange(false);

    const adminToken = localStorage.getItem('authToken');
    setToken(adminToken);
    if (!adminToken) {
      logout();
      return;
    }

    const employee = await fetchCurrentUser();
    if (employee) {
      setUser(employee);
    } else {
      logout();
    }
  };

  const startImpersonation = async (
    employeeId: number
  ): Promise<{ success: boolean; error?: string }> => {
    try {
      const response = await apiClient.post<ImpersonationResponse>(
        `/api/admin/employees/${employeeId}/impersonate`
      );

      // Tab-scoped on purpose: the admin's localStorage session stays intact
      // for every other tab, and the preview cannot outlive this tab.
      sessionStorage.setItem(IMPERSONATION_TOKEN_KEY, response.token);
      sessionStorage.setItem(
        IMPERSONATION_STORAGE_KEY,
        JSON.stringify({
          employeeId: response.employeeId,
          fullName: response.fullName,
          adminName: response.impersonatorName
        } satisfies ImpersonationState)
      );

      setToken(response.token);
      setRequiresPasswordChange(false);
      setImpersonation({
        employeeId: response.employeeId,
        fullName: response.fullName,
        adminName: response.impersonatorName
      });

      const employee = await fetchCurrentUser();
      if (!employee) {
        await restoreAdminSession();
        return { success: false, error: t('admin.impersonationFailed') };
      }
      setUser(employee);
      return { success: true };
    } catch (error) {
      console.error('Impersonation failed', error);
      const message = error instanceof ApiError ? error.message : t('admin.impersonationFailed');
      return { success: false, error: message };
    }
  };

  const stopImpersonation = async () => {
    await restoreAdminSession();
  };

  const login = async (
    email: string,
    password: string
  ): Promise<{ success: boolean; error?: string }> => {
    try {
      sessionStorage.removeItem(IMPERSONATION_TOKEN_KEY);
      clearStoredImpersonation();
      setImpersonation(null);

      const response = await apiClient.post<LoginResponse>('/api/auth/login', {
        email,
        password
      });

      setToken(response.token);
      localStorage.setItem('authToken', response.token);
      setRequiresPasswordChange(response.requiresPasswordChange);
      await loadCurrentUser();
      return { success: true };
    } catch (error) {
      console.error(t('errors.loginError'), error);

      let errorMessage = t('login.invalidCredentials');
      if (error instanceof ApiError) {
        const serverMessage = error.message;
        if (serverMessage.includes('deaktivován') || serverMessage.includes('deactivated')) {
          errorMessage = t('login.accountDeactivated');
        } else if (error.status !== 401) {
          errorMessage = serverMessage;
        }
      }

      return { success: false, error: errorMessage };
    }
  };

  const changePassword = async (
    currentPassword: string,
    newPassword: string,
    confirmPassword: string
  ): Promise<{ success: boolean; error?: string }> => {
    try {
      await apiClient.post('/api/auth/change-password', {
        currentPassword,
        newPassword,
        confirmPassword
      });

      // After successful password change, no longer requires password change
      setRequiresPasswordChange(false);
      return { success: true };
    } catch (error) {
      console.error(t('errors.changePasswordError'), error);
      const errorMessage =
        error instanceof ApiError ? error.message : t('passwordChange.currentPasswordIncorrect');
      return { success: false, error: errorMessage };
    }
  };

  const createEmployee = async (
    employeeData: CreateEmployeeRequest
  ): Promise<CreateEmployeeResponse | null> => {
    try {
      const response = await apiClient.post<CreateEmployeeResponse>(
        '/api/auth/create-employee',
        employeeData
      );
      return response;
    } catch (error) {
      console.error(t('errors.createEmployeeError'), error);
      return null;
    }
  };

  const logout = () => {
    setUser(null);
    setToken(null);
    setRequiresPasswordChange(false);
    setImpersonation(null);
    sessionStorage.removeItem(IMPERSONATION_TOKEN_KEY);
    clearStoredImpersonation();
    localStorage.removeItem('authToken');
  };

  const isAuthenticated = !!user && !!token;

  const value: AuthContextType = {
    user,
    token,
    login,
    logout,
    changePassword,
    createEmployee,
    impersonation,
    startImpersonation,
    stopImpersonation,
    isAuthenticated,
    loading,
    requiresPasswordChange
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};
