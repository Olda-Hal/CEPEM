import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { Doctor, AuthContextType, LoginResponse } from '../types';
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
  const [user, setUser] = useState<Doctor | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
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
      const doctor = await apiClient.get<Doctor>('/api/doctors/me');
      setUser(doctor);
    } catch (error) {
      console.error(t('errors.loadingUser'), error);
      logout();
    } finally {
      setLoading(false);
    }
  };

  const login = async (email: string, password: string): Promise<boolean> => {
    try {
      const response = await apiClient.post<LoginResponse>('/api/auth/login', {
        email,
        password,
      });

      setToken(response.token);
      localStorage.setItem('authToken', response.token);
      await loadCurrentUser();
      return true;
    } catch (error) {
      console.error(t('errors.loginError'), error);
      return false;
    }
  };

  const logout = () => {
    setUser(null);
    setToken(null);
    localStorage.removeItem('authToken');
  };

  const isAuthenticated = !!user && !!token;

  const value: AuthContextType = {
    user,
    token,
    login,
    logout,
    isAuthenticated,
    loading,
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
};
