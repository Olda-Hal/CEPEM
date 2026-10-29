import React, { useState, useEffect } from 'react';
import { apiClient } from '../utils/api';
import BaseModal from './BaseModal';
import { PatientFormFields, PatientFormData } from './PatientFormFields';
import './AddIntakeFormEventModal.css';

interface AddIntakeFormEventModalProps {
  patientId: number;
  patientFirstName?: string;
  patientLastName?: string;
  isOpen: boolean;
  onClose: () => void;
  onEventCreated: (eventId?: number) => void;
}

const getDefaultFormData = (firstName: string, lastName: string): PatientFormData => ({
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

export const AddIntakeFormEventModal: React.FC<AddIntakeFormEventModalProps> = ({
  patientId,
  patientFirstName = '',
  patientLastName = '',
  isOpen,
  onClose,
  onEventCreated
}) => {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [form, setForm] = useState<PatientFormData>(
    getDefaultFormData(patientFirstName, patientLastName)
  );

  useEffect(() => {
    if (isOpen) {
      setForm((prev) => ({
        ...prev,
        firstName: patientFirstName,
        lastName: patientLastName
      }));
    }
  }, [isOpen, patientFirstName, patientLastName]);

  const handleChange = (field: keyof PatientFormData, value: any) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!form.confirmAccuracy) {
      setError('Potvrďte správnost vyplněných údajů');
      return;
    }

    if (!form.termsAccepted) {
      setError('Musíte souhlasit se smluvními podmínkami');
      return;
    }

    if (!form.signatureVector) {
      setError('Podpis je povinný');
      return;
    }

    setLoading(true);
    try {
      const payload = {
        patientId,
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
        additionalHealthInfo: form.additionalHealthInfo || undefined,
        sicknessHistories:
          form.hadCovid || form.covidVaccine || !!form.covidWhen
            ? [
                {
                  sicknessName: 'COVID-19',
                  hadSickness: form.hadCovid,
                  sicknessWhen: form.covidWhen || undefined,
                  vaccinated: form.covidVaccine,
                  notes: form.vaccinesAfter2023 ? 'Other vaccines after 2023: yes' : undefined
                }
              ]
            : []
      };

      const created = await apiClient.post<{ eventId?: number }>(
        '/api/events/intake-form',
        payload
      );
      onEventCreated(created?.eventId);
      onClose();
    } catch (err: any) {
      console.error(err);
      setError(err?.response?.data?.message || 'Chyba při vytváření eventu');
    } finally {
      setLoading(false);
    }
  };

  if (!isOpen) return null;

  return (
    <BaseModal title="Nový Vstupní Formulář" onClose={onClose} contentClassName="intake-form-modal">
      {error && <div className="error-message">{error}</div>}

      <form onSubmit={handleSubmit} className="modal-form">
        <PatientFormFields
          form={form}
          onChange={handleChange}
          disabled={loading}
          className="form-section"
        />

        <div className="modal-footer">
          <button type="button" className="btn-secondary" onClick={onClose} disabled={loading}>
            Zrušit
          </button>
          <button type="submit" className="btn-primary" disabled={loading}>
            {loading ? 'Vytváření...' : 'Vytvořit event'}
          </button>
        </div>
      </form>
    </BaseModal>
  );
};
