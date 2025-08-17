import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { Employee, AuthContextType, LoginResponse, ChangePasswordRequest, CreateEmployeeRequest, CreateEmployeeResponse } from '../types';
import { apiClient } from '../utils/api';
import { useTranslation } from 'react-i18next';

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
  const { t } = useTranslation();

  useEffect(() => {
    const savedToken = localStorage.getItem('authToken');
    if (savedToken) {
      setToken(savedToken);
      loadCurrentUser();
    } else {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const loadCurrentUser = async () => {
    try {
      const employee = await apiClient.get<Employee>('/api/employees/me');
      setUser(employee);
    } catch (error) {
      console.error(t('errors.loadingUser'), error);
      logout();
    } finally {
      setLoading(false);
    }
  };

  const login = async (email: string, password: string): Promise<{ success: boolean; error?: string }> => {
    try {
      const response = await apiClient.post<LoginResponse>('/api/auth/login', {
        email,
        password,
      });

      setToken(response.token);
      localStorage.setItem('authToken', response.token);
      setRequiresPasswordChange(response.requiresPasswordChange);
      await loadCurrentUser();
      return { success: true };
    } catch (error: any) {
      console.error(t('errors.loginError'), error);
      
      // Extract error message from response
      let errorMessage = t('login.invalidCredentials');
      if (error.response?.data) {
        const serverMessage = error.response.data;
        if (typeof serverMessage === 'string') {
          // Check if it's the deactivated account message
          if (serverMessage.includes('deaktivován') || serverMessage.includes('deactivated')) {
            errorMessage = t('login.accountDeactivated');
          } else {
            errorMessage = serverMessage;
          }
        }
      }
      
      return { success: false, error: errorMessage };
    }
  };

  const changePassword = async (currentPassword: string, newPassword: string, confirmPassword: string): Promise<{ success: boolean; error?: string }> => {
    try {
      await apiClient.post('/api/auth/change-password', {
        currentPassword,
        newPassword,
        confirmPassword,
      });
      
      // After successful password change, no longer requires password change
      setRequiresPasswordChange(false);
      return { success: true };
    } catch (error: any) {
      console.error(t('errors.changePasswordError'), error);
      const errorMessage = error.response?.data || t('passwordChange.currentPasswordIncorrect');
      return { success: false, error: errorMessage };
    }
  };

  const createEmployee = async (employeeData: CreateEmployeeRequest): Promise<CreateEmployeeResponse | null> => {
    try {
      const response = await apiClient.post<CreateEmployeeResponse>('/api/auth/create-employee', employeeData);
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
    isAuthenticated,
    loading,
    requiresPasswordChange,
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
};
