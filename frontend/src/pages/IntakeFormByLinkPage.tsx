import React, { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { PatientFormData, PatientFormFields } from '../components/PatientFormFields';
import { apiClient } from '../utils/api';
import { IntakeFormLinkInfo } from '../types';
import './AddPatientFormPage.css';

const getDefaultFormData = (firstName = '', lastName = ''): PatientFormData => ({
  firstName,
  lastName,
  dateOfBirth: '',
  gender: 'M',
  weight: '',
  height: '',
  insuranceCompany: '',
  email: '',
  phone: '',
  address: '',
  postalCode: '',
  medBloodPressure: false,
  medHeart: false,
  medCholesterol: false,
  medBloodThinners: false,
  medDiabetes: false,
  medThyroid: false,
  medNerves: false,
  medPsych: false,
  medDigestion: false,
  medPain: false,
  medDehydration: false,
  medBreathing: false,
  medAntibiotics: false,
  medSupplements: false,
  medAllergies: false,
  poorSleep: false,
  digestiveIssues: false,
  physicalStress: false,
  mentalStress: false,
  smoking: false,
  fatigue: false,
  lastMealHours: '',
  confirmAccuracy: false,
  termsAccepted: false,
  signaturePlace: '',
  signatureDate: new Date().toISOString().split('T')[0],
  signatureVector: '',
  additionalHealthInfo: '',
  hadCovid: false,
  covidWhen: '',
  covidVaccine: false,
  vaccinesAfter2023: false,
  lastMenstruationDate: '',
  menstruationCycleDays: '',
  yearsSinceLastMenstruation: '',
  gaveBirth: false,
  birthCount: '',
  birthWhen: '',
  breastfed: false,
  breastfeedingMonths: '',
  breastfeedingInflammation: false,
  endedWithInflammation: false,
  contraception: false,
  contraceptionDuration: '',
  estrogen: false,
  estrogenType: '',
  interruption: false,
  interruptionCount: '',
  miscarriage: false,
  miscarriageCount: '',
  breastInjury: false,
  mammogram: false,
  mammogramCount: '',
  breastBiopsy: false,
  breastImplants: false,
  breastSurgery: false,
  breastSurgeryType: '',
  familyTumors: false,
  familyTumorType: '',
});

export const IntakeFormByLinkPage: React.FC = () => {
  const { t } = useTranslation();
  const { token } = useParams<{ token: string }>();
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [linkInfo, setLinkInfo] = useState<IntakeFormLinkInfo | null>(null);
  const [form, setForm] = useState<PatientFormData>(getDefaultFormData());

  useEffect(() => {
    const loadLink = async () => {
      if (!token) {
        setError(t('intakeLink.invalidLink'));
        setLoading(false);
        return;
      }

      try {
        const info = await apiClient.get<IntakeFormLinkInfo>(`/api/events/intake-form-links/${token}`);
        setLinkInfo(info);
        setForm(getDefaultFormData(info.firstName, info.lastName));
      } catch (err: any) {
        setError(err.message || t('intakeLink.invalidLink'));
      } finally {
        setLoading(false);
      }
    };

    loadLink();
  }, [token, t]);

  const handleChange = (field: keyof PatientFormData, value: any) => {
    setForm(prev => ({ ...prev, [field]: value }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!token) {
      setError(t('intakeLink.invalidLink'));
      return;
    }

    if (!form.confirmAccuracy) {
      setError(t('intakeLink.confirmAccuracyRequired'));
      return;
    }

    if (!form.termsAccepted) {
      setError(t('intakeLink.termsRequired'));
      return;
    }

    if (!form.signatureVector) {
      setError(t('intakeLink.signatureRequired'));
      return;
    }

    setSubmitting(true);

    try {
      const payload = {
        firstName: form.firstName || undefined,
        lastName: form.lastName || undefined,
        dateOfBirth: form.dateOfBirth || undefined,
        gender: form.gender || undefined,
        insuranceNumber: form.insuranceCompany ? parseInt(form.insuranceCompany) : undefined,
        email: form.email || undefined,
        phoneNumber: form.phone || undefined,
        address: form.address || undefined,
        postalCode: form.postalCode || undefined,
        weight: form.weight ? parseFloat(form.weight) : undefined,
        height: form.height ? parseFloat(form.height) : undefined,
        medBloodPressure: form.medBloodPressure,
        medHeart: form.medHeart,
        medCholesterol: form.medCholesterol,
        medBloodThinners: form.medBloodThinners,
        medDiabetes: form.medDiabetes,
        medThyroid: form.medThyroid,
        medNerves: form.medNerves,
        medPsych: form.medPsych,
        medDigestion: form.medDigestion,
        medPain: form.medPain,
        medDehydration: form.medDehydration,
        medBreathing: form.medBreathing,
        medAntibiotics: form.medAntibiotics,
        medSupplements: form.medSupplements,
        medAllergies: form.medAllergies,
        poorSleep: form.poorSleep,
        digestiveIssues: form.digestiveIssues,
        physicalStress: form.physicalStress,
        mentalStress: form.mentalStress,
        smoking: form.smoking,
        fatigue: form.fatigue,
        lastMealHours: form.lastMealHours || undefined,
        hadCovid: form.hadCovid,
        covidWhen: form.covidWhen || undefined,
        covidVaccine: form.covidVaccine,
        vaccinesAfter2023: form.vaccinesAfter2023,
        lastMenstruationDate: form.lastMenstruationDate || undefined,
        menstruationCycleDays: form.menstruationCycleDays ? parseInt(form.menstruationCycleDays) : undefined,
        yearsSinceLastMenstruation: form.yearsSinceLastMenstruation ? parseInt(form.yearsSinceLastMenstruation) : undefined,
        gaveBirth: form.gaveBirth,
        birthCount: form.birthCount ? parseInt(form.birthCount) : undefined,
        birthWhen: form.birthWhen || undefined,
        breastfed: form.breastfed,
        breastfeedingMonths: form.breastfeedingMonths ? parseInt(form.breastfeedingMonths) : undefined,
        breastfeedingInflammation: form.breastfeedingInflammation,
        endedWithInflammation: form.endedWithInflammation,
        contraception: form.contraception,
        contraceptionDuration: form.contraceptionDuration || undefined,
        estrogen: form.estrogen,
        estrogenType: form.estrogenType || undefined,
        interruption: form.interruption,
        interruptionCount: form.interruptionCount ? parseInt(form.interruptionCount) : undefined,
        miscarriage: form.miscarriage,
        miscarriageCount: form.miscarriageCount ? parseInt(form.miscarriageCount) : undefined,
        breastInjury: form.breastInjury,
        mammogram: form.mammogram,
        mammogramCount: form.mammogramCount ? parseInt(form.mammogramCount) : undefined,
        breastBiopsy: form.breastBiopsy,
        breastImplants: form.breastImplants,
        breastSurgery: form.breastSurgery,
        breastSurgeryType: form.breastSurgeryType || undefined,
        familyTumors: form.familyTumors,
        familyTumorType: form.familyTumorType || undefined,
        confirmAccuracy: form.confirmAccuracy,
        termsAccepted: form.termsAccepted,
        signaturePlace: form.signaturePlace || undefined,
        signatureDate: form.signatureDate || undefined,
        signatureVector: form.signatureVector,
        additionalHealthInfo: form.additionalHealthInfo || undefined,
      };

      await apiClient.post(`/api/events/intake-form-links/${token}/submit`, payload);
      setSuccess(true);
    } catch (err: any) {
      setError(err.message || t('intakeLink.submitFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return <div className="add-patient-page"><div className="form-container"><h1>{t('common.loading')}</h1></div></div>;
  }

  if (success) {
    return (
      <div className="add-patient-page">
        <div className="form-container">
          <h1>{t('intakeLink.thankYouTitle')}</h1>
          <p>{t('intakeLink.thankYouDescription')}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="add-patient-page">
      <div className="form-container">
        <h1>{t('intakeLink.pageTitle')}</h1>
        {linkInfo && <p>{t('intakeLink.pageSubtitle', { name: `${linkInfo.firstName} ${linkInfo.lastName}`.trim() })}</p>}

        {error && <div className="error-message">{error}</div>}

        <form onSubmit={handleSubmit} className="patient-form">
          <PatientFormFields form={form} onChange={handleChange} disabled={submitting} showPhoto={false} />

          <div className="form-actions">
            <button type="submit" disabled={submitting} className="btn-submit">
              {submitting ? t('intakeLink.submitting') : t('intakeLink.submit')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
