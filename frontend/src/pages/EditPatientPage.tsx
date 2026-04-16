import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { apiClient } from '../utils/api';
import { AppHeader } from '../components/AppHeader';
import { PatientDetail, UpdatePatientRequest } from '../types';
import './EditPatientPage.css';

interface PatientEditForm {
  firstName: string;
  lastName: string;
  titleBefore: string;
  titleAfter: string;
  uid: string;
  birthDate: string;
  gender: string;
  insuranceNumber: string;
  email: string;
  phoneNumber: string;
  alive: boolean;
  comment: string;
}

interface IntakeSummary {
  weight: string | null;
  height: string | null;
  medications: string[];
  healthStatus: string[];
  covid: string[];
  gynecology: string[];
  notes: string | null;
}

const formatFieldLabel = (key: string): string => {
  return key
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/^./, (char) => char.toUpperCase());
};

const formatFieldValue = (value: unknown, t: (key: string) => string): string => {
  if (value === null || value === undefined || value === '') {
    return t('common.notProvided');
  }

  if (typeof value === 'boolean') {
    return value ? t('common.yes') : t('common.no');
  }

  if (typeof value === 'number') {
    return String(value);
  }

  if (typeof value === 'string') {
    return value;
  }

  return JSON.stringify(value);
};

const getInitialForm = (): PatientEditForm => ({
  firstName: '',
  lastName: '',
  titleBefore: '',
  titleAfter: '',
  uid: '',
  birthDate: '',
  gender: 'M',
  insuranceNumber: '',
  email: '',
  phoneNumber: '',
  alive: true,
  comment: ''
});

const parseIntakeSummary = (comment?: string | null): IntakeSummary => {
  const summary: IntakeSummary = {
    weight: null,
    height: null,
    medications: [],
    healthStatus: [],
    covid: [],
    gynecology: [],
    notes: null
  };

  if (!comment) {
    return summary;
  }

  comment
    .split('\n')
    .map(line => line.trim())
    .filter(Boolean)
    .forEach(line => {
      if (line.startsWith('Váha:')) {
        summary.weight = line.replace('Váha:', '').trim();
        return;
      }

      if (line.startsWith('Výška:')) {
        summary.height = line.replace('Výška:', '').trim();
        return;
      }

      if (line.startsWith('Léky:')) {
        summary.medications.push(line.replace('Léky:', '').trim());
        return;
      }

      if (line.startsWith('Zdravotní stav:')) {
        summary.healthStatus.push(line.replace('Zdravotní stav:', '').trim());
        return;
      }

      if (line.startsWith('COVID:') || line.startsWith('Vakcinace')) {
        summary.covid.push(line);
        return;
      }

      if (
        line.startsWith('Poslední menzes:') ||
        line.startsWith('Cyklus opakování:') ||
        line.startsWith('Roky od poslední menzes:') ||
        line.startsWith('Rodila:') ||
        line.startsWith('Kojila:') ||
        line.startsWith('Záněty při kojení:') ||
        line.startsWith('Končilo kojení zánětem:') ||
        line.startsWith('Antikoncepce:') ||
        line.startsWith('Estrogen:') ||
        line.startsWith('Interrupce:') ||
        line.startsWith('Potrat:') ||
        line.startsWith('Úraz prsu:') ||
        line.startsWith('RTG mamograf:') ||
        line.startsWith('Biopsie prsu:') ||
        line.startsWith('Implantáty:') ||
        line.startsWith('Operace prsu:') ||
        line.startsWith('Nádory v rodině:')
      ) {
        summary.gynecology.push(line);
        return;
      }

      if (line.startsWith('Poznámky:')) {
        summary.notes = line.replace('Poznámky:', '').trim();
      }
    });

  return summary;
};

