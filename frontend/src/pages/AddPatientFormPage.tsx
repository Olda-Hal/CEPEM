import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { apiClient } from '../utils/api';
import './AddPatientFormPage.css';

interface PatientFormData {
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  gender: string;
  weight: string;
  height: string;
  insuranceCompany: string;
  email: string;
  phone: string;
  address: string;
  postalCode: string;
  
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
  
  poorSleep: boolean;
  digestiveIssues: boolean;
  physicalStress: boolean;
  mentalStress: boolean;
  smoking: boolean;
  fatigue: boolean;
  lastMealHours: string;
  
  confirmAccuracy: boolean;
  termsAccepted: boolean;
  signaturePlace: string;
  signatureDate: string;
  signature: string;
  
  additionalHealthInfo: string;
  
  hadCovid: boolean;
  covidWhen: string;
  covidVaccine: boolean;
  vaccinesAfter2023: boolean;
  
  lastMenstruationDate: string;
  menstruationCycleDays: string;
  yearsSinceLastMenstruation: string;
  gaveBirth: boolean;
  birthCount: string;
  birthWhen: string;
  breastfed: boolean;
  breastfeedingMonths: string;
  breastfeedingInflammation: boolean;
  endedWithInflammation: boolean;
  contraception: boolean;
  contraceptionDuration: string;
  estrogen: boolean;
  estrogenType: string;
  interruption: boolean;
  interruptionCount: string;
  miscarriage: boolean;
  miscarriageCount: string;
  breastInjury: boolean;
  mammogram: boolean;
  mammogramCount: string;
  breastBiopsy: boolean;
  breastImplants: boolean;
  breastSurgery: boolean;
  breastSurgeryType: string;
  familyTumors: boolean;
  familyTumorType: string;
}

