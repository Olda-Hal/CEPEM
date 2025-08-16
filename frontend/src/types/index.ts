export interface Doctor {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  specialization: string;
  licenseNumber: string;
  isActive: boolean;
  createdAt: string;
  lastLoginAt?: string;
}

export interface Employee {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  titleBefore?: string;
  titleAfter?: string;
  uid: string;
  active: boolean;
  lastLoginAt?: string;
  fullName: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  email: string;
  fullName: string;
  specialization: string;
}

export interface DashboardStats {
  totalEmployees: number;
  lastLogin?: string;
  systemStatus: string;
}

export interface AuthContextType {
  user: Employee | null;
  token: string | null;
  login: (email: string, password: string) => Promise<boolean>;
  logout: () => void;
  isAuthenticated: boolean;
  loading: boolean;
}
