import React, { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { PatientFormData, PatientFormFields } from '../components/PatientFormFields';
import { apiClient } from '../utils/api';
import { IntakeFormLinkInfo, IntakeFormPrefill } from '../types';
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
  signatureDate: new Date().toISOString().slice(0, 10),
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
  familyTumorType: ''
});

/** Maps the backend prefill (patient card + last form) onto the form state. */
const applyPrefill = (
  base: PatientFormData,
  prefill: IntakeFormPrefill | undefined
): PatientFormData => {
  if (!prefill) {
    return base;
  }

  const text = (value: string | null | undefined, fallback = '') => value ?? fallback;
  const numberText = (value: number | null | undefined) =>
    value === null || value === undefined ? '' : String(value);
  const flag = (value: boolean | null | undefined) => value ?? false;

  return {
    ...base,
    firstName: text(prefill.firstName, base.firstName),
    lastName: text(prefill.lastName, base.lastName),
    dateOfBirth: prefill.dateOfBirth ? prefill.dateOfBirth.slice(0, 10) : '',
    gender: prefill.gender === 'M' || prefill.gender === 'F' ? prefill.gender : '',
    weight: numberText(prefill.weight),
    height: numberText(prefill.height),
    insuranceCompany: numberText(prefill.insuranceNumber),
    email: text(prefill.email),
    phone: text(prefill.phoneNumber),
    address: text(prefill.address),
    postalCode: text(prefill.postalCode),

    medBloodPressure: flag(prefill.medBloodPressure),
    medHeart: flag(prefill.medHeart),
    medCholesterol: flag(prefill.medCholesterol),
    medBloodThinners: flag(prefill.medBloodThinners),
    medDiabetes: flag(prefill.medDiabetes),
    medThyroid: flag(prefill.medThyroid),
    medNerves: flag(prefill.medNerves),
    medPsych: flag(prefill.medPsych),
    medDigestion: flag(prefill.medDigestion),
    medPain: flag(prefill.medPain),
    medDehydration: flag(prefill.medDehydration),
    medBreathing: flag(prefill.medBreathing),
    medAntibiotics: flag(prefill.medAntibiotics),
    medSupplements: flag(prefill.medSupplements),
    medAllergies: flag(prefill.medAllergies),

    poorSleep: flag(prefill.poorSleep),
    digestiveIssues: flag(prefill.digestiveIssues),
    physicalStress: flag(prefill.physicalStress),
    mentalStress: flag(prefill.mentalStress),
    smoking: flag(prefill.smoking),
    fatigue: flag(prefill.fatigue),
    lastMealHours: text(prefill.lastMealHours),

    hadCovid: flag(prefill.hadCovid),
    covidWhen: text(prefill.covidWhen),
    covidVaccine: flag(prefill.covidVaccine),
    vaccinesAfter2023: flag(prefill.vaccinesAfter2023),

    lastMenstruationDate: text(prefill.lastMenstruationDate),
    menstruationCycleDays: numberText(prefill.menstruationCycleDays),
    yearsSinceLastMenstruation: numberText(prefill.yearsSinceLastMenstruation),
    gaveBirth: flag(prefill.gaveBirth),
    birthCount: numberText(prefill.birthCount),
    birthWhen: text(prefill.birthWhen),
    breastfed: flag(prefill.breastfed),
    breastfeedingMonths: numberText(prefill.breastfeedingMonths),
    breastfeedingInflammation: flag(prefill.breastfeedingInflammation),
    endedWithInflammation: flag(prefill.endedWithInflammation),
    contraception: flag(prefill.contraception),
    contraceptionDuration: text(prefill.contraceptionDuration),
    estrogen: flag(prefill.estrogen),
    estrogenType: text(prefill.estrogenType),
    interruption: flag(prefill.interruption),
    interruptionCount: numberText(prefill.interruptionCount),
    miscarriage: flag(prefill.miscarriage),
    miscarriageCount: numberText(prefill.miscarriageCount),
    breastInjury: flag(prefill.breastInjury),
    mammogram: flag(prefill.mammogram),
    mammogramCount: numberText(prefill.mammogramCount),
    breastBiopsy: flag(prefill.breastBiopsy),
    breastImplants: flag(prefill.breastImplants),
    breastSurgery: flag(prefill.breastSurgery),
    breastSurgeryType: text(prefill.breastSurgeryType),
    familyTumors: flag(prefill.familyTumors),
    familyTumorType: text(prefill.familyTumorType),
    additionalHealthInfo: text(prefill.additionalHealthInfo),

    // Consent and the signature belong to this visit, never prefilled.
    confirmAccuracy: false,
    termsAccepted: false,
    signaturePlace: '',
    signatureDate: new Date().toISOString().slice(0, 10),
    signatureVector: ''
  };
};

