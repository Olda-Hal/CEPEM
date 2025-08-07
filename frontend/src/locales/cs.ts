export const cs = {
  // Login Page
  login: {
    title: "Centrum Preventivní Medicíny",
    subtitle: "Zdravotnický administrační systém",
    description: "Přihlášení pro doktory",
    email: "Email",
    password: "Heslo",
    emailPlaceholder: "vas.email@centrum-pm.cz",
    passwordPlaceholder: "Vaše heslo",
    loginButton: "Přihlásit se",
    loggingIn: "Přihlašování...",
    invalidCredentials: "Neplatné přihlašovací údaje"
  },

  // Dashboard Page
  dashboard: {
    title: "Centrum Preventivní Medicíny",
    subtitle: "Zdravotnický administrační systém",
    welcome: "Vítejte, {firstName} {lastName}",
    logout: "Odhlásit se",
    systemOverview: "Přehled systému",
    welcomeMessage: "Vítejte v administračním rozhraní Centra Preventivní Medicíny zdravotnického systému.",
    
    // Profile Card
    myProfile: "Můj profil",
    specialization: "Specializace: {specialization}",
    email: "Email: {email}",
    
    // Stats Card
    systemStats: "Statistiky systému",
    loading: "Načítání...",
    totalDoctors: "{count} doktorů",
    totalInSystem: "Celkem v systému",
    status: "Status: {status}",
    
    // Time Card
    timeAndLogin: "Čas a přihlášení",
    currentTime: "Aktuální čas",
    lastLogin: "Poslední přihlášení: {time}",
    never: "Nikdy",
    
    // System Info Card
    systemInfo: "Systémové informace",
    systemVersion: "v1.0.0",
    version: "Verze systému",
    license: "Licence: {licenseNumber}",
    
    // Quick Actions
    quickActions: "Rychlé akce",
    patients: "Pacienti",
    patientsDesc: "Správa pacientů a jejich záznamů",
    appointments: "Termíny",
    appointmentsDesc: "Správa a plánování termínů",
    reports: "Zprávy",
    reportsDesc: "Lékařské zprávy a dokumentace",
    settings: "Nastavení",
    settingsDesc: "Konfigurace systému a profilu",
    comingSoon: "Brzy k dispozici"
  },

  // Auth Context
  auth: {
    mustBeUsedInsideProvider: "useAuth musí být použit uvnitř AuthProvider"
  },

  // API Errors
  errors: {
    loadingUser: "Chyba při načítání uživatele",
    loginFailed: "Chyba při přihlašování",
    loadingStats: "Chyba při načítání statistik"
  }
};