const formSubmissionToIntakeSummary = (formSubmission?: PatientDetail['formSubmission'] | null): IntakeSummary => {
  const summary: IntakeSummary = {
    weight: null,
    height: null,
    medications: [],
    healthStatus: [],
    covid: [],
    gynecology: [],
    notes: null
  };

  if (!formSubmission) {
    return summary;
  }

  const medicationList: string[] = [];
  
  if (formSubmission.medication?.medBloodPressure) medicationList.push('Léky na krevní tlak');
  if (formSubmission.medication?.medHeart) medicationList.push('Léky na srdce');
  if (formSubmission.medication?.medCholesterol) medicationList.push('Léky na cholesterol');
  if (formSubmission.medication?.medBloodThinners) medicationList.push('Antikoagulancia');
  if (formSubmission.medication?.medDiabetes) medicationList.push('Léky na cukrovku');
  if (formSubmission.medication?.medThyroid) medicationList.push('Léky na štítnou žlázu');
  if (formSubmission.medication?.medNerves) medicationList.push('Léky na nervy');
  if (formSubmission.medication?.medPsych) medicationList.push('Psychofarmaká');
  if (formSubmission.medication?.medDigestion) medicationList.push('Léky na trávení');
  if (formSubmission.medication?.medPain) medicationList.push('Bolest léky');
  if (formSubmission.medication?.medDehydration) medicationList.push('Hydratace léky');
  if (formSubmission.medication?.medBreathing) medicationList.push('Léky na dýchání');
  if (formSubmission.medication?.medAntibiotics) medicationList.push('Antibiotika');
  if (formSubmission.medication?.medSupplements) medicationList.push('Doplňky stravy');
  if (formSubmission.medication?.medAllergies) medicationList.push('Léky na alergie');

  summary.medications = medicationList;

  const healthStatusList: string[] = [];
  
  if (formSubmission.lifestyle?.poorSleep) healthStatusList.push('Špatný spánek');
  if (formSubmission.lifestyle?.digestiveIssues) healthStatusList.push('Zažívací problémy');
  if (formSubmission.lifestyle?.physicalStress) healthStatusList.push('Fyzický stres');
  if (formSubmission.lifestyle?.mentalStress) healthStatusList.push('Duševní stres');
  if (formSubmission.lifestyle?.smoking) healthStatusList.push('Kouření');
  if (formSubmission.lifestyle?.fatigue) healthStatusList.push('Únava');

  summary.healthStatus = healthStatusList;
  summary.notes = formSubmission.lifestyle?.additionalHealthInfo || null;

  const covidList: string[] = [];
  formSubmission.sicknessHistories?.forEach(history => {
    if (history.hadSickness) {
      covidList.push(`${history.sicknessName}: ano`);
    }
    if (history.vaccinated) {
      covidList.push(`Vakcinace (${history.sicknessName}): ${history.vaccinationWhen || 'ano'}`);
    }
  });
  summary.covid = covidList;

  const gynecologyList: string[] = [];

  if (formSubmission.reproductiveHealth?.lastMenstruationDate) gynecologyList.push(`Poslední menzes: ${formSubmission.reproductiveHealth.lastMenstruationDate}`);
  if (formSubmission.reproductiveHealth?.menstruationCycleDays) gynecologyList.push(`Cyklus opakování: ${formSubmission.reproductiveHealth.menstruationCycleDays} dní`);
  if (formSubmission.reproductiveHealth?.yearsSinceLastMenstruation) gynecologyList.push(`Roky od poslední menzes: ${formSubmission.reproductiveHealth.yearsSinceLastMenstruation}`);
  if (formSubmission.reproductiveHealth?.gaveBirth) gynecologyList.push(`Rodila: ano${formSubmission.reproductiveHealth.birthCount ? ` (${formSubmission.reproductiveHealth.birthCount}x)` : ''}`);
  if (formSubmission.reproductiveHealth?.breastfed) gynecologyList.push(`Kojila: ano${formSubmission.reproductiveHealth.breastfeedingMonths ? ` (${formSubmission.reproductiveHealth.breastfeedingMonths} měsíců)` : ''}`);
  if (formSubmission.reproductiveHealth?.breastfeedingInflammation) gynecologyList.push('Záněty při kojení: ano');
  if (formSubmission.reproductiveHealth?.endedWithInflammation) gynecologyList.push('Končilo kojení zánětem: ano');
  if (formSubmission.reproductiveHealth?.contraception) gynecologyList.push(`Antikoncepce: ano${formSubmission.reproductiveHealth.contraceptionDuration ? ` (${formSubmission.reproductiveHealth.contraceptionDuration})` : ''}`);
  if (formSubmission.reproductiveHealth?.estrogen) gynecologyList.push(`Estrogen: ano${formSubmission.reproductiveHealth.estrogenType ? ` (${formSubmission.reproductiveHealth.estrogenType})` : ''}`);
  if (formSubmission.reproductiveHealth?.interruption) gynecologyList.push(`Interrupce: ano${formSubmission.reproductiveHealth.interruptionCount ? ` (${formSubmission.reproductiveHealth.interruptionCount}x)` : ''}`);
  if (formSubmission.reproductiveHealth?.miscarriage) gynecologyList.push(`Potrat: ano${formSubmission.reproductiveHealth.miscarriageCount ? ` (${formSubmission.reproductiveHealth.miscarriageCount}x)` : ''}`);
  if (formSubmission.reproductiveHealth?.breastInjury) gynecologyList.push('Úraz prsu: ano');
  if (formSubmission.reproductiveHealth?.mammogram) gynecologyList.push(`RTG mamograf: ano${formSubmission.reproductiveHealth.mammogramCount ? ` (${formSubmission.reproductiveHealth.mammogramCount}x)` : ''}`);
  if (formSubmission.reproductiveHealth?.breastBiopsy) gynecologyList.push('Biopsie prsu: ano');
  if (formSubmission.reproductiveHealth?.breastImplants) gynecologyList.push('Implantáty: ano');
  if (formSubmission.reproductiveHealth?.breastSurgery) gynecologyList.push(`Operace prsu: ano${formSubmission.reproductiveHealth.breastSurgeryType ? ` (${formSubmission.reproductiveHealth.breastSurgeryType})` : ''}`);
  if (formSubmission.reproductiveHealth?.familyTumors) gynecologyList.push(`Nádory v rodině: ano${formSubmission.reproductiveHealth.familyTumorType ? ` (${formSubmission.reproductiveHealth.familyTumorType})` : ''}`);

  summary.gynecology = gynecologyList;

  return summary;
};

