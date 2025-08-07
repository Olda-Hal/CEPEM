export const en = {
  // Login Page
  login: {
    title: "Center for Preventive Medicine",
    subtitle: "Healthcare Administration System",
    description: "Login for doctors",
    email: "Email",
    password: "Password",
    emailPlaceholder: "your.email@centrum-pm.cz",
    passwordPlaceholder: "Your password",
    loginButton: "Sign In",
    loggingIn: "Signing in...",
    invalidCredentials: "Invalid login credentials"
  },

  // Dashboard Page
  dashboard: {
    title: "Center for Preventive Medicine",
    subtitle: "Healthcare Administration System",
    welcome: "Welcome, {firstName} {lastName}",
    logout: "Sign Out",
    systemOverview: "System Overview",
    welcomeMessage: "Welcome to the Center for Preventive Medicine healthcare system administration interface.",
    
    // Profile Card
    myProfile: "My Profile",
    specialization: "Specialization: {specialization}",
    email: "Email: {email}",
    
    // Stats Card
    systemStats: "System Statistics",
    loading: "Loading...",
    totalDoctors: "{count} doctors",
    totalInSystem: "Total in system",
    status: "Status: {status}",
    
    // Time Card
    timeAndLogin: "Time and Login",
    currentTime: "Current time",
    lastLogin: "Last login: {time}",
    never: "Never",
    
    // System Info Card
    systemInfo: "System Information",
    systemVersion: "v1.0.0",
    version: "System version",
    license: "License: {licenseNumber}",
    
    // Quick Actions
    quickActions: "Quick Actions",
    patients: "Patients",
    patientsDesc: "Patient management and records",
    appointments: "Appointments",
    appointmentsDesc: "Appointment management and scheduling",
    reports: "Reports",
    reportsDesc: "Medical reports and documentation",
    settings: "Settings",
    settingsDesc: "System and profile configuration",
    comingSoon: "Coming Soon"
  },

  // Auth Context
  auth: {
    mustBeUsedInsideProvider: "useAuth must be used within AuthProvider"
  },

  // API Errors
  errors: {
    loadingUser: "Error loading user",
    loginFailed: "Login failed",
    loadingStats: "Error loading statistics"
  }
};

export type TranslationKeys = typeof en;