export const AddPatientFormPage: React.FC = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [photoFile, setPhotoFile] = useState<Blob | null>(null);
  const [photoPreview, setPhotoPreview] = useState<string | null>(null);
  const [showCamera, setShowCamera] = useState(false);
  const [cameraStream, setCameraStream] = useState<MediaStream | null>(null);
  const videoRef = React.useRef<HTMLVideoElement>(null);

  const [form, setForm] = useState<PatientFormData>({
    firstName: '',
    lastName: '',
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
    signature: '',
    
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

  const handleChange = (field: keyof PatientFormData, value: any) => {
    setForm(prev => ({ ...prev, [field]: value }));
  };

  const startCamera = async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ 
        video: { facingMode: 'user', width: 640, height: 480 } 
      });
      setCameraStream(stream);
      setShowCamera(true);
      
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
      }
    } catch (error) {
      console.error('Error accessing camera:', error);
      alert('Nepodařilo se spustit kameru. Zkontrolujte oprávnění.');
    }
  };

  const stopCamera = () => {
    if (cameraStream) {
      cameraStream.getTracks().forEach(track => track.stop());
      setCameraStream(null);
    }
    setShowCamera(false);
  };

  const capturePhoto = () => {
    if (!videoRef.current) return;

    const canvas = document.createElement('canvas');
    canvas.width = videoRef.current.videoWidth;
    canvas.height = videoRef.current.videoHeight;
    const ctx = canvas.getContext('2d');
    
    if (ctx) {
      ctx.drawImage(videoRef.current, 0, 0);
      
      canvas.toBlob((blob) => {
        if (blob) {
          setPhotoFile(blob);
          setPhotoPreview(URL.createObjectURL(blob));
          stopCamera();
        }
      }, 'image/jpeg', 0.9);
    }
  };

  React.useEffect(() => {
    return () => {
      if (cameraStream) {
        cameraStream.getTracks().forEach(track => track.stop());
      }
      if (photoPreview) {
        URL.revokeObjectURL(photoPreview);
      }
    };
  }, [cameraStream, photoPreview]);

  const buildComment = (): string => {
    const parts: string[] = [];
    
    const meds: string[] = [];
    if (form.medBloodPressure) meds.push('Tlak');
    if (form.medHeart) meds.push('Srdce');
    if (form.medCholesterol) meds.push('Cholesterol');
    if (form.medBloodThinners) meds.push('Srážlivost');
    if (form.medDiabetes) meds.push('Cukrovka');
    if (form.medThyroid) meds.push('Štítná žláza');
    if (form.medNerves) meds.push('Nervy');
    if (form.medPsych) meds.push('Psychika');
    if (form.medDigestion) meds.push('Zažívání');
    if (form.medPain) meds.push('Bolest');
    if (form.medDehydration) meds.push('Odvodnění');
    if (form.medBreathing) meds.push('Dýchání');
    if (form.medAntibiotics) meds.push('Antibiotika');
    if (form.medSupplements) meds.push('Doplňky stravy');
    if (form.medAllergies) meds.push('Alergie');
    if (meds.length > 0) parts.push(`Léky: ${meds.join(', ')}`);
    
    const issues: string[] = [];
    if (form.poorSleep) issues.push('Špatný spánek');
    if (form.digestiveIssues) issues.push('Zažívání');
    if (form.physicalStress) issues.push('Fyzická zátěž');
    if (form.mentalStress) issues.push('Psychická zátěž');
    if (form.smoking) issues.push('Kouření');
    if (form.fatigue) issues.push('Pocity únavy');
    if (form.lastMealHours) issues.push(`Poslední jídlo před ${form.lastMealHours} hod`);
    if (issues.length > 0) parts.push(`Zdravotní stav: ${issues.join(', ')}`);
    
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
    
    if (form.additionalHealthInfo) parts.push(`\n${form.additionalHealthInfo}`);
    
    return parts.join('\n');
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    
    if (!form.firstName || !form.lastName || !form.dateOfBirth) {
      setError('Vyplňte jméno, příjmení a datum narození');
      return;
    }
    
    if (!form.confirmAccuracy) {
      setError('Potvrďte správnost vyplněných údajů');
      return;
    }
    
    if (!form.termsAccepted) {
      setError('Musíte souhlasit se smluvními podmínkami');
      return;
    }

    setLoading(true);
    try {
      const payload: any = {
        firstName: form.firstName,
        lastName: form.lastName,
        birthDate: form.dateOfBirth,
        gender: form.gender,
        phoneNumber: form.phone,
        email: form.email,
        insuranceNumber: form.insuranceCompany,
        weight: form.weight ? parseFloat(form.weight) : undefined,
        height: form.height ? parseFloat(form.height) : undefined,
        comment: buildComment(),
      };

      const response = await apiClient.post<any>('/api/patients', payload);
      const newId = response?.id;
      
      if (photoFile && newId) {
        const formData = new FormData();
        formData.append('photo', photoFile, 'patient-photo.jpg');
        
        try {
          const token = localStorage.getItem('authToken');
          await fetch(`http://localhost:5000/api/patients/${newId}/photo`, {
            method: 'POST',
            headers: {
              ...(token && { Authorization: `Bearer ${token}` }),
            },
            body: formData
          });
        } catch (photoError) {
          console.error('Error uploading photo:', photoError);
        }
      }
      
      if (newId) {
        navigate(`/patients/${newId}`);
      } else {
        setError('Nepodařilo se vytvořit pacienta');
      }
    } catch (err: any) {
      console.error(err);
      setError(err?.response?.data?.message || 'Chyba při vytváření pacienta');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="add-patient-page">
      <div className="form-container">
        <h1>Vstupní formulář klienta</h1>
        
        {error && <div className="error-message">{error}</div>}
        
        <form onSubmit={handleSubmit} className="patient-form">
          
          <section className="form-section photo-section">
            <h2>Fotografie pacienta</h2>
            <div className="photo-container">
              {photoPreview ? (
                <div className="photo-preview">
                  <img src={photoPreview} alt="Náhled fotky" />
                  <button 
                    type="button"
                    className="remove-photo-btn"
                    onClick={() => {
                      setPhotoFile(null);
                      setPhotoPreview(null);
                    }}
                  >
                    ×
                  </button>
                </div>
              ) : (
                <div className="photo-placeholder">
                  <svg viewBox="0 0 24 24" width="60" height="60">
                    <path fill="currentColor" d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/>
                  </svg>
                </div>
              )}
              <button 
                type="button"
                className="camera-btn"
                onClick={startCamera}
              >
                📷 Vyfotit pacienta
              </button>
            </div>
          </section>

          <section className="form-section">
            <h2>Základní údaje</h2>
            <div className="form-row">
              <div className="form-field">
                <label>Příjmení *</label>
                <input type="text" value={form.lastName} onChange={e => handleChange('lastName', e.target.value)} required />
              </div>
              <div className="form-field">
                <label>Jméno *</label>
                <input type="text" value={form.firstName} onChange={e => handleChange('firstName', e.target.value)} required />
              </div>
              <div className="form-field">
                <label>Datum narození *</label>
                <input type="date" value={form.dateOfBirth} onChange={e => handleChange('dateOfBirth', e.target.value)} required />
              </div>
            </div>
            
            <div className="form-row">
              <div className="form-field">
                <label>Pohlaví *</label>
                <select value={form.gender} onChange={e => handleChange('gender', e.target.value)} required>
                  <option value="">Vyberte pohlaví</option>
                  <option value="M">Muž</option>
                  <option value="F">Žena</option>
                </select>
              </div>
              <div className="form-field">
                <label>Váha (kg)</label>
                <input type="number" step="0.1" value={form.weight} onChange={e => handleChange('weight', e.target.value)} />
              </div>
              <div className="form-field">
                <label>Výška (cm)</label>
                <input type="number" value={form.height} onChange={e => handleChange('height', e.target.value)} />
              </div>
            </div>
            
            <div className="form-row">
              <div className="form-field">
                <label>Pojišťovna</label>
                <input type="text" value={form.insuranceCompany} onChange={e => handleChange('insuranceCompany', e.target.value)} />
              </div>
              <div className="form-field">
                <label>Email</label>
                <input type="email" value={form.email} onChange={e => handleChange('email', e.target.value)} />
              </div>
              <div className="form-field">
                <label>Telefon</label>
                <input type="tel" value={form.phone} onChange={e => handleChange('phone', e.target.value)} />
              </div>
            </div>
            
            <div className="form-row">
              <div className="form-field">
                <label>Adresa + PSČ</label>
                <input type="text" value={form.address} onChange={e => handleChange('address', e.target.value)} placeholder="Ulice, město, PSČ" />
              </div>
            </div>
          </section>

          <section className="form-section">
            <h2>Užívám léky na:</h2>
            <div className="checkbox-grid">
              <label className="checkbox-label"><input type="checkbox" checked={form.medBloodPressure} onChange={e => handleChange('medBloodPressure', e.target.checked)} />Tlak</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.medHeart} onChange={e => handleChange('medHeart', e.target.checked)} />Srdce</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.medCholesterol} onChange={e => handleChange('medCholesterol', e.target.checked)} />Cholesterol</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.medBloodThinners} onChange={e => handleChange('medBloodThinners', e.target.checked)} />Srážlivost</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.medDiabetes} onChange={e => handleChange('medDiabetes', e.target.checked)} />Cukrovka</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.medThyroid} onChange={e => handleChange('medThyroid', e.target.checked)} />Štítná žláza</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.medNerves} onChange={e => handleChange('medNerves', e.target.checked)} />Nervy</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.medPsych} onChange={e => handleChange('medPsych', e.target.checked)} />Psychika</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.medDigestion} onChange={e => handleChange('medDigestion', e.target.checked)} />Zažívání</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.medPain} onChange={e => handleChange('medPain', e.target.checked)} />Bolest</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.medDehydration} onChange={e => handleChange('medDehydration', e.target.checked)} />Odvodnění</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.medBreathing} onChange={e => handleChange('medBreathing', e.target.checked)} />Dýchání</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.medAntibiotics} onChange={e => handleChange('medAntibiotics', e.target.checked)} />Antibiotika</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.medSupplements} onChange={e => handleChange('medSupplements', e.target.checked)} />Doplňky stravy</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.medAllergies} onChange={e => handleChange('medAllergies', e.target.checked)} />Alergie</label>
            </div>
          </section>

          <section className="form-section">
            <h2>Doplňující informace</h2>
            <div className="checkbox-grid">
              <label className="checkbox-label"><input type="checkbox" checked={form.poorSleep} onChange={e => handleChange('poorSleep', e.target.checked)} />Špatný spánek</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.digestiveIssues} onChange={e => handleChange('digestiveIssues', e.target.checked)} />Zažívání</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.physicalStress} onChange={e => handleChange('physicalStress', e.target.checked)} />Fyzická zátěž</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.mentalStress} onChange={e => handleChange('mentalStress', e.target.checked)} />Psychická zátěž</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.smoking} onChange={e => handleChange('smoking', e.target.checked)} />Kouření</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.fatigue} onChange={e => handleChange('fatigue', e.target.checked)} />Pocity únavy</label>
            </div>
            <div className="form-row">
              <div className="form-field">
                <label>Poslední jídlo před (hod.)</label>
                <input type="number" value={form.lastMealHours} onChange={e => handleChange('lastMealHours', e.target.value)} />
              </div>
            </div>
          </section>

          <section className="form-section">
            <h2>Další informace o stavu organizmu</h2>
            <textarea rows={6} value={form.additionalHealthInfo} onChange={e => handleChange('additionalHealthInfo', e.target.value)} placeholder="Další informace o stavu organizmu, které půjdou do komentáře k pacientovi..." />
          </section>

          <section className="form-section">
            <h2>COVID</h2>
            <div className="form-row">
              <label className="checkbox-label"><input type="checkbox" checked={form.hadCovid} onChange={e => handleChange('hadCovid', e.target.checked)} />Prodělal/a jsem COVID</label>
            </div>
            {form.hadCovid && (
              <div className="form-row">
                <div className="form-field">
                  <label>Kdy?</label>
                  <input type="text" value={form.covidWhen} onChange={e => handleChange('covidWhen', e.target.value)} placeholder="např. 2021, lehký průběh" />
                </div>
              </div>
            )}
            <div className="form-row">
              <label className="checkbox-label"><input type="checkbox" checked={form.covidVaccine} onChange={e => handleChange('covidVaccine', e.target.checked)} />Vakcinace proti COVID-u</label>
            </div>
            <div className="form-row">
              <label className="checkbox-label"><input type="checkbox" checked={form.vaccinesAfter2023} onChange={e => handleChange('vaccinesAfter2023', e.target.checked)} />Vakcinace jiné po r.2023</label>
            </div>
          </section>

          <section className="form-section optional-section">
            <h2>Doplnění jen k mamární diagnostice MEIK (nepovinné)</h2>
            <p className="section-note">Tuto sekci vyplňte pouze pokud je relevantní</p>
            
            <div className="form-row">
              <div className="form-field">
                <label>Kdy poslední menzes</label>
                <input type="text" value={form.lastMenstruationDate} onChange={e => handleChange('lastMenstruationDate', e.target.value)} />
              </div>
              <div className="form-field">
                <label>Po kolika dnech se opakuje</label>
                <input type="number" value={form.menstruationCycleDays} onChange={e => handleChange('menstruationCycleDays', e.target.value)} />
              </div>
              <div className="form-field">
                <label>Počet let od poslední menzes</label>
                <input type="number" value={form.yearsSinceLastMenstruation} onChange={e => handleChange('yearsSinceLastMenstruation', e.target.value)} />
              </div>
            </div>

            <div className="form-row">
              <label className="checkbox-label"><input type="checkbox" checked={form.gaveBirth} onChange={e => handleChange('gaveBirth', e.target.checked)} />Rodila</label>
              {form.gaveBirth && (
                <>
                  <div className="form-field">
                    <label>Kolikrát</label>
                    <input type="number" value={form.birthCount} onChange={e => handleChange('birthCount', e.target.value)} />
                  </div>
                  <div className="form-field">
                    <label>Kdy</label>
                    <input type="text" value={form.birthWhen} onChange={e => handleChange('birthWhen', e.target.value)} placeholder="např. 2015, 2018" />
                  </div>
                </>
              )}
            </div>

            <div className="form-row">
              <label className="checkbox-label"><input type="checkbox" checked={form.breastfed} onChange={e => handleChange('breastfed', e.target.checked)} />Kojila</label>
              {form.breastfed && (
                <div className="form-field">
                  <label>Počet měsíců</label>
                  <input type="number" value={form.breastfeedingMonths} onChange={e => handleChange('breastfeedingMonths', e.target.value)} />
                </div>
              )}
            </div>

            <div className="form-row">
              <label className="checkbox-label"><input type="checkbox" checked={form.breastfeedingInflammation} onChange={e => handleChange('breastfeedingInflammation', e.target.checked)} />Záněty při kojení?</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.endedWithInflammation} onChange={e => handleChange('endedWithInflammation', e.target.checked)} />Končilo kojení zánětem?</label>
            </div>

            <div className="form-row">
              <label className="checkbox-label"><input type="checkbox" checked={form.contraception} onChange={e => handleChange('contraception', e.target.checked)} />Antikoncepce</label>
              {form.contraception && (
                <div className="form-field">
                  <label>Jak dlouho?</label>
                  <input type="text" value={form.contraceptionDuration} onChange={e => handleChange('contraceptionDuration', e.target.value)} />
                </div>
              )}
            </div>

            <div className="form-row">
              <label className="checkbox-label"><input type="checkbox" checked={form.estrogen} onChange={e => handleChange('estrogen', e.target.checked)} />Estrogen</label>
              {form.estrogen && (
                <div className="form-field">
                  <label>Druh?</label>
                  <input type="text" value={form.estrogenType} onChange={e => handleChange('estrogenType', e.target.value)} />
                </div>
              )}
            </div>

            <div className="form-row">
              <label className="checkbox-label"><input type="checkbox" checked={form.interruption} onChange={e => handleChange('interruption', e.target.checked)} />Interrupce</label>
              {form.interruption && (
                <div className="form-field">
                  <label>Počet?</label>
                  <input type="number" value={form.interruptionCount} onChange={e => handleChange('interruptionCount', e.target.value)} />
                </div>
              )}
              <label className="checkbox-label"><input type="checkbox" checked={form.miscarriage} onChange={e => handleChange('miscarriage', e.target.checked)} />Potrat</label>
              {form.miscarriage && (
                <div className="form-field">
                  <label>Počet?</label>
                  <input type="number" value={form.miscarriageCount} onChange={e => handleChange('miscarriageCount', e.target.value)} />
                </div>
              )}
            </div>

            <div className="form-row">
              <label className="checkbox-label"><input type="checkbox" checked={form.breastInjury} onChange={e => handleChange('breastInjury', e.target.checked)} />Úraz prsu?</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.mammogram} onChange={e => handleChange('mammogram', e.target.checked)} />RTG Mamograf?</label>
              {form.mammogram && (
                <div className="form-field">
                  <label>Počet?</label>
                  <input type="number" value={form.mammogramCount} onChange={e => handleChange('mammogramCount', e.target.value)} />
                </div>
              )}
            </div>

            <div className="form-row">
              <label className="checkbox-label"><input type="checkbox" checked={form.breastBiopsy} onChange={e => handleChange('breastBiopsy', e.target.checked)} />Biopsie prsu?</label>
              <label className="checkbox-label"><input type="checkbox" checked={form.breastImplants} onChange={e => handleChange('breastImplants', e.target.checked)} />Impl?</label>
            </div>

            <div className="form-row">
              <label className="checkbox-label"><input type="checkbox" checked={form.breastSurgery} onChange={e => handleChange('breastSurgery', e.target.checked)} />Operace prsu?</label>
              {form.breastSurgery && (
                <div className="form-field">
                  <label>Jaká?</label>
                  <input type="text" value={form.breastSurgeryType} onChange={e => handleChange('breastSurgeryType', e.target.value)} />
                </div>
              )}
            </div>

            <div className="form-row">
              <label className="checkbox-label"><input type="checkbox" checked={form.familyTumors} onChange={e => handleChange('familyTumors', e.target.checked)} />Nádory v rodině?</label>
              {form.familyTumors && (
                <div className="form-field">
                  <label>Druh?</label>
                  <input type="text" value={form.familyTumorType} onChange={e => handleChange('familyTumorType', e.target.value)} />
                </div>
              )}
            </div>
          </section>

          <section className="form-section terms-section">
            <h2>Potvrzení správnosti vyplněných údajů</h2>
            <div className="form-row">
              <label className="checkbox-label">
                <input type="checkbox" checked={form.confirmAccuracy} onChange={e => handleChange('confirmAccuracy', e.target.checked)} required />
                Potvrzuji správnost vyplnění výše uvedených údajů *
              </label>
            </div>
            
            <h2>Smluvní podmínky</h2>
            <div className="terms-text">
              <ol>
                <li>Klient souhlasí se zpracováním svých osobních údajů Centrem Preventivní Medicíny (dále CPM).</li>
                <li>Klient, při pobytu v CPM, respektuje organizační pokyny personálu CPM.</li>
                <li>CPM zachází s osobními údaji klientů podle platných zákonů EU a ČR, které to upravují.</li>
                <li>CPM uchovává dokumenty, výsledky různých vyšetření v CPM nebo které Klient CPM předal.</li>
                <li>CPM používá výsledky vyšetření Klienta jen k dalšímu odbornému posouzení, eventuálně k další výzkumné činnosti nebo ke statistice v navazujících databázích. Ve všech těchto případech respektuje anonymitu klientů (není možno spojit osobní údaje s výsledkem vyšetření).</li>
                <li>CPM může informovat Klienta o změně v organizaci poskytování svých služeb tím způsobem, který si při objednání služeb zvolil Klient sám.</li>
                <li>Klienta CPM vyškrtne z aktuální evidence jen na základě jeho písemné žádosti nebo při úmrtí.</li>
              </ol>
            </div>
            <div className="form-row">
              <label className="checkbox-label">
                <input type="checkbox" checked={form.termsAccepted} onChange={e => handleChange('termsAccepted', e.target.checked)} required />
                Se smluvními podmínkami CPM souhlasím *
              </label>
            </div>

            <div className="form-row">
              <div className="form-field">
                <label>Kde</label>
                <input type="text" value={form.signaturePlace} onChange={e => handleChange('signaturePlace', e.target.value)} />
              </div>
              <div className="form-field">
                <label>Datum</label>
                <input type="date" value={form.signatureDate} onChange={e => handleChange('signatureDate', e.target.value)} />
              </div>
              <div className="form-field">
                <label>Podpis klienta</label>
                <input type="text" value={form.signature} onChange={e => handleChange('signature', e.target.value)} placeholder="Jméno a příjmení" />
              </div>
            </div>
          </section>

          <div className="form-actions">
            <button type="button" onClick={() => navigate('/patients')} className="btn-cancel">
              Zrušit
            </button>
            <button type="submit" disabled={loading} className="btn-submit">
              {loading ? 'Vytvářím...' : 'Vytvořit pacienta'}
            </button>
          </div>
        </form>
      </div>

      {/* Camera Modal */}
      {showCamera && (
        <div className="camera-modal">
          <div className="camera-content">
            <div className="camera-header">
              <h3>Vyfotit pacienta</h3>
              <button className="close-btn" onClick={stopCamera}>×</button>
            </div>
            <video 
              ref={videoRef} 
              autoPlay 
              playsInline
              className="camera-video"
            />
            <div className="camera-actions">
              <button onClick={stopCamera} className="btn-cancel">
                Zrušit
              </button>
              <button onClick={capturePhoto} className="btn-capture">
                📷 Vyfotit
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
