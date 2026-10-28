import React, { useMemo, useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { PatientDetail, QuickPreviewSettings } from '../types';
import { parseIntakeSummary, formSubmissionToIntakeSummary } from '../utils/intakeSummary';
import { apiClient } from '../utils/api';
import { AppHeader } from '../components/AppHeader';
import QuickPreviewSettingsModal from '../components/QuickPreviewSettingsModal';
import AddEventModal from '../components/AddEventModal';
import { AddIntakeFormEventModal } from '../components/AddIntakeFormEventModal';
import { DocumentUpload } from '../components/DocumentUpload';
import './PatientDetailPage.css';

import { useToast } from '../contexts/ToastContext';
import PatientEventsSection from '../components/patient/PatientEventsSection';
import PatientDocumentsSection from '../components/patient/PatientDocumentsSection';
import CameraCaptureModal from '../components/patient/CameraCaptureModal';
import { formatDateTime } from '../utils/format';

export const PatientDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t, i18n } = useTranslation();
  const toast = useToast();
  const [patient, setPatient] = useState<PatientDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [photoObjectUrl, setPhotoObjectUrl] = useState<string | null>(null);
  const [showSettingsModal, setShowSettingsModal] = useState(false);
  const [showAddEventModal, setShowAddEventModal] = useState(false);
  const [showAddIntakeFormModal, setShowAddIntakeFormModal] = useState(false);
  const [showDocumentUpload, setShowDocumentUpload] = useState(false);
  const [uploadingDocument, setUploadingDocument] = useState(false);
  const [editingComment, setEditingComment] = useState(false);
  const [commentText, setCommentText] = useState('');
  const [showCamera, setShowCamera] = useState(false);
  const [expandedEvents, setExpandedEvents] = useState<Set<number>>(new Set());

  const intakeSummary = useMemo(() => {
    if (!patient) {
      return parseIntakeSummary(null);
    }

    if (patient.formSubmission) {
      return formSubmissionToIntakeSummary(patient.formSubmission);
    }

    const intakeEvent = patient.events.find((event: PatientDetail['events'][number]) => {
      const eventName = event.eventTypeName.toLowerCase();
      return (
        eventName.includes('formul') ||
        (event.comment?.includes('Váha:') && event.comment?.includes('Výška:'))
      );
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

  // Fetch the patient photo through the authenticated API client - the
  // endpoint no longer allows anonymous access, so it cannot be loaded
  // directly via an <img src> URL.
  useEffect(() => {
    let objectUrl: string | null = null;
    let cancelled = false;

    const loadPhoto = async () => {
      if (!patient?.photoUrl) return;
      try {
        const blob = await apiClient.getBlob(patient.photoUrl);
        if (!cancelled) {
          objectUrl = URL.createObjectURL(blob);
          setPhotoObjectUrl(objectUrl);
        }
      } catch (error) {
        console.error('Error loading patient photo:', error);
      }
    };

    loadPhoto();
    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [patient?.photoUrl]);

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

  const handleGenerateIntakeLink = async () => {
    if (!patient) {
      return;
    }

    try {
      const response = await apiClient.post<{ intakePath: string }>(
        '/api/events/intake-form-links',
        {
          personId: patient.personId
        }
      );

      const absoluteLink = `${window.location.origin}${response.intakePath}`;

      try {
        await navigator.clipboard.writeText(absoluteLink);
        toast.info(t('patients.intakeLinkCopied', { link: absoluteLink }));
      } catch {
        toast.info(t('patients.intakeLinkGenerated', { link: absoluteLink }));
      }
    } catch (err: any) {
      toast.info(err.message || t('patients.intakeLinkGenerationFailed'));
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
      toast.error(t('patients.documents.errors.uploadFailed'));
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
      toast.error(t('patients.documents.errors.viewFailed'));
    }
  };

  const handleExaminationDocumentView = async (examinationId: number, documentId: number) => {
    try {
      const blob = await apiClient.getBlob(
        `/api/examinations/${examinationId}/documents/${documentId}`
      );
      const url = window.URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (error) {
      console.error('Error viewing examination document:', error);
      toast.error(t('patients.documents.errors.viewFailed'));
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
      toast.error(t('patients.documents.errors.deleteFailed'));
    }
  };

  const handleSaveComment = async () => {
    if (!patient) return;

    try {
      await apiClient.patch(`/api/patients/${patient.id}/comment`, {
        comment: commentText
      });

      setPatient({ ...patient, comment: commentText });
      setEditingComment(false);
    } catch (error) {
      console.error('Error updating comment:', error);
      toast.info('Nepodařilo se uložit komentář');
    }
  };

  const handleCapturePhoto = async (blob: Blob) => {
    if (!patient) return;

    const formData = new FormData();
    formData.append('photo', blob, 'patient-photo.jpg');

    try {
      await apiClient.postFormData(`/api/patients/${patient.id}/photo`, formData);
      setShowCamera(false);
      if (id) {
        loadPatientDetail(parseInt(id));
      }
    } catch (error) {
      console.error('Error uploading photo:', error);
      toast.error('Nepodařilo se nahrát fotografii');
    }
  };

  const handleCancelEditComment = () => {
    setCommentText(patient?.comment || '');
    setEditingComment(false);
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString(i18n.language, {
      year: 'numeric',
      month: 'long',
      day: 'numeric'
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
                {photoObjectUrl ? (
                  <img
                    src={photoObjectUrl}
                    alt={patient.fullName}
                    className="patient-photo"
                    onError={(e) => {
                      e.currentTarget.style.display = 'none';
                      const placeholder = e.currentTarget.nextElementSibling as HTMLElement;
                      if (placeholder) placeholder.style.display = 'block';
                    }}
                  />
                ) : null}
                <svg
                  className="photo-icon"
                  viewBox="0 0 24 24"
                  style={{ display: photoObjectUrl ? 'none' : 'block' }}
                >
                  <path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z" />
                </svg>
              </div>
              <button
                className="photo-button"
                onClick={() => setShowCamera(true)}
                title="Vyfotit pacienta"
              >
                <svg viewBox="0 0 24 24" width="20" height="20">
                  <path
                    fill="currentColor"
                    d="M12 15.5c-1.93 0-3.5-1.57-3.5-3.5s1.57-3.5 3.5-3.5 3.5 1.57 3.5 3.5-1.57 3.5-3.5 3.5zM9 2L7.17 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2h-3.17L15 2H9z"
                  />
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
                  <span className="value">
                    {formatDate(patient.birthDate)} ({patient.age} {t('patients.yearsOld')})
                  </span>
                </div>
                <div className="detail-row">
                  <span className="label">{t('patients.fields.gender')}:</span>
                  <span className="value">
                    {t(`patients.genders.${patient.gender.toLowerCase()}`)}
                  </span>
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
                <p className="intake-card-description">
                  {t('patients.intakeFormSummaryDescription')}
                </p>
              </div>
            </div>

            <div className="intake-overview-grid">
              <div className="intake-overview-item">
                <span className="intake-label">{t('patients.weight')}</span>
                <span className="intake-value">
                  {intakeSummary.weight || t('common.notProvided')}
                </span>
              </div>
              <div className="intake-overview-item">
                <span className="intake-label">{t('patients.height')}</span>
                <span className="intake-value">
                  {intakeSummary.height || t('common.notProvided')}
                </span>
              </div>
            </div>

            <div className="intake-sections-grid">
              <div className="intake-section">
                <h3>{t('patients.medications')}</h3>
                {intakeSummary.medications.length > 0 ? (
                  <ul>
                    {intakeSummary.medications.map((item) => (
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
                    {intakeSummary.healthStatus.map((item) => (
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
                    {intakeSummary.covid.map((item) => (
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
                    {intakeSummary.gynecology.map((item) => (
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
                  <path
                    fill="currentColor"
                    d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z"
                  />
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
                <path d="M19.14,12.94c0.04-0.3,0.06-0.61,0.06-0.94c0-0.32-0.02-0.64-0.07-0.94l2.03-1.58c0.18-0.14,0.23-0.41,0.12-0.61 l-1.92-3.32c-0.12-0.22-0.37-0.29-0.59-0.22l-2.39,0.96c-0.5-0.38-1.03-0.7-1.62-0.94L14.4,2.81c-0.04-0.24-0.24-0.41-0.48-0.41 h-3.84c-0.24,0-0.43,0.17-0.47,0.41L9.25,5.35C8.66,5.59,8.12,5.92,7.63,6.29L5.24,5.33c-0.22-0.08-0.47,0-0.59,0.22L2.74,8.87 C2.62,9.08,2.66,9.34,2.86,9.48l2.03,1.58C4.84,11.36,4.82,11.69,4.82,12s0.02,0.64,0.07,0.94l-2.03,1.58 c-0.18,0.14-0.23,0.41-0.12,0.61l1.92,3.32c0.12,0.22,0.37,0.29,0.59,0.22l2.39-0.96c0.5,0.38,1.03,0.7,1.62,0.94l0.36,2.54 c0.05,0.24,0.24,0.41,0.48,0.41h3.84c0.24,0,0.44-0.17,0.47-0.41l0.36-2.54c0.59-0.24,1.13-0.56,1.62-0.94l2.39,0.96 c0.22,0.08,0.47,0,0.59-0.22l1.92-3.32c0.12-0.22,0.07-0.47-0.12-0.61L19.14,12.94z M12,15.6c-1.98,0-3.6-1.62-3.6-3.6 s1.62-3.6,3.6-3.6s3.6,1.62,3.6,3.6S13.98,15.6,12,15.6z" />
              </svg>
            </button>
          </div>
          <div className="quick-preview-grid">
            {patient.quickPreviewSettings.showCovidVaccination && (
              <div className="preview-item">
                <span className="preview-label">{t('patients.covidVaccination')}:</span>
                <span
                  className={`preview-value ${patient.quickPreview.hasCovidVaccination ? 'positive' : 'negative'}`}
                >
                  {patient.quickPreview.hasCovidVaccination ? t('common.yes') : t('common.no')}
                </span>
              </div>
            )}
            {patient.quickPreviewSettings.showFluVaccination && (
              <div className="preview-item">
                <span className="preview-label">{t('patients.fluVaccination')}:</span>
                <span
                  className={`preview-value ${patient.quickPreview.hasFluVaccination ? 'positive' : 'negative'}`}
                >
                  {patient.quickPreview.hasFluVaccination ? t('common.yes') : t('common.no')}
                </span>
              </div>
            )}
            {patient.quickPreviewSettings.showDiabetes && (
              <div className="preview-item">
                <span className="preview-label">{t('patients.diabetes')}:</span>
                <span
                  className={`preview-value ${patient.quickPreview.hasDiabetes ? 'warning' : 'negative'}`}
                >
                  {patient.quickPreview.hasDiabetes ? t('common.yes') : t('common.no')}
                </span>
              </div>
            )}
            {patient.quickPreviewSettings.showHypertension && (
              <div className="preview-item">
                <span className="preview-label">{t('patients.hypertension')}:</span>
                <span
                  className={`preview-value ${patient.quickPreview.hasHypertension ? 'warning' : 'negative'}`}
                >
                  {patient.quickPreview.hasHypertension ? t('common.yes') : t('common.no')}
                </span>
              </div>
            )}
            {patient.quickPreviewSettings.showHeartDisease && (
              <div className="preview-item">
                <span className="preview-label">{t('patients.heartDisease')}:</span>
                <span
                  className={`preview-value ${patient.quickPreview.hasHeartDisease ? 'warning' : 'negative'}`}
                >
                  {patient.quickPreview.hasHeartDisease ? t('common.yes') : t('common.no')}
                </span>
              </div>
            )}
            {patient.quickPreviewSettings.showAllergies && (
              <div className="preview-item">
                <span className="preview-label">{t('patients.allergies')}:</span>
                <span
                  className={`preview-value ${patient.quickPreview.hasAllergies ? 'warning' : 'negative'}`}
                >
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
                <span className="preview-value info">
                  {patient.quickPreview.upcomingAppointmentsCount}
                </span>
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
        <PatientEventsSection
          events={patient.events}
          expandedEvents={expandedEvents}
          onToggleEvent={toggleEvent}
          onAddEvent={() => setShowAddEventModal(true)}
          onAddIntakeForm={() => setShowAddIntakeFormModal(true)}
          onGenerateIntakeLink={handleGenerateIntakeLink}
          onExaminationDocumentView={handleExaminationDocumentView}
        />

        {/* Patient Appointments */}
        <div className="patient-appointments-card">
          <div className="card-header">
            <h2>
              {t('patients.appointments')} ({patient.appointments.length})
            </h2>
            <button className="add-appointment-btn">{t('patients.addAppointment')}</button>
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
                    <div className="start-time">
                      {formatDateTime(appointment.startTime, i18n.language)}
                    </div>
                    <div className="end-time">
                      {formatDateTime(appointment.endTime, i18n.language)}
                    </div>
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
        <PatientDocumentsSection
          documents={patient.documents}
          uploading={uploadingDocument}
          onAdd={() => setShowDocumentUpload(true)}
          onDocumentView={handleDocumentView}
          onDocumentDelete={handleDocumentDelete}
        />
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
        <CameraCaptureModal onClose={() => setShowCamera(false)} onCapture={handleCapturePhoto} />
      )}
    </div>
  );
};