export const EditPatientPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t } = useTranslation();

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [patient, setPatient] = useState<PatientDetail | null>(null);
  const [form, setForm] = useState<PatientEditForm>(getInitialForm());

  const intakeSummary = useMemo(() => {
    if (!patient) {
      return parseIntakeSummary(null);
    }
    
    if (patient.formSubmission) {
      return formSubmissionToIntakeSummary(patient.formSubmission);
    }
    
    return parseIntakeSummary(patient?.comment ?? null);
  }, [patient]);

  useEffect(() => {
    if (!id) {
      setError(t('errors.patientNotFound'));
      setLoading(false);
      return;
    }

    const loadPatient = async () => {
      try {
        setLoading(true);
        const patientDetail = await apiClient.get<PatientDetail>(`/api/patients/${id}/detail`);
        setPatient(patientDetail);
        setForm({
          firstName: patientDetail.firstName ?? '',
          lastName: patientDetail.lastName ?? '',
          titleBefore: patientDetail.titleBefore ?? '',
          titleAfter: patientDetail.titleAfter ?? '',
          uid: patientDetail.uid ?? '',
          birthDate: patientDetail.birthDate ? patientDetail.birthDate.slice(0, 10) : '',
          gender: patientDetail.gender ?? 'M',
          insuranceNumber: String(patientDetail.insuranceNumber ?? ''),
          email: patientDetail.email ?? '',
          phoneNumber: patientDetail.phoneNumber ?? '',
          alive: patientDetail.alive,
          comment: patientDetail.comment ?? ''
        });
        setError(null);
      } catch (loadError) {
        console.error('Error loading patient for edit:', loadError);
        setError(t('errors.loadingPatientDetail'));
      } finally {
        setLoading(false);
      }
    };

    loadPatient();
  }, [id, t]);

  const handleFormChange = (field: keyof PatientEditForm, value: string | boolean) => {
    setForm(prev => ({ ...prev, [field]: value }));
    if (successMessage) {
      setSuccessMessage(null);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!id) {
      setError(t('errors.patientNotFound'));
      return;
    }

    if (!form.firstName.trim() || !form.lastName.trim() || !form.uid.trim() || !form.birthDate) {
      setError(t('patients.errors.createFailed'));
      return;
    }

    const parsedInsuranceNumber = Number(form.insuranceNumber);
    if (!Number.isFinite(parsedInsuranceNumber)) {
      setError(t('patients.errors.insuranceNumberInvalid'));
      return;
    }

    const payload: UpdatePatientRequest = {
      firstName: form.firstName.trim(),
      lastName: form.lastName.trim(),
      birthDate: form.birthDate,
      gender: form.gender,
      insuranceNumber: parsedInsuranceNumber,
      uid: form.uid.trim(),
      titleBefore: form.titleBefore.trim() || undefined,
      titleAfter: form.titleAfter.trim() || undefined,
      email: form.email.trim() || undefined,
      phoneNumber: form.phoneNumber.trim() || undefined,
      alive: form.alive,
      comment: form.comment
    };

    try {
      setSaving(true);
      setError(null);
      await apiClient.put(`/api/patients/${id}`, payload);
      setSuccessMessage(t('patients.updateSuccess'));
    } catch (saveError: any) {
      console.error('Error updating patient:', saveError);
      setError(saveError?.message || t('patients.updateFailed'));
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="edit-patient-page">
        <AppHeader sectionTitle={t('patients.editPatient')} />
        <div className="edit-patient-state">{t('common.loading')}</div>
      </div>
    );
  }

  if (!patient) {
    return (
      <div className="edit-patient-page">
        <AppHeader sectionTitle={t('patients.editPatient')} />
        <div className="edit-patient-state error">{error || t('errors.patientNotFound')}</div>
      </div>
    );
  }

  return (
    <div className="edit-patient-page">
      <AppHeader sectionTitle={t('patients.editPatient')}>
        <button className="edit-patient-header-btn" onClick={() => navigate(`/patients/${id}`)}>
          {t('patients.viewDetails')}
        </button>
        <button className="edit-patient-header-btn" onClick={() => navigate('/patients')}>
          {t('patients.backToPatients')}
        </button>
      </AppHeader>

      <div className="edit-patient-container">
        <div className="edit-patient-meta">
          <span>ID: {patient.id}</span>
          <span>{t('patients.createdAt')}: {new Date(patient.createdAt).toLocaleDateString()}</span>
        </div>

        {error && <div className="edit-patient-alert error">{error}</div>}
        {successMessage && <div className="edit-patient-alert success">{successMessage}</div>}

        <form className="edit-patient-form" onSubmit={handleSubmit}>
          <div className="edit-grid">
            <label>
              {t('patients.titleBefore')}
              <input
                type="text"
                value={form.titleBefore}
                onChange={(e) => handleFormChange('titleBefore', e.target.value)}
              />
            </label>

            <label>
              {t('patients.titleAfter')}
              <input
                type="text"
                value={form.titleAfter}
                onChange={(e) => handleFormChange('titleAfter', e.target.value)}
              />
            </label>

            <label>
              {t('patients.firstName')}
              <input
                type="text"
                value={form.firstName}
                onChange={(e) => handleFormChange('firstName', e.target.value)}
                required
              />
            </label>

            <label>
              {t('patients.lastName')}
              <input
                type="text"
                value={form.lastName}
                onChange={(e) => handleFormChange('lastName', e.target.value)}
                required
              />
            </label>

            <label>
              {t('patients.uid')}
              <input
                type="text"
                value={form.uid}
                onChange={(e) => handleFormChange('uid', e.target.value)}
                required
              />
            </label>

            <label>
              {t('patients.birthDate')}
              <input
                type="date"
                value={form.birthDate}
                onChange={(e) => handleFormChange('birthDate', e.target.value)}
                required
              />
            </label>

            <label>
              {t('patients.gender')}
              <select
                value={form.gender}
                onChange={(e) => handleFormChange('gender', e.target.value)}
              >
                <option value="M">{t('patients.male')}</option>
                <option value="F">{t('patients.female')}</option>
              </select>
            </label>

            <label>
              {t('patients.insuranceNumber')}
              <input
                type="number"
                value={form.insuranceNumber}
                onChange={(e) => handleFormChange('insuranceNumber', e.target.value)}
                required
              />
            </label>

            <label>
              {t('patients.email')}
              <input
                type="email"
                value={form.email}
                onChange={(e) => handleFormChange('email', e.target.value)}
              />
            </label>

            <label>
              {t('patients.phone')}
              <input
                type="tel"
                value={form.phoneNumber}
                onChange={(e) => handleFormChange('phoneNumber', e.target.value)}
              />
            </label>

            <label className="alive-toggle">
              <input
                type="checkbox"
                checked={form.alive}
                onChange={(e) => handleFormChange('alive', e.target.checked)}
              />
              {t('patients.alive')}
            </label>
          </div>

          <label className="comment-field">
            {t('patients.comment')}
            <textarea
              rows={8}
              value={form.comment}
              onChange={(e) => handleFormChange('comment', e.target.value)}
            />
          </label>

          <div className="edit-actions">
            <button type="button" className="secondary" onClick={() => navigate(`/patients/${id}`)}>
              {t('common.cancel')}
            </button>
            <button type="submit" className="primary" disabled={saving}>
              {saving ? t('common.creating') : t('common.save')}
            </button>
          </div>
        </form>

        <div className="patient-overview-card">
          <div className="patient-overview-header">
            <h2>{t('patients.patientDetail')}</h2>
            <div className="patient-overview-subtitle">{patient.fullName}</div>
          </div>

          <div className="patient-overview-grid">
            <div className="overview-panel">
              <h3>{t('patients.title')}</h3>
              <div className="overview-row"><span>{t('patients.firstName')}</span><strong>{patient.firstName}</strong></div>
              <div className="overview-row"><span>{t('patients.lastName')}</span><strong>{patient.lastName}</strong></div>
              <div className="overview-row"><span>{t('patients.titleBefore')}</span><strong>{patient.titleBefore || t('common.notProvided')}</strong></div>
              <div className="overview-row"><span>{t('patients.titleAfter')}</span><strong>{patient.titleAfter || t('common.notProvided')}</strong></div>
              <div className="overview-row"><span>{t('patients.uid')}</span><strong>{patient.uid}</strong></div>
              <div className="overview-row"><span>{t('patients.birthDate')}</span><strong>{new Date(patient.birthDate).toLocaleDateString()}</strong></div>
              <div className="overview-row"><span>{t('patients.gender')}</span><strong>{t(`patients.genders.${patient.gender.toLowerCase()}`)}</strong></div>
              <div className="overview-row"><span>{t('patients.status')}</span><strong>{patient.alive ? t('patients.alive') : t('patients.deceased')}</strong></div>
              <div className="overview-row"><span>{t('patients.insuranceNumber')}</span><strong>{patient.insuranceNumber}</strong></div>
              <div className="overview-row"><span>{t('patients.email')}</span><strong>{patient.email || t('common.notProvided')}</strong></div>
              <div className="overview-row"><span>{t('patients.phone')}</span><strong>{patient.phoneNumber || t('common.notProvided')}</strong></div>
            </div>

            <div className="overview-panel">
              <h3>{t('patients.quickPreview')}</h3>
              <div className="overview-row"><span>{t('patients.covidVaccination')}</span><strong>{patient.quickPreview.hasCovidVaccination ? t('common.yes') : t('common.no')}</strong></div>
              <div className="overview-row"><span>{t('patients.fluVaccination')}</span><strong>{patient.quickPreview.hasFluVaccination ? t('common.yes') : t('common.no')}</strong></div>
              <div className="overview-row"><span>{t('patients.diabetes')}</span><strong>{patient.quickPreview.hasDiabetes ? t('common.yes') : t('common.no')}</strong></div>
              <div className="overview-row"><span>{t('patients.hypertension')}</span><strong>{patient.quickPreview.hasHypertension ? t('common.yes') : t('common.no')}</strong></div>
              <div className="overview-row"><span>{t('patients.heartDisease')}</span><strong>{patient.quickPreview.hasHeartDisease ? t('common.yes') : t('common.no')}</strong></div>
              <div className="overview-row"><span>{t('patients.allergies')}</span><strong>{patient.quickPreview.hasAllergies ? t('common.yes') : t('common.no')}</strong></div>
              <div className="overview-row"><span>{t('patients.recentEvents')}</span><strong>{patient.quickPreview.recentEventsCount}</strong></div>
              <div className="overview-row"><span>{t('patients.upcomingAppointments')}</span><strong>{patient.quickPreview.upcomingAppointmentsCount}</strong></div>
              <div className="overview-row"><span>{t('patients.lastVisit')}</span><strong>{patient.quickPreview.lastVisit ? new Date(patient.quickPreview.lastVisit).toLocaleString() : t('common.notProvided')}</strong></div>
            </div>

            <div className="overview-panel full-width">
              <h3>{t('patients.intakeForm')}</h3>
              <div className="intake-grid compact">
                <div className="overview-row"><span>{t('patients.weight')}</span><strong>{intakeSummary.weight || t('common.notProvided')}</strong></div>
                <div className="overview-row"><span>{t('patients.height')}</span><strong>{intakeSummary.height || t('common.notProvided')}</strong></div>
              </div>
              <div className="overview-subsection">
                <h4>{t('patients.medications')}</h4>
                <p>{intakeSummary.medications.length > 0 ? intakeSummary.medications.join(', ') : t('common.notProvided')}</p>
              </div>
              <div className="overview-subsection">
                <h4>{t('patients.healthStatus')}</h4>
                <p>{intakeSummary.healthStatus.length > 0 ? intakeSummary.healthStatus.join(', ') : t('common.notProvided')}</p>
              </div>
              <div className="overview-subsection">
                <h4>{t('patients.covidAndVaccination')}</h4>
                <p>{intakeSummary.covid.length > 0 ? intakeSummary.covid.join(' | ') : t('common.notProvided')}</p>
              </div>
              <div className="overview-subsection">
                <h4>{t('patients.gynecology')}</h4>
                <p>{intakeSummary.gynecology.length > 0 ? intakeSummary.gynecology.join(' | ') : t('common.notProvided')}</p>
              </div>
              <div className="overview-subsection">
                <h4>{t('patients.notes')}</h4>
                <p>{intakeSummary.notes || t('common.notProvided')}</p>
              </div>
            </div>

            <div className="overview-panel full-width">
              <h3>Structured FormSubmission (all fields)</h3>
              {patient.formSubmission ? (
                <>
                  <div className="overview-row"><span>Submission ID</span><strong>{patient.formSubmission.id}</strong></div>
                  <div className="overview-row"><span>Patient ID</span><strong>{patient.formSubmission.patientId}</strong></div>
                  <div className="overview-row"><span>Event ID</span><strong>{patient.formSubmission.eventId}</strong></div>
                  <div className="overview-row"><span>Submitted At</span><strong>{new Date(patient.formSubmission.submittedAtUtc).toLocaleString()}</strong></div>

                  <div className="overview-subsection">
                    <h4>Medication</h4>
                    {patient.formSubmission.medication ? (
                      Object.entries(patient.formSubmission.medication).map(([key, value]) => (
                        <div className="overview-row" key={`med-${key}`}>
                          <span>{formatFieldLabel(key)}</span>
                          <strong>{formatFieldValue(value, t)}</strong>
                        </div>
                      ))
                    ) : (
                      <p>{t('common.notProvided')}</p>
                    )}
                  </div>

                  <div className="overview-subsection">
                    <h4>Lifestyle</h4>
                    {patient.formSubmission.lifestyle ? (
                      Object.entries(patient.formSubmission.lifestyle).map(([key, value]) => (
                        <div className="overview-row" key={`life-${key}`}>
                          <span>{formatFieldLabel(key)}</span>
                          <strong>{formatFieldValue(value, t)}</strong>
                        </div>
                      ))
                    ) : (
                      <p>{t('common.notProvided')}</p>
                    )}
                  </div>

                  <div className="overview-subsection">
                    <h4>Reproductive Health</h4>
                    {patient.formSubmission.reproductiveHealth ? (
                      Object.entries(patient.formSubmission.reproductiveHealth).map(([key, value]) => (
                        <div className="overview-row" key={`repro-${key}`}>
                          <span>{formatFieldLabel(key)}</span>
                          <strong>{formatFieldValue(value, t)}</strong>
                        </div>
                      ))
                    ) : (
                      <p>{t('common.notProvided')}</p>
                    )}
                  </div>

                  <div className="overview-subsection">
                    <h4>Consent</h4>
                    {patient.formSubmission.consent ? (
                      <>
                        {Object.entries(patient.formSubmission.consent).filter(([key]) => key !== 'signatureVector').map(([key, value]) => (
                          <div className="overview-row" key={`consent-${key}`}>
                            <span>{formatFieldLabel(key)}</span>
                            <strong>{formatFieldValue(value, t)}</strong>
                          </div>
                        ))}
                        <div className="overview-subsection">
                          <h4>Signature Vector Raw Data</h4>
                          <pre className="raw-data-pre">{patient.formSubmission.consent.signatureVector || t('common.notProvided')}</pre>
                        </div>
                      </>
                    ) : (
                      <p>{t('common.notProvided')}</p>
                    )}
                  </div>

                  <div className="overview-subsection">
                    <h4>Sickness Histories</h4>
                    {patient.formSubmission.sicknessHistories?.length ? (
                      patient.formSubmission.sicknessHistories.map((history, idx) => (
                        <div className="simple-list-item" key={`history-${history.id}-${idx}`}>
                          {Object.entries(history).map(([key, value]) => (
                            <div className="overview-row" key={`history-${idx}-${key}`}>
                              <span>{formatFieldLabel(key)}</span>
                              <strong>{formatFieldValue(value, t)}</strong>
                            </div>
                          ))}
                        </div>
                      ))
                    ) : (
                      <p>{t('common.notProvided')}</p>
                    )}
                  </div>
                </>
              ) : (
                <p>{t('common.notProvided')}</p>
              )}
            </div>

            <div className="overview-panel full-width">
              <h3>{t('patients.comment')}</h3>
              <pre className="patient-comment-pre">{patient.comment || t('common.notProvided')}</pre>
            </div>

            <div className="overview-panel full-width">
              <h3>{t('patients.events')} ({patient.events.length})</h3>
              {patient.events.length > 0 ? (
                <div className="timeline-list">
                  {patient.events.map(event => (
                    <div key={event.id} className="timeline-item">
                      <div className="timeline-main">
                        <strong>{event.eventTypeName}</strong>
                        <span>{new Date(event.happenedAt).toLocaleString()}</span>
                      </div>
                      <div className="timeline-details">
                        {event.happenedTo && <div>{t('patients.eventUntil')}: {new Date(event.happenedTo).toLocaleString()}</div>}
                        {event.comment && <pre>{event.comment}</pre>}
                        {event.drugUses.length > 0 && <div><strong>{t('patients.drugUses')}:</strong> {event.drugUses.join(', ')}</div>}
                        {event.symptoms.length > 0 && <div><strong>{t('patients.symptoms')}:</strong> {event.symptoms.join(', ')}</div>}
                        {event.injuries.length > 0 && <div><strong>{t('patients.injuries')}:</strong> {event.injuries.join(', ')}</div>}
                        {event.vaccines.length > 0 && <div><strong>{t('patients.vaccines')}:</strong> {event.vaccines.join(', ')}</div>}
                        {event.hasPregnancy && <div><strong>{t('patients.pregnancy')}</strong></div>}
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <p>{t('patients.noEvents')}</p>
              )}
            </div>

            <div className="overview-panel full-width">
              <h3>{t('patients.appointments')} ({patient.appointments.length})</h3>
              {patient.appointments.length > 0 ? (
                <div className="simple-list">
                  {patient.appointments.map(appointment => (
                    <div key={appointment.id} className="simple-list-item">
                      <strong>{new Date(appointment.startTime).toLocaleString()} - {new Date(appointment.endTime).toLocaleString()}</strong>
                      <span>{appointment.doctorName}</span>
                      <span>{appointment.hospitalName}{appointment.equipmentName ? ` · ${appointment.equipmentName}` : ''}</span>
                    </div>
                  ))}
                </div>
              ) : (
                <p>{t('patients.noAppointments')}</p>
              )}
            </div>

            <div className="overview-panel full-width">
              <h3>{t('patients.documents.title')} ({patient.documents.length})</h3>
              {patient.documents.length > 0 ? (
                <div className="simple-list">
                  {patient.documents.map(document => (
                    <div key={document.id} className="simple-list-item">
                      <strong>{document.fileName}</strong>
                      <span>{new Date(document.uploadedAt).toLocaleString()}</span>
                      <span>{document.fileSize} B</span>
                    </div>
                  ))}
                </div>
              ) : (
                <p>{t('patients.documents.noDocuments')}</p>
              )}
            </div>

            <div className="overview-panel full-width">
              <h3>Raw Patient JSON (complete payload)</h3>
              <pre className="raw-data-pre">{JSON.stringify(patient, null, 2)}</pre>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
