import React, { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { MdAssignment, MdOutlineDescription, MdPerson } from 'react-icons/md';
import { apiClient } from '../utils/api';
import { Patient, PatientDetail, PatientSearchResponse, VisitDetail, VisitListItem } from '../types';
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
  const [visits, setVisits] = useState<VisitListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [noteDraft, setNoteDraft] = useState('');
  const [savingNote, setSavingNote] = useState(false);
  const [showUpload, setShowUpload] = useState(false);
  const [showIntakeModal, setShowIntakeModal] = useState(false);

  const [patientSearch, setPatientSearch] = useState('');
  const [searchResults, setSearchResults] = useState<Patient[]>([]);
  const [showSearchResults, setShowSearchResults] = useState(false);

  const patientId = Number(id);
  const visitIdNum = Number(visitId);

  const load = useCallback(async () => {
    try {
      const [patientData, visitData, visitsData] = await Promise.all([
        apiClient.get<PatientDetail>(`/api/patients/${patientId}`),
        apiClient.get<VisitDetail>(`/api/patients/${patientId}/visits/${visitIdNum}`),
        apiClient.get<VisitListItem[]>(`/api/patients/${patientId}/visits`)
      ]);
      setPatient(patientData);
      setVisit(visitData);
      setVisits(visitsData);
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

  // Patient search (debounced) — lets the user jump to visits of other patients
  useEffect(() => {
    if (patientSearch.trim().length < 2) {
      setSearchResults([]);
      return undefined;
    }
    const timeoutId = setTimeout(async () => {
      try {
        const params = new URLSearchParams({
          page: '0',
          limit: '8',
          search: patientSearch.trim(),
          sortBy: 'lastName',
          sortOrder: 'asc'
        });
        const data = await apiClient.get<PatientSearchResponse>(
          `/api/patients/search?${params}`
        );
        setSearchResults(data.patients);
        setShowSearchResults(true);
      } catch (err) {
        console.error('Patient search failed', err);
      }
    }, 300);
    return () => clearTimeout(timeoutId);
  }, [patientSearch]);

  const handleSelectPatient = async (selectedPatientId: number) => {
    setShowSearchResults(false);
    setPatientSearch('');
    try {
      const list = await apiClient.get<VisitListItem[]>(`/api/patients/${selectedPatientId}/visits`);
      const latest = list[0];
      if (latest) {
        navigate(`/patients/${selectedPatientId}/visits/${latest.id}`);
      } else {
        navigate(`/patients/${selectedPatientId}`);
      }
    } catch (err) {
      console.error(err);
      navigate(`/patients/${selectedPatientId}`);
    }
  };

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
          <div className="visit-patient-search">
            <input
              type="text"
              className="visit-patient-search-input"
              placeholder={t('visits.patientSearchPlaceholder')}
              value={patientSearch}
              onChange={(e) => setPatientSearch(e.target.value)}
              onFocus={() => searchResults.length > 0 && setShowSearchResults(true)}
              onBlur={() => setTimeout(() => setShowSearchResults(false), 200)}
            />
            {showSearchResults && searchResults.length > 0 && (
              <ul className="visit-patient-search-results">
                {searchResults.map((p) => (
                  <li key={p.id}>
                    <button type="button" onClick={() => handleSelectPatient(p.id)}>
                      <span className="visit-patient-search-name">
                        {p.lastName}, {p.firstName}
                      </span>
                      <span className="visit-patient-search-meta">
                        {new Date(p.birthDate).toLocaleDateString(i18n.language)} · {p.uid}
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
          <div className="visit-patient-card">
            <div className="visit-patient-photo">
              {photoObjectUrl ? (
                <img src={photoObjectUrl} alt={fullName} className="visit-patient-photo-img" />
              ) : (
                <div className="visit-patient-photo-placeholder" aria-hidden>
                  <MdPerson size={72} />
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

          {visits.length > 0 && (
            <div className="visit-sidebar-card">
              <h3 className="visit-sidebar-card-title">{t('visits.otherVisits')}</h3>
              <div className="visit-sidebar-visits">
                {visits.map((v) => (
                  <button
                    key={v.id}
                    type="button"
                    className={`visit-sidebar-item${v.id === visitIdNum ? ' active' : ''}`}
                    onClick={() => navigate(`/patients/${patientId}/visits/${v.id}`)}
                  >
                    <span className="visit-sidebar-item-date">
                      {new Date(v.visitDate).toLocaleDateString(i18n.language)}
                    </span>
                    <span className="visit-sidebar-item-meta">
                      {t('visits.documentsCount', { count: v.documentCount })}
                      {v.hasIntakeForm && (
                        <span className="visit-sidebar-item-badge">
                          <MdAssignment size={15} />
                        </span>
                      )}
                    </span>
                  </button>
                ))}
              </div>
            </div>
          )}
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
                      <MdOutlineDescription size={15} />
                      {doc.originalFileName}
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
