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
  passwordExpiration?: string;
  roles?: string[];
}

export interface EmployeeListItem {
  employeeId: number;
  personId: number;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  uid: string;
  gender: string;
  titleBefore?: string;
  titleAfter?: string;
  active: boolean;
  lastLoginAt?: string;
  passwordExpiration: string;
  roles: string[];
  fullName: string;
}

export interface UpdateEmployeeRequest {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  uid: string;
  gender: string;
  titleBefore?: string;
  titleAfter?: string;
  active: boolean;
  roleIds: number[];
}

export interface UpdateEmployeeResponse {
  success: boolean;
  message: string;
  employee?: EmployeeListItem;
}

export interface Role {
  id: number;
  name: string;
}

export interface CreateEmployeeRequest {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  uid: string;
  gender: string;
  password: string;
  titleBefore?: string;
  titleAfter?: string;
  active: boolean;
}

export interface CreateEmployeeResponse {
  employeeId: number;
  personId: number;
  email: string;
  fullName: string;
  uid: string;
  active: boolean;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

export interface LoginResponse {
  token: string;
  email: string;
  fullName: string;
  passwordExpiration?: string;
  requiresPasswordChange: boolean;
}

export interface DashboardStats {
  totalEmployees: number;
  lastLogin?: string;
  systemStatus: string;
}

export interface Patient {
  id: number;
  personId: number;
  firstName: string;
  lastName: string;
  birthDate: string;
  phoneNumber?: string;
  email?: string;
  insuranceNumber: number;
  gender: string;
  createdAt: string;
  uid: string;
  titleBefore?: string;
  titleAfter?: string;
  alive: boolean;
  fullName: string;
}

export interface PatientSearchResponse {
  patients: Patient[];
  totalCount: number;
  hasMore: boolean;
}

export interface PatientDetail {
  id: number;
  personId: number;
  firstName: string;
  lastName: string;
  birthDate: string;
  phoneNumber?: string;
  email?: string;
  insuranceNumber: number;
  gender: string;
  createdAt: string;
  uid: string;
  titleBefore?: string;
  titleAfter?: string;
  alive: boolean;
  fullName: string;
  age: number;
  comment?: string;
  events: PatientEvent[];
  appointments: PatientAppointment[];
}

export interface PatientEvent {
  id: number;
  eventTypeName: string;
  happenedAt: string;
  happenedTo?: string;
  comment?: string;
  drugUses: string[];
  examinations: string[];
  symptoms: string[];
  injuries: string[];
  vaccines: string[];
  hasPregnancy: boolean;
}

export interface PatientAppointment {
  id: number;
  startTime: string;
  endTime: string;
  doctorName: string;
  equipmentName?: string;
  hospitalName: string;
}

export interface AuthContextType {
  user: Employee | null;
  token: string | null;
  login: (email: string, password: string) => Promise<{ success: boolean; error?: string }>;
  logout: () => void;
  changePassword: (currentPassword: string, newPassword: string, confirmPassword: string) => Promise<{ success: boolean; error?: string }>;
  createEmployee: (employeeData: CreateEmployeeRequest) => Promise<CreateEmployeeResponse | null>;
  isAuthenticated: boolean;
  loading: boolean;
  requiresPasswordChange: boolean;
}
