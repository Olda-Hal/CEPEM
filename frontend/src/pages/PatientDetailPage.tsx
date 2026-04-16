import React, { useMemo, useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { PatientDetail, QuickPreviewSettings } from '../types';
import { apiClient } from '../utils/api';
import { AppHeader } from '../components/AppHeader';
import QuickPreviewSettingsModal from '../components/QuickPreviewSettingsModal';
import AddEventModal from '../components/AddEventModal';
import { AddIntakeFormEventModal } from '../components/AddIntakeFormEventModal';
import { DocumentUpload } from '../components/DocumentUpload';
import './PatientDetailPage.css';

interface IntakeSummary {
  weight: string | null;
  height: string | null;
  medications: string[];
  healthStatus: string[];
  covid: string[];
  gynecology: string[];
  notes: string | null;
  rawLines: string[];
}

const parseIntakeSummary = (comment?: string | null): IntakeSummary => {
  const summary: IntakeSummary = {
    weight: null,
    height: null,
    medications: [],
    healthStatus: [],
    covid: [],
    gynecology: [],
    notes: null,
    rawLines: []
  };

  if (!comment) {
    return summary;
  }

  const lines = comment.split('\n').map(line => line.trim()).filter(Boolean);
  summary.rawLines = lines;

  lines.forEach(line => {
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
      return;
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
    notes: null,
    rawLines: []
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

export const PatientDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t, i18n } = useTranslation();
  const [patient, setPatient] = useState<PatientDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showSettingsModal, setShowSettingsModal] = useState(false);
  const [showAddEventModal, setShowAddEventModal] = useState(false);
  const [showAddIntakeFormModal, setShowAddIntakeFormModal] = useState(false);
  const [showDocumentUpload, setShowDocumentUpload] = useState(false);
  const [uploadingDocument, setUploadingDocument] = useState(false);
  const [editingComment, setEditingComment] = useState(false);
  const [commentText, setCommentText] = useState('');
  const [showCamera, setShowCamera] = useState(false);
  const [cameraStream, setCameraStream] = useState<MediaStream | null>(null);
  const [expandedEvents, setExpandedEvents] = useState<Set<number>>(new Set());
  const videoRef = React.useRef<HTMLVideoElement>(null);

  const intakeSummary = useMemo(() => {
    if (!patient) {
      return parseIntakeSummary(null);
    }

    if (patient.formSubmission) {
      return formSubmissionToIntakeSummary(patient.formSubmission);
    }

    const intakeEvent = patient.events.find((event: PatientDetail['events'][number]) => {
      const eventName = event.eventTypeName.toLowerCase();
      return eventName.includes('formul') || (event.comment?.includes('Váha:') && event.comment?.includes('Výška:'));
    });

    return parseIntakeSummary(intakeEvent?.comment ?? null);
  }, [patient]);

  const toggleEvent = (eventId: number) => {
    setExpandedEvents((prev: Set<number>) => {
      const next = new Set(prev);
      if (next.has(eventId)) {
        next.delete(eventId);
      } else {
        next.add(eventId);
      }
      return next;
    });
  };

  useEffect(() => {
    if (id) {
      loadPatientDetail(parseInt(id));
    }
  }, [id]);

  const loadPatientDetail = async (patientId: number) => {
    try {
      setLoading(true);
      const response = await apiClient.get<PatientDetail>(`/api/patients/${patientId}/detail`);
      setPatient(response);
      setCommentText(response.comment || '');
      setError(null);
    } catch (error) {
      console.error('Error loading patient detail:', error);
      setError(t('errors.loadingPatientDetail'));
    } finally {
      setLoading(false);
    }
  };

  const handleBackToPatients = () => {
    navigate('/patients');
  };

  const handleSaveSettings = async (settings: QuickPreviewSettings) => {
    try {
      await apiClient.put('/api/settings/quick-preview', settings);
      if (patient) {
        setPatient({
          ...patient,
          quickPreviewSettings: settings
        });
      }
    } catch (error) {
      console.error('Error saving quick preview settings:', error);
    }
  };

  const handleEventAdded = () => {
    if (id) {
      loadPatientDetail(parseInt(id));
    }
  };

  const handleDocumentUpload = async (file: File) => {
    try {
      setUploadingDocument(true);
      
      const formData = new FormData();
      formData.append('document', file);

      await apiClient.postFormData(`/api/patients/${id}/documents`, formData);

      setShowDocumentUpload(false);
      
      if (id) {
        loadPatientDetail(parseInt(id));
      }
    } catch (error) {
      console.error('Error uploading document:', error);
      alert(t('patients.documents.errors.uploadFailed'));
    } finally {
      setUploadingDocument(false);
    }
  };

  const handleDocumentView = async (documentId: number) => {
    try {
      const blob = await apiClient.getBlob(`/api/patients/${id}/documents/${documentId}`);
      const url = window.URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (error) {
      console.error('Error viewing document:', error);
      alert(t('patients.documents.errors.viewFailed'));
    }
  };

  const handleExaminationDocumentView = async (examinationId: number, documentId: number) => {
    try {
      const blob = await apiClient.getBlob(`/api/examinations/${examinationId}/documents/${documentId}`);
      const url = window.URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (error) {
      console.error('Error viewing examination document:', error);
      alert(t('patients.documents.errors.viewFailed'));
    }
  };

  const handleDocumentDelete = async (documentId: number) => {
    if (!window.confirm(t('patients.documents.confirmDelete'))) {
      return;
    }

    try {
      await apiClient.delete(`/api/patients/${id}/documents/${documentId}`);
      
      if (id) {
        loadPatientDetail(parseInt(id));
      }
    } catch (error) {
      console.error('Error deleting document:', error);
      alert(t('patients.documents.errors.deleteFailed'));
    }
  };

  const handleSaveComment = async () => {
    if (!patient) return;
    
    try {
      await apiClient.patch(`/api/patients/${patient.id}/comment`, {
        comment: commentText,
      });
      
      setPatient({ ...patient, comment: commentText });
      setEditingComment(false);
    } catch (error) {
      console.error('Error updating comment:', error);
      alert('Nepodařilo se uložit komentář');
    }
  };

  const handleCancelEditComment = () => {
    setCommentText(patient?.comment || '');
    setEditingComment(false);
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
      cameraStream.getTracks().forEach((track: MediaStreamTrack) => track.stop());
      setCameraStream(null);
    }
    setShowCamera(false);
  };

  const capturePhoto = async () => {
    if (!videoRef.current || !patient) return;

    const canvas = document.createElement('canvas');
    canvas.width = videoRef.current.videoWidth;
    canvas.height = videoRef.current.videoHeight;
    const ctx = canvas.getContext('2d');
    
    if (ctx) {
      ctx.drawImage(videoRef.current, 0, 0);
      
      canvas.toBlob(async (blob) => {
        if (!blob) return;
        
        const formData = new FormData();
        formData.append('photo', blob, 'patient-photo.jpg');
        
        try {
          await apiClient.postFormData(`/api/patients/${patient.id}/photo`, formData);
          
          stopCamera();
          
          if (id) {
            loadPatientDetail(parseInt(id));
          }
        } catch (error) {
          console.error('Error uploading photo:', error);
          alert('Nepodařilo se nahrát fotografii');
        }
      }, 'image/jpeg', 0.9);
    }
  };

  React.useEffect(() => {
    return () => {
      if (cameraStream) {
        cameraStream.getTracks().forEach((track: MediaStreamTrack) => track.stop());
      }
    };
  }, [cameraStream]);

  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(2) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString(i18n.language, {
      year: 'numeric',
      month: 'long',
      day: 'numeric'
    });
  };

  const formatDateTime = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString(i18n.language, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  if (loading) {
    return (
      <div className="patient-detail-page">
        <AppHeader sectionTitle={t('patients.patientDetail')} />
        <div className="loading-container">
          <div className="loading-spinner"></div>
          <p>{t('common.loading')}</p>
        </div>
      </div>
    );
  }

  if (error || !patient) {
    return (
      <div className="patient-detail-page">
        <AppHeader sectionTitle={t('patients.patientDetail')} />
        <div className="error-container">
          <p className="error-message">{error || t('errors.patientNotFound')}</p>
          <button onClick={handleBackToPatients} className="back-button">
            {t('common.back')}
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="patient-detail-page">
      <AppHeader sectionTitle={t('patients.patientDetail')}>
        <button onClick={() => navigate(`/patients/${patient.id}/edit`)} className="back-button">
          {t('patients.editPatient')}
        </button>
        <button onClick={handleBackToPatients} className="back-button">
          {t('patients.backToPatients')}
        </button>
      </AppHeader>

      <div className="patient-detail-content">
        {/* Patient Basic Info */}
        <div className="patient-info-card">
          <div className="patient-header">
            <div className="patient-photo-container">
              <div className="patient-photo-placeholder">
                {patient.photoUrl ? (
                  <img 
                    src={`${process.env.REACT_APP_API_URL || '/api'}${patient.photoUrl}`} 
                    alt={patient.fullName}
                    className="patient-photo"
                    onError={(e) => {
                      e.currentTarget.style.display = 'none';
                      const placeholder = e.currentTarget.nextElementSibling as HTMLElement;
                      if (placeholder) placeholder.style.display = 'block';
                    }}
                  />
                ) : null}
                <svg className="photo-icon" viewBox="0 0 24 24" style={{ display: patient.photoUrl ? 'none' : 'block' }}>
                  <path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/>
                </svg>
              </div>
              <button 
                className="photo-button"
                onClick={startCamera}
                title="Vyfotit pacienta"
              >
                <svg viewBox="0 0 24 24" width="20" height="20">
                  <path fill="currentColor" d="M12 15.5c-1.93 0-3.5-1.57-3.5-3.5s1.57-3.5 3.5-3.5 3.5 1.57 3.5 3.5-1.57 3.5-3.5 3.5zM9 2L7.17 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2h-3.17L15 2H9z"/>
                </svg>
              </button>
            </div>
            <div className="patient-basic-info">
              <h1 className="patient-name">
                {patient.titleBefore && <span className="title">{patient.titleBefore} </span>}
                {patient.fullName}
                {patient.titleAfter && <span className="title"> {patient.titleAfter}</span>}
              </h1>
              <div className="patient-details">
                <div className="detail-row">
                  <span className="label">{t('patients.fields.uid')}:</span>
                  <span className="value">{patient.uid}</span>
                </div>
                <div className="detail-row">
                  <span className="label">{t('patients.fields.birthDate')}:</span>
                  <span className="value">{formatDate(patient.birthDate)} ({patient.age} {t('patients.yearsOld')})</span>
                </div>
                <div className="detail-row">
                  <span className="label">{t('patients.fields.gender')}:</span>
                  <span className="value">{t(`patients.genders.${patient.gender.toLowerCase()}`)}</span>
                </div>
                <div className="detail-row">
                  <span className="label">{t('patients.fields.insuranceNumber')}:</span>
                  <span className="value">{patient.insuranceNumber}</span>
                </div>
                <div className="detail-row">
                  <span className="label">{t('patients.fields.email')}:</span>
                  <span className="value">{patient.email || '-'}</span>
                </div>
                <div className="detail-row">
                  <span className="label">{t('patients.fields.phoneNumber')}:</span>
                  <span className="value">{patient.phoneNumber || '-'}</span>
                </div>
                <div className="detail-row">
                  <span className="label">{t('patients.status')}:</span>
                  <span className={`value status ${patient.alive ? 'alive' : 'deceased'}`}>
                    {patient.alive ? t('patients.alive') : t('patients.deceased')}
                  </span>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Intake Form Summary */}
        {intakeSummary.rawLines.length > 0 && (
          <div className="patient-intake-card">
            <div className="card-header">
              <div>
                <h2>{t('patients.intakeForm')}</h2>
                <p className="intake-card-description">{t('patients.intakeFormSummaryDescription')}</p>
              </div>
            </div>

            <div className="intake-overview-grid">
              <div className="intake-overview-item">
                <span className="intake-label">{t('patients.weight')}</span>
                <span className="intake-value">{intakeSummary.weight || t('common.notProvided')}</span>
              </div>
              <div className="intake-overview-item">
                <span className="intake-label">{t('patients.height')}</span>
                <span className="intake-value">{intakeSummary.height || t('common.notProvided')}</span>
              </div>
            </div>

            <div className="intake-sections-grid">
              <div className="intake-section">
                <h3>{t('patients.medications')}</h3>
                {intakeSummary.medications.length > 0 ? (
                  <ul>
                    {intakeSummary.medications.map(item => (
                      <li key={item}>{item}</li>
                    ))}
                  </ul>
                ) : (
                  <p>{t('common.notProvided')}</p>
                )}
              </div>

              <div className="intake-section">
                <h3>{t('patients.healthStatus')}</h3>
                {intakeSummary.healthStatus.length > 0 ? (
                  <ul>
                    {intakeSummary.healthStatus.map(item => (
                      <li key={item}>{item}</li>
                    ))}
                  </ul>
                ) : (
                  <p>{t('common.notProvided')}</p>
                )}
              </div>

              <div className="intake-section">
                <h3>{t('patients.covidAndVaccination')}</h3>
                {intakeSummary.covid.length > 0 ? (
                  <ul>
                    {intakeSummary.covid.map(item => (
                      <li key={item}>{item}</li>
                    ))}
                  </ul>
                ) : (
                  <p>{t('common.notProvided')}</p>
                )}
              </div>

              <div className="intake-section full-width">
                <h3>{t('patients.gynecology')}</h3>
                {intakeSummary.gynecology.length > 0 ? (
                  <ul>
                    {intakeSummary.gynecology.map(item => (
                      <li key={item}>{item}</li>
                    ))}
                  </ul>
                ) : (
                  <p>{t('common.notProvided')}</p>
                )}
              </div>

              {intakeSummary.notes && (
                <div className="intake-section full-width">
                  <h3>{t('patients.notes')}</h3>
                  <p>{intakeSummary.notes}</p>
                </div>
              )}
            </div>
          </div>
        )}

        {/* Patient Comment */}
        <div className="patient-comment-section">
          <div className="comment-header">
            <h3>Komentář o pacientovi</h3>
            {!editingComment && (
              <button 
                className="edit-comment-btn"
                onClick={() => setEditingComment(true)}
                title="Upravit komentář"
              >
                <svg viewBox="0 0 24 24" width="18" height="18">
                  <path fill="currentColor" d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z"/>
                </svg>
              </button>
            )}
          </div>
          {editingComment ? (
            <div className="comment-edit">
              <textarea
                value={commentText}
                onChange={(e) => setCommentText(e.target.value)}
                rows={10}
                className="comment-textarea"
                placeholder="Zadejte komentář o pacientovi..."
              />
              <div className="comment-actions">
                <button onClick={handleCancelEditComment} className="btn-cancel">
                  Zrušit
                </button>
                <button onClick={handleSaveComment} className="btn-save">
                  Uložit
                </button>
              </div>
            </div>
          ) : (
            <div className="comment-display">
              {commentText ? (
                <p className="comment-text">{commentText}</p>
              ) : (
                <p className="comment-empty">Žádný komentář</p>
              )}
            </div>
          )}
        </div>

        {/* Quick Preview */}
        <div className="patient-quick-preview">
          <div className="quick-preview-header">
            <h3>{t('patients.quickPreview')}</h3>
            <button 
              className="settings-button"
              onClick={() => setShowSettingsModal(true)}
              title={t('patients.quickPreviewSettings')}
            >
              <svg viewBox="0 0 24 24" width="20" height="20">
                <path d="M19.14,12.94c0.04-0.3,0.06-0.61,0.06-0.94c0-0.32-0.02-0.64-0.07-0.94l2.03-1.58c0.18-0.14,0.23-0.41,0.12-0.61 l-1.92-3.32c-0.12-0.22-0.37-0.29-0.59-0.22l-2.39,0.96c-0.5-0.38-1.03-0.7-1.62-0.94L14.4,2.81c-0.04-0.24-0.24-0.41-0.48-0.41 h-3.84c-0.24,0-0.43,0.17-0.47,0.41L9.25,5.35C8.66,5.59,8.12,5.92,7.63,6.29L5.24,5.33c-0.22-0.08-0.47,0-0.59,0.22L2.74,8.87 C2.62,9.08,2.66,9.34,2.86,9.48l2.03,1.58C4.84,11.36,4.82,11.69,4.82,12s0.02,0.64,0.07,0.94l-2.03,1.58 c-0.18,0.14-0.23,0.41-0.12,0.61l1.92,3.32c0.12,0.22,0.37,0.29,0.59,0.22l2.39-0.96c0.5,0.38,1.03,0.7,1.62,0.94l0.36,2.54 c0.05,0.24,0.24,0.41,0.48,0.41h3.84c0.24,0,0.44-0.17,0.47-0.41l0.36-2.54c0.59-0.24,1.13-0.56,1.62-0.94l2.39,0.96 c0.22,0.08,0.47,0,0.59-0.22l1.92-3.32c0.12-0.22,0.07-0.47-0.12-0.61L19.14,12.94z M12,15.6c-1.98,0-3.6-1.62-3.6-3.6 s1.62-3.6,3.6-3.6s3.6,1.62,3.6,3.6S13.98,15.6,12,15.6z"/>
              </svg>
            </button>
          </div>
          <div className="quick-preview-grid">
            {patient.quickPreviewSettings.showCovidVaccination && (
              <div className="preview-item">
                <span className="preview-label">{t('patients.covidVaccination')}:</span>
                <span className={`preview-value ${patient.quickPreview.hasCovidVaccination ? 'positive' : 'negative'}`}>
                  {patient.quickPreview.hasCovidVaccination ? t('common.yes') : t('common.no')}
                </span>
              </div>
            )}
            {patient.quickPreviewSettings.showFluVaccination && (
              <div className="preview-item">
                <span className="preview-label">{t('patients.fluVaccination')}:</span>
                <span className={`preview-value ${patient.quickPreview.hasFluVaccination ? 'positive' : 'negative'}`}>
                  {patient.quickPreview.hasFluVaccination ? t('common.yes') : t('common.no')}
                </span>
              </div>
            )}
            {patient.quickPreviewSettings.showDiabetes && (
              <div className="preview-item">
                <span className="preview-label">{t('patients.diabetes')}:</span>
                <span className={`preview-value ${patient.quickPreview.hasDiabetes ? 'warning' : 'negative'}`}>
                  {patient.quickPreview.hasDiabetes ? t('common.yes') : t('common.no')}
                </span>
              </div>
            )}
            {patient.quickPreviewSettings.showHypertension && (
              <div className="preview-item">
                <span className="preview-label">{t('patients.hypertension')}:</span>
                <span className={`preview-value ${patient.quickPreview.hasHypertension ? 'warning' : 'negative'}`}>
                  {patient.quickPreview.hasHypertension ? t('common.yes') : t('common.no')}
                </span>
              </div>
            )}
            {patient.quickPreviewSettings.showHeartDisease && (
              <div className="preview-item">
                <span className="preview-label">{t('patients.heartDisease')}:</span>
                <span className={`preview-value ${patient.quickPreview.hasHeartDisease ? 'warning' : 'negative'}`}>
                  {patient.quickPreview.hasHeartDisease ? t('common.yes') : t('common.no')}
                </span>
              </div>
            )}
            {patient.quickPreviewSettings.showAllergies && (
              <div className="preview-item">
                <span className="preview-label">{t('patients.allergies')}:</span>
                <span className={`preview-value ${patient.quickPreview.hasAllergies ? 'warning' : 'negative'}`}>
                  {patient.quickPreview.hasAllergies ? t('common.yes') : t('common.no')}
                </span>
              </div>
            )}
            {patient.quickPreviewSettings.showRecentEvents && (
              <div className="preview-item">
                <span className="preview-label">{t('patients.recentEvents')}:</span>
                <span className="preview-value info">{patient.quickPreview.recentEventsCount}</span>
              </div>
            )}
            {patient.quickPreviewSettings.showUpcomingAppointments && (
              <div className="preview-item">
                <span className="preview-label">{t('patients.upcomingAppointments')}:</span>
                <span className="preview-value info">{patient.quickPreview.upcomingAppointmentsCount}</span>
              </div>
            )}
            {patient.quickPreviewSettings.showLastVisit && patient.quickPreview.lastVisit && (
              <div className="preview-item full-width">
                <span className="preview-label">{t('patients.lastVisit')}:</span>
                <span className="preview-value info">
                  {formatDate(patient.quickPreview.lastVisit)}
                  {patient.quickPreview.lastVisitType && ` (${patient.quickPreview.lastVisitType})`}
                </span>
              </div>
            )}
          </div>
        </div>

        {/* Patient Events */}
        <div className="patient-events-card">
          <div className="card-header">
            <h2>{t('patients.events')} ({patient.events.length})</h2>
            <div className="button-group">
              <button className="add-event-btn" onClick={() => setShowAddIntakeFormModal(true)}>
                📋 Vstupní Formulář
              </button>
              <button className="add-event-btn" onClick={() => setShowAddEventModal(true)}>
                {t('patients.addEvent')}
              </button>
            </div>
          </div>
          
          {patient.events.length === 0 ? (
            <div className="no-data">
              <p>{t('patients.noEvents')}</p>
            </div>
          ) : (
            <div className="events-list">
              {patient.events.map((event) => {
                const eventDocuments = event.examinations.flatMap(exam =>
                  exam.documents.map(doc => ({
                    examId: exam.id,
                    examName: exam.name,
                    ...doc,
                  }))
                );

                return (
                <div key={event.id} className="event-item">
                  <button className="event-header event-toggle" onClick={() => toggleEvent(event.id)}>
                    <div className="event-title-wrap">
                      <svg className={`event-chevron ${expandedEvents.has(event.id) ? 'expanded' : ''}`} viewBox="0 0 24 24" width="16" height="16">
                        <path fill="currentColor" d="M10 6L8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z"/>
                      </svg>
                      <h3 className="event-type">{event.eventTypeName}</h3>
                    </div>
                    <span className="event-date">{formatDateTime(event.happenedAt)}</span>
                  </button>

                  {expandedEvents.has(event.id) && (
                    <div className="event-expanded-content">
                    {event.happenedTo && (
                      <div className="event-duration">
                        {t('patients.eventUntil')}: {formatDateTime(event.happenedTo)}
                      </div>
                    )}

                    {event.comment && (
                      <div className="event-comment">
                        <p>{event.comment}</p>
                      </div>
                    )}

                    <div className="event-details">
                    {event.drugUses.length > 0 && (
                      <div className="detail-section">
                        <h4>{t('patients.drugUses')}:</h4>
                        <ul>
                          {event.drugUses.map((drug, index) => (
                            <li key={index}>{drug}</li>
                          ))}
                        </ul>
                      </div>
                    )}
                    
                    {event.symptoms.length > 0 && (
                      <div className="detail-section">
                        <h4>{t('patients.symptoms')}:</h4>
                        <ul>
                          {event.symptoms.map((symptom, index) => (
                            <li key={index}>{symptom}</li>
                          ))}
                        </ul>
                      </div>
                    )}
                    
                    {event.injuries.length > 0 && (
                      <div className="detail-section">
                        <h4>{t('patients.injuries')}:</h4>
                        <ul>
                          {event.injuries.map((injury, index) => (
                            <li key={index}>{injury}</li>
                          ))}
                        </ul>
                      </div>
                    )}
                    
                    {event.vaccines.length > 0 && (
                      <div className="detail-section">
                        <h4>{t('patients.vaccines')}:</h4>
                        <ul>
                          {event.vaccines.map((vaccine, index) => (
                            <li key={index}>{vaccine}</li>
                          ))}
                        </ul>
                      </div>
                    )}
                    
                    {event.hasPregnancy && (
                      <div className="detail-section">
                        <h4>{t('patients.pregnancy')}</h4>
                      </div>
                    )}
                  </div>

                  {event.examinations.length > 0 && (
                    <div className="examinations-full-section">
                      <h4>{t('patients.examinations')}:</h4>
                      <ul className="examination-list">
                        {event.examinations.map((exam) => (
                          <li key={exam.id} className="examination-list-item">
                            <span className="examination-name">{exam.name}</span>
                            {exam.documents.length > 0 && (
                              <span className="examination-doc-badge">{exam.documents.length}</span>
                            )}
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}

                  <div className="event-documents-section">
                    <h4>{t('patients.documents.title')}:</h4>
                    {eventDocuments.length === 0 ? (
                      <p className="no-exam-documents">{t('patients.documents.noDocuments')}</p>
                    ) : (
                      <ul className="event-document-list">
                        {eventDocuments.map((doc) => (
                          <li key={`${doc.examId}-${doc.id}`} className="event-document-item">
                            <button
                              className="event-document-btn"
                              onClick={() => handleExaminationDocumentView(doc.examId, doc.id)}
                              title={doc.fileName}
                            >
                              <svg viewBox="0 0 24 24" width="18" height="18" className="event-doc-icon">
                                <path fill="currentColor" d="M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zM6 20V4h7v5h5v11H6z"/>
                              </svg>
                              <div className="event-doc-main">
                                <span className="event-doc-file">{doc.fileName}</span>
                                <span className="event-doc-exam">{doc.examName}</span>
                              </div>
                              <span className="event-doc-meta">
                                {formatDateTime(doc.uploadedAt)} · {formatFileSize(doc.fileSize)}
                              </span>
                            </button>
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>
                  </div>
                  )}
                </div>
              )})}
            </div>
          )}
        </div>

        {/* Patient Appointments */}
        <div className="patient-appointments-card">
          <div className="card-header">
            <h2>{t('patients.appointments')} ({patient.appointments.length})</h2>
            <button className="add-appointment-btn">
              {t('patients.addAppointment')}
            </button>
          </div>
          
          {patient.appointments.length === 0 ? (
            <div className="no-data">
              <p>{t('patients.noAppointments')}</p>
            </div>
          ) : (
            <div className="appointments-list">
              {patient.appointments.map((appointment) => (
                <div key={appointment.id} className="appointment-item">
                  <div className="appointment-time">
                    <div className="start-time">{formatDateTime(appointment.startTime)}</div>
                    <div className="end-time">{formatDateTime(appointment.endTime)}</div>
                  </div>
                  
                  <div className="appointment-details">
                    <div className="doctor">{appointment.doctorName}</div>
                    <div className="hospital">{appointment.hospitalName}</div>
                    {appointment.equipmentName && (
                      <div className="equipment">{appointment.equipmentName}</div>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Patient Documents */}
        <div className="patient-documents-card">
          <div className="card-header">
            <h2>{t('patients.documents.title')} ({patient.documents.length})</h2>
            <button 
              className="add-document-btn" 
              onClick={() => setShowDocumentUpload(true)}
              disabled={uploadingDocument}
            >
              {t('patients.documents.addDocument')}
            </button>
          </div>
          
          {patient.documents.length === 0 ? (
            <div className="no-data">
              <p>{t('patients.documents.noDocuments')}</p>
            </div>
          ) : (
            <div className="documents-list">
              {patient.documents.map((document) => (
                <div key={document.id} className="document-item">
                  <div 
                    className="document-info" 
                    onClick={() => handleDocumentView(document.id)}
                  >
                    <svg viewBox="0 0 24 24" width="24" height="24" className="document-icon">
                      <path d="M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zM6 20V4h7v5h5v11H6z"/>
                    </svg>
                    <div className="document-details">
                      <div className="document-name">{document.fileName}</div>
                      <div className="document-meta">
                        <span className="document-date">{formatDateTime(document.uploadedAt)}</span>
                        <span className="document-size">{formatFileSize(document.fileSize)}</span>
                      </div>
                    </div>
                  </div>
                  <button 
                    className="delete-document-btn"
                    onClick={() => handleDocumentDelete(document.id)}
                    title={t('patients.documents.deleteDocument')}
                  >
                    <svg viewBox="0 0 24 24" width="20" height="20">
                      <path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"/>
                    </svg>
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Quick Preview Settings Modal */}
      <QuickPreviewSettingsModal
        isOpen={showSettingsModal}
        onClose={() => setShowSettingsModal(false)}
        settings={patient.quickPreviewSettings}
        onSave={handleSaveSettings}
      />

      {/* Add Event Modal */}
      <AddEventModal
        isOpen={showAddEventModal}
        onClose={() => setShowAddEventModal(false)}
        patientId={patient.id}
        onEventAdded={handleEventAdded}
      />

      {/* Add Intake Form Event Modal */}
      <AddIntakeFormEventModal
        isOpen={showAddIntakeFormModal}
        patientId={patient.id}
        patientFirstName={patient.firstName}
        patientLastName={patient.lastName}
        onClose={() => setShowAddIntakeFormModal(false)}
        onEventCreated={handleEventAdded}
      />

      {/* Document Upload Modal */}
      {showDocumentUpload && (
        <DocumentUpload
          onUpload={handleDocumentUpload}
          onCancel={() => setShowDocumentUpload(false)}
        />
      )}

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
