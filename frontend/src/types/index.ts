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
  photoUrl?: string;
}

export interface PatientSearchResponse {
  patients: Patient[];
  totalCount: number;
  hasMore: boolean;
}

export interface UpdatePatientRequest {
  firstName: string;
  lastName: string;
  birthDate: string;
  phoneNumber?: string;
  email?: string;
  insuranceNumber: number;
  gender: string;
  uid: string;
  titleBefore?: string;
  titleAfter?: string;
  alive: boolean;
  comment?: string;
}

export interface PatientQuickPreview {
  hasCovidVaccination: boolean;
  hasFluVaccination: boolean;
  hasDiabetes: boolean;
  hasHypertension: boolean;
  hasHeartDisease: boolean;
  hasAllergies: boolean;
  recentEventsCount: number;
  upcomingAppointmentsCount: number;
  lastVisit?: string;
  lastVisitType?: string;
}

export interface QuickPreviewSettings {
  showCovidVaccination: boolean;
  showFluVaccination: boolean;
  showDiabetes: boolean;
  showHypertension: boolean;
  showHeartDisease: boolean;
  showAllergies: boolean;
  showRecentEvents: boolean;
  showUpcomingAppointments: boolean;
  showLastVisit: boolean;
}

export interface SicknessHistory {
  id: number;
  sicknessName: string;
  hadSickness?: boolean;
  sicknessWhen?: string;
  vaccinated?: boolean;
  vaccinationWhen?: string;
  notes?: string;
}

export interface FormSubmissionMedication {
  id: number;
  medBloodPressure: boolean;
  medHeart: boolean;
  medCholesterol: boolean;
  medBloodThinners: boolean;
  medDiabetes: boolean;
  medThyroid: boolean;
  medNerves: boolean;
  medPsych: boolean;
  medDigestion: boolean;
  medPain: boolean;
  medDehydration: boolean;
  medBreathing: boolean;
  medAntibiotics: boolean;
  medSupplements: boolean;
  medAllergies: boolean;
}

export interface FormSubmissionLifestyle {
  id: number;
  poorSleep: boolean;
  digestiveIssues: boolean;
  physicalStress: boolean;
  mentalStress: boolean;
  smoking: boolean;
  fatigue: boolean;
  lastMealHours?: number;
  vaccinesAfter2023: boolean;
  additionalHealthInfo?: string;
}

export interface FormSubmissionReproductiveHealth {
  id: number;
  lastMenstruationDate?: string;
  menstruationCycleDays?: number;
  yearsSinceLastMenstruation?: number;
  gaveBirth: boolean;
  birthCount?: number;
  birthWhen?: string;
  breastfed: boolean;
  breastfeedingMonths?: number;
  breastfeedingInflammation: boolean;
  endedWithInflammation: boolean;
  contraception: boolean;
  contraceptionDuration?: string;
  estrogen: boolean;
  estrogenType?: string;
  interruption: boolean;
  interruptionCount?: number;
  miscarriage: boolean;
  miscarriageCount?: number;
  breastInjury: boolean;
  mammogram: boolean;
  mammogramCount?: number;
  breastBiopsy: boolean;
  breastImplants: boolean;
  breastSurgery: boolean;
  breastSurgeryType?: string;
  familyTumors: boolean;
  familyTumorType?: string;
}

export interface FormSubmissionConsent {
  id: number;
  confirmAccuracy: boolean;
  termsAccepted: boolean;
  signaturePlace?: string;
  signatureDate?: string;
  signatureVector?: string;
}

export interface FormSubmission {
  id: number;
  patientId: number;
  eventId: number;
  submittedAtUtc: string;
  medication?: FormSubmissionMedication;
  lifestyle?: FormSubmissionLifestyle;
  reproductiveHealth?: FormSubmissionReproductiveHealth;
  consent?: FormSubmissionConsent;
  sicknessHistories: SicknessHistory[];
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
  photoUrl?: string;
  quickPreview: PatientQuickPreview;
  quickPreviewSettings: QuickPreviewSettings;
  formSubmission?: FormSubmission;
  events: PatientEvent[];
  appointments: PatientAppointment[];
  documents: PatientDocument[];
}

