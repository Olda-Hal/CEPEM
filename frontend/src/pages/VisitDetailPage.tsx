import React, { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { apiClient } from '../utils/api';
import { PatientDetail, VisitDetail } from '../types';
import { DocumentUpload } from '../components/DocumentUpload';
import { AddIntakeFormEventModal } from '../components/AddIntakeFormEventModal';
import { BaseModal } from '../components/BaseModal';
import { useToast } from '../contexts/ToastContext';
import './VisitDetailPage.css';

/**
 * Visit detail: sidebar with the patient card, main area with the visit note,
 * attached documents (upload/download/delete) and the optional intake form
 * (fill it on this page → it is linked to the visit and shown as a preview).
 */
const VisitDetailPage: React.FC = () => {
  const { id, visitId } = useParams<{ id: string; visitId: string }>();
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const toast = useToast();

  const [patient, setPatient] = useState<PatientDetail | null>(null);
  const [photoObjectUrl, setPhotoObjectUrl] = useState<string | null>(null);
  const [visit, setVisit] = useState<VisitDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [noteDraft, setNoteDraft] = useState('');
  const [savingNote, setSavingNote] = useState(false);
  const [showUpload, setShowUpload] = useState(false);
  const [showIntakeModal, setShowIntakeModal] = useState(false);

  const patientId = Number(id);
  const visitIdNum = Number(visitId);

  const load = useCallback(async () => {
    try {
      const [patientData, visitData] = await Promise.all([
        apiClient.get<PatientDetail>(`/api/patients/${patientId}`),
        apiClient.get<VisitDetail>(`/api/patients/${patientId}/visits/${visitIdNum}`)
      ]);
      setPatient(patientData);
      setVisit(visitData);
      setNoteDraft(visitData.note ?? '');
      setError(null);
    } catch (err) {
      console.error(err);
      setError(t('visits.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [patientId, visitIdNum, t]);

  useEffect(() => {
    void load();
  }, [load]);

  // Photo through the authenticated client (endpoint is not anonymous)
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
      } catch (err) {
        console.error('Error loading patient photo:', err);
      }
    };

    loadPhoto();
    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [patient?.photoUrl]);

  const handleSaveNote = async () => {
    setSavingNote(true);
    try {
      const updated = await apiClient.patch<VisitDetail>(
        `/api/patients/${patientId}/visits/${visitIdNum}`,
        { note: noteDraft }
      );
      setVisit(updated);
      toast.success(t('visits.noteSaved'));
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : t('visits.noteSaveFailed'));
    } finally {
      setSavingNote(false);
    }
  };

  const handleUpload = async (file: File) => {
    try {
      const formData = new FormData();
      formData.append('file', file, file.name);
      await apiClient.postFormData(
        `/api/patients/${patientId}/visits/${visitIdNum}/documents`,
        formData
      );
      setShowUpload(false);
      await load();
      toast.success(t('patients.documents.uploadSuccess'));
    } catch (err) {
      toast.error(
        err instanceof Error && err.message
          ? err.message
          : t('patients.documents.errors.uploadFailed')
      );
    }
  };

  const handleDocumentView = async (documentId: number) => {
    try {
      const blob = await apiClient.getBlob(`/api/patients/${patientId}/documents/${documentId}`);
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (err) {
      toast.error(
        err instanceof Error && err.message
          ? err.message
          : t('patients.documents.errors.viewFailed')
      );
    }
  };

  const handleIntakeCreated = async (eventId?: number) => {
    if (eventId) {
      try {
        await apiClient.patch<VisitDetail>(`/api/patients/${patientId}/visits/${visitIdNum}`, {
          sourceEventId: eventId
        });
      } catch (err) {
        console.error(err);
      }
    }
    await load();
  };

  if (loading) {
    return <div className="loading">{t('common.loading')}</div>;
  }

  if (error || !patient || !visit) {
    return (
      <div className="visit-detail-page">
        <button className="btn btn-secondary" onClick={() => navigate(`/patients/${patientId}`)}>
          ← {t('patients.backToPatients')}
        </button>
        <div className="alert alert-error">{error ?? t('visits.loadFailed')}</div>
      </div>
    );
  }

  const fullName =
    `${patient.titleBefore ?? ''} ${patient.lastName}, ${patient.firstName} ${patient.titleAfter ?? ''}`
      .replace(/\s+/g, ' ')
      .trim();

  return (
    <div className="visit-detail-page">
      <div className="page-heading">
        <h1 className="page-title">
          {t('visits.detailTitle', {
            date: new Date(visit.visitDate).toLocaleDateString(i18n.language)
          })}
        </h1>
        <div className="page-heading-actions">
          <button className="btn btn-secondary" onClick={() => navigate(`/patients/${patientId}`)}>
            ← {t('patients.backToPatient')}
          </button>
        </div>
      </div>

      <div className="visit-layout">
        <aside className="visit-sidebar">
          <div className="visit-patient-card">
            <div className="visit-patient-photo">
              {photoObjectUrl ? (
                <img src={photoObjectUrl} alt={fullName} className="visit-patient-photo-img" />
              ) : (
                <div className="visit-patient-photo-placeholder" aria-hidden>
                  👤
                </div>
              )}
            </div>
            <h2 className="visit-patient-name">{fullName}</h2>
            <dl className="visit-patient-meta">
              <dt>{t('patients.uid')}</dt>
              <dd>{patient.uid}</dd>
              <dt>{t('patients.birthDate')}</dt>
              <dd>{new Date(patient.birthDate).toLocaleDateString(i18n.language)}</dd>
              <dt>{t('patients.insuranceCompany')}</dt>
              <dd>{patient.insuranceNumber}</dd>
            </dl>
          </div>
        </aside>

        <main className="visit-main">
          <section className="visit-card">
            <h3>{t('visits.visitNote')}</h3>
            <textarea
              value={noteDraft}
              onChange={(e) => setNoteDraft(e.target.value)}
              rows={4}
              placeholder={t('visits.visitNotePlaceholder')}
            />
            <div className="visit-card-actions">
              <button
                className="btn btn-secondary"
                onClick={handleSaveNote}
                disabled={savingNote || noteDraft === (visit.note ?? '')}
              >
                {savingNote ? t('common.loading') : t('visits.saveNote')}
              </button>
            </div>
          </section>

          <section className="visit-card">
            <div className="visit-card-header">
              <h3>
                {t('patients.documents.title')} ({visit.documents.length})
              </h3>
              <button className="btn btn-primary btn-sm" onClick={() => setShowUpload(true)}>
                + {t('patients.documents.upload')}
              </button>
            </div>
            {visit.documents.length === 0 ? (
              <p className="visit-empty">{t('patients.documents.empty')}</p>
            ) : (
              <ul className="visit-document-list">
                {visit.documents.map((doc) => (
                  <li key={doc.documentId}>
                    <button
                      className="visit-doc-name"
                      onClick={() => handleDocumentView(doc.documentId)}
                    >
                      📄 {doc.originalFileName}
                    </button>
                    <span className="visit-doc-meta">
                      {new Date(doc.uploadedAt).toLocaleDateString(i18n.language)} ·{' '}
                      {Math.ceil(doc.fileSize / 1024)} KB
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="visit-card">
            <div className="visit-card-header">
              <h3>{t('visits.intakeTitle')}</h3>
              {!visit.intakeForm && (
                <button className="btn btn-primary btn-sm" onClick={() => setShowIntakeModal(true)}>
                  + {t('visits.addIntake')}
                </button>
              )}
            </div>
            {visit.intakeForm ? (
              <pre className="visit-intake-preview">{visit.intakeForm}</pre>
            ) : (
              <p className="visit-empty">{t('visits.intakeEmpty')}</p>
            )}
          </section>
        </main>
      </div>

      {showUpload && (
        <BaseModal title={t('patients.documents.upload')} onClose={() => setShowUpload(false)}>
          <DocumentUpload onUpload={handleUpload} onCancel={() => setShowUpload(false)} />
        </BaseModal>
      )}

      {showIntakeModal && (
        <AddIntakeFormEventModal
          isOpen
          patientId={patientId}
          patientFirstName={patient.firstName}
          patientLastName={patient.lastName}
          onClose={() => setShowIntakeModal(false)}
          onEventCreated={handleIntakeCreated}
        />
      )}
    </div>
  );
};

export default VisitDetailPage;