export const IntakeFormByLinkPage: React.FC = () => {
  const { t, i18n } = useTranslation();
  const { token } = useParams<{ token: string }>();
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [linkInfo, setLinkInfo] = useState<IntakeFormLinkInfo | null>(null);
  const [form, setForm] = useState<PatientFormData>(getDefaultFormData());
  const [alreadySubmitted, setAlreadySubmitted] = useState(false);
  const [canEdit, setCanEdit] = useState(false);

  useEffect(() => {
    const loadLink = async () => {
      if (!token) {
        setError(i18n.t('intakeLink.invalidLink'));
        setLoading(false);
        return;
      }

      try {
        const info = await apiClient.get<IntakeFormLinkInfo>(
          `/api/events/intake-form-links/${token}`
        );
        setLinkInfo(info);
        setAlreadySubmitted(info.alreadySubmitted);
        setCanEdit(info.canEdit);
        setForm(applyPrefill(getDefaultFormData(info.firstName, info.lastName), info.prefill));

        // The form follows the reservation country (cs / nl / en).
        if (['cs', 'en', 'nl'].includes(info.language) && i18n.language !== info.language) {
          void i18n.changeLanguage(info.language);
        }
      } catch (err: any) {
        setError(err.message || i18n.t('intakeLink.invalidLink'));
      } finally {
        setLoading(false);
      }
    };

    loadLink();
    // Load once per token; language changes must not reset the user's edits.
  }, [token, i18n]);

  const handleChange = (field: keyof PatientFormData, value: any) => {
    setForm((prev) => ({ ...prev, [field]: value }));
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
        menstruationCycleDays: form.menstruationCycleDays
          ? parseInt(form.menstruationCycleDays)
          : undefined,
        yearsSinceLastMenstruation: form.yearsSinceLastMenstruation
          ? parseInt(form.yearsSinceLastMenstruation)
          : undefined,
        gaveBirth: form.gaveBirth,
        birthCount: form.birthCount ? parseInt(form.birthCount) : undefined,
        birthWhen: form.birthWhen || undefined,
        breastfed: form.breastfed,
        breastfeedingMonths: form.breastfeedingMonths
          ? parseInt(form.breastfeedingMonths)
          : undefined,
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
        additionalHealthInfo: form.additionalHealthInfo || undefined
      };

      await apiClient.post(`/api/events/intake-form-links/${token}/submit`, payload);
      setAlreadySubmitted(true);
      setSuccess(true);
    } catch (err: any) {
      setError(err.message || t('intakeLink.submitFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="add-patient-page">
        <div className="form-container">
          <h1>{t('common.loading')}</h1>
        </div>
      </div>
    );
  }

  if (success) {
    return (
      <div className="add-patient-page">
        <div className="form-container">
          <h1>{t('intakeLink.thankYouTitle')}</h1>
          <p>
            {canEdit
              ? t('intakeLink.thankYouEditable', {
                  date: linkInfo?.visitStartUtc
                    ? new Date(linkInfo.visitStartUtc).toLocaleString(i18n.language)
                    : t('intakeLink.visitDateFallback')
                })
              : t('intakeLink.thankYouDescription')}
          </p>
          {canEdit && (
            <div className="form-actions">
              <button type="button" className="btn-submit" onClick={() => setSuccess(false)}>
                {t('intakeLink.editAgain')}
              </button>
            </div>
          )}
        </div>
      </div>
    );
  }

  return (
    <div className="add-patient-page">
      <div className="form-container">
        <h1>{t('intakeLink.pageTitle')}</h1>
        {linkInfo && (
          <p>
            {t('intakeLink.pageSubtitle', {
              name: `${linkInfo.firstName} ${linkInfo.lastName}`.trim()
            })}
          </p>
        )}

        {linkInfo?.visitStartUtc && (
          <p className="intake-notice">
            {t('intakeLink.visitDate', {
              date: new Date(linkInfo.visitStartUtc).toLocaleString(i18n.language)
            })}
          </p>
        )}

        {alreadySubmitted && <p className="intake-notice">{t('intakeLink.alreadySubmitted')}</p>}

        {error && <div className="error-message">{error}</div>}

        <form onSubmit={handleSubmit} className="patient-form">
          <PatientFormFields
            form={form}
            onChange={handleChange}
            disabled={submitting}
            showPhoto={false}
          />

          <div className="form-actions">
            <button type="submit" disabled={submitting} className="btn-submit">
              {submitting
                ? t('intakeLink.submitting')
                : alreadySubmitted
                  ? t('intakeLink.update')
                  : t('intakeLink.submit')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