export interface ExaminationDocument {
  id: number;
  fileName: string;
  uploadedAt: string;
  fileSize: number;
}

export interface ExaminationWithDocuments {
  id: number;
  name: string;
  documents: ExaminationDocument[];
}

export interface PatientEvent {
  id: number;
  eventTypeName: string;
  happenedAt: string;
  happenedTo?: string;
  comment?: string;
  drugUses: string[];
  examinations: ExaminationWithDocuments[];
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

export interface PatientDocument {
  id: number;
  fileName: string;
  uploadedAt: string;
  fileSize: number;
}

export interface EventType {
  id: number;
  name: string;
}

export interface Drug {
  id: number;
  name: string;
}

export interface DrugCategory {
  id: number;
  name: string;
}

export interface ExaminationType {
  id: number;
  name: string;
}

export interface Symptom {
  id: number;
  name: string;
}

export interface InjuryType {
  id: number;
  name: string;
}

export interface VaccineType {
  id: number;
  name: string;
}

export interface EventOptions {
  eventTypes: EventType[];
  drugs: Drug[];
  drugCategories: DrugCategory[];
  examinationTypes: ExaminationType[];
  symptoms: Symptom[];
  injuryTypes: InjuryType[];
  vaccineTypes: VaccineType[];
}

export interface DrugUseRequest {
  drugId: number;
  categoryIds: number[];
}

export interface CreateEventRequest {
  patientId: number;
  eventTypeId: number;
  happenedAt: string;
  happenedTo?: string;
  comment?: string;
  eventGroupId?: string;
  drugUses: DrugUseRequest[];
  examinationTypeIds: number[];
  symptomIds: number[];
  injuryTypeIds: number[];
  vaccineTypeIds: number[];
  isPregnant?: boolean;
  pregnancyResult?: boolean;
}

export interface CreateEventGroupRequest {
  patientId: number;
  events: CreateEventRequest[];
}

export interface CreateEventGroupResponse {
  eventGroupId: string;
  eventIds: number[];
}

export interface ExaminationRoom {
  id: number;
  name: string;
  description?: string;
  hospitalId: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface Hospital {
  id: number;
  name?: string;
  address?: {
    street?: string;
    city?: string;
    postalCode?: string;
    country?: string;
  };
  active?: boolean;
  companyIco?: string;
  companyName?: string;
  parentHospitalId?: number;
}

export interface DoctorExaminationRoom {
  id: number;
  doctorId: number;
  examinationRoomId: number;
  roomName: string;
  hospitalId: number;
  assignedAt: string;
}

export interface DoctorHospital {
  id: number;
  name: string;
  address?: string;
}

export interface DoctorRoom {
  id: number;
  name: string;
  description?: string;
  hospitalId: number;
}

export interface RoomDoctor {
  id: number;
  fullName: string;
}

export interface Reservation {
  id: number;
  doctorId: number;
  doctorName?: string;
  personId: number;
  personName?: string;
  examinationRoomId: number;
  roomName?: string;
  examinationTypeId: number;
  examinationTypeName?: string;
  startDateTime: string;
  endDateTime: string;
  notes?: string;
  status: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateReservationPersonRequest {
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  email?: string;
}

export interface CreateReservationRequest {
  doctorId: number;
  personId?: number;
  newPerson?: CreateReservationPersonRequest;
  examinationRoomId: number;
  examinationTypeId: number;
  startDateTime: string;
  endDateTime: string;
  notes?: string;
}

export interface UpdateReservationRequest {
  startDateTime?: string;
  endDateTime?: string;
  notes?: string;
  status?: string;
}

export interface CreateIntakeFormLinkRequest {
  personId: number;
  reservationId?: number;
  expiresInHours?: number;
}

export interface IntakeFormLinkResponse {
  linkId: number;
  token: string;
  intakePath: string;
  expiresAtUtc: string;
  personId: number;
  personName: string;
  reservationId?: number;
}

export interface IntakeFormLinkInfo {
  personId: number;
  firstName: string;
  lastName: string;
  reservationId?: number;
  expiresAtUtc: string;
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
