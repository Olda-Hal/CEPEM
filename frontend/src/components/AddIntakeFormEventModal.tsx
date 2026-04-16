import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { apiClient } from '../utils/api';
import { PatientFormFields, PatientFormData } from './PatientFormFields';
import './AddIntakeFormEventModal.css';

interface AddIntakeFormEventModalProps {
  patientId: number;
  patientFirstName?: string;
  patientLastName?: string;
  isOpen: boolean;
  onClose: () => void;
  onEventCreated: () => void;
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

export const AddIntakeFormEventModal: React.FC<AddIntakeFormEventModalProps> = ({
  patientId,
  patientFirstName = '',
  patientLastName = '',
  isOpen,
  onClose,
  onEventCreated,
}) => {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [form, setForm] = useState<PatientFormData>(getDefaultFormData(patientFirstName, patientLastName));

  useEffect(() => {
    if (isOpen) {
      setForm(prev => ({
        ...prev,
        firstName: patientFirstName,
        lastName: patientLastName,
      }));
    }
  }, [isOpen, patientFirstName, patientLastName]);

  const handleChange = (field: keyof PatientFormData, value: any) => {
    setForm(prev => ({ ...prev, [field]: value }));
  };

  const buildComment = (): string => {
    const parts: string[] = [];

    if (form.medBloodPressure) parts.push('Léky: Tlak');
    if (form.medHeart) parts.push('Léky: Srdce');
    if (form.medCholesterol) parts.push('Léky: Cholesterol');
    if (form.medBloodThinners) parts.push('Léky: Srážlivost');
    if (form.medDiabetes) parts.push('Léky: Cukrovka');
    if (form.medThyroid) parts.push('Léky: Štítná žláza');
    if (form.medNerves) parts.push('Léky: Nervy');
    if (form.medPsych) parts.push('Léky: Psychika');
    if (form.medDigestion) parts.push('Léky: Zažívání');
    if (form.medPain) parts.push('Léky: Bolest');
    if (form.medDehydration) parts.push('Léky: Odvodnění');
    if (form.medBreathing) parts.push('Léky: Dýchání');
    if (form.medAntibiotics) parts.push('Léky: Antibiotika');
    if (form.medSupplements) parts.push('Léky: Doplňky stravy');
    if (form.medAllergies) parts.push('Léky: Alergie');

    if (form.poorSleep) parts.push('Zdravotní stav: Špatný spánek');
    if (form.digestiveIssues) parts.push('Zdravotní stav: Zažívání');
    if (form.physicalStress) parts.push('Zdravotní stav: Fyzická zátěž');
    if (form.mentalStress) parts.push('Zdravotní stav: Psychická zátěž');
    if (form.smoking) parts.push('Zdravotní stav: Kouření');
    if (form.fatigue) parts.push('Zdravotní stav: Pocity únavy');
    if (form.lastMealHours) parts.push(`Zdravotní stav: Poslední jídlo před ${form.lastMealHours} hod`);
    
    if (form.hadCovid) {
      parts.push(`COVID: Prodělal/a (${form.covidWhen || 'neuvedeno kdy'})`);
    }
    if (form.covidVaccine) parts.push('Vakcinace proti COVID: Ano');
    if (form.vaccinesAfter2023) parts.push('Vakcinace po r. 2023: Ano');
    
    if (form.lastMenstruationDate) parts.push(`Poslední menzes: ${form.lastMenstruationDate}`);
    if (form.menstruationCycleDays) parts.push(`Cyklus opakování: ${form.menstruationCycleDays} dnů`);
    if (form.yearsSinceLastMenstruation) parts.push(`Roky od poslední menzes: ${form.yearsSinceLastMenstruation}`);
    if (form.gaveBirth) parts.push(`Rodila: ${form.birthCount || '?'}x (${form.birthWhen || '?'})`);
    if (form.breastfed) parts.push(`Kojila: ${form.breastfeedingMonths || '?'} měsíců`);
    if (form.breastfeedingInflammation) parts.push('Záněty při kojení: Ano');
    if (form.endedWithInflammation) parts.push('Končilo kojení zánětem: Ano');
    if (form.contraception) parts.push(`Antikoncepce: ${form.contraceptionDuration || 'Ano'}`);
    if (form.estrogen) parts.push(`Estrogen: ${form.estrogenType || 'Ano'}`);
    if (form.interruption) parts.push(`Interrupce: ${form.interruptionCount || '?'}x`);
    if (form.miscarriage) parts.push(`Potrat: ${form.miscarriageCount || '?'}x`);
    if (form.breastInjury) parts.push('Úraz prsu: Ano');
    if (form.mammogram) parts.push(`RTG mamograf: ${form.mammogramCount || '?'}x`);
    if (form.breastBiopsy) parts.push('Biopsie prsu: Ano');
    if (form.breastImplants) parts.push('Implantáty: Ano');
    if (form.breastSurgery) parts.push(`Operace prsu: ${form.breastSurgeryType || 'Ano'}`);
    if (form.familyTumors) parts.push(`Nádory v rodině: ${form.familyTumorType || 'Ano'}`);
    
    if (form.additionalHealthInfo) parts.push(`Poznámky: ${form.additionalHealthInfo}`);
    
    return parts.join('\n');
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
        sicknessHistories: (form.hadCovid || form.covidVaccine || !!form.covidWhen)
          ? [
              {
                sicknessName: 'COVID-19',
                hadSickness: form.hadCovid,
                sicknessWhen: form.covidWhen || undefined,
                vaccinated: form.covidVaccine,
                notes: form.vaccinesAfter2023 ? 'Other vaccines after 2023: yes' : undefined,
              },
            ]
          : [],
      };

      await apiClient.post('/api/events/intake-form', payload);
      onEventCreated();
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
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content intake-form-modal" onClick={e => e.stopPropagation()}>
        <div className="modal-header">
          <h2>Nový Vstupní Formulář</h2>
          <button className="modal-close" onClick={onClose}>&times;</button>
        </div>

        {error && <div className="error-message">{error}</div>}

        <form onSubmit={handleSubmit} className="modal-form">
          <PatientFormFields 
            form={form}
            onChange={handleChange}
            disabled={loading}
            className="form-section"
          />

          <div className="modal-footer">
            <button
              type="button"
              className="btn-secondary"
              onClick={onClose}
              disabled={loading}
            >
              Zrušit
            </button>
            <button
              type="submit"
              className="btn-primary"
              disabled={loading}
            >
              {loading ? 'Vytváření...' : 'Vytvořit event'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
