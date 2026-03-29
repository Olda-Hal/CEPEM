import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { apiClient } from '../utils/api';
import { PatientFormFields, PatientFormData } from '../components/PatientFormFields';
import './AddPatientFormPage.css';

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
        phoneNumber: form.phone || undefined,
        email: form.email || undefined,
        insuranceNumber: form.insuranceCompany ? parseInt(form.insuranceCompany) : undefined,
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
          await apiClient.postFormData(`/api/patients/${newId}/photo`, formData);
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

          <PatientFormFields 
            form={form}
            onChange={handleChange}
            disabled={loading}
            showPhoto={false}
          />

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
