import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { MdDeleteOutline, MdDownload, MdPrivacyTip, MdSearch } from 'react-icons/md';
import { apiClient, ApiError, getAuthToken } from '../utils/api';
import { useAuth } from '../contexts/AuthContext';
import { isAdmin } from '../utils/roles';
import { BaseModal } from '../components/BaseModal';
import { PatientSearchResponse } from '../types';
import './GdprPage.css';

interface GdprDataMap {
  patientId: number;
  patientName: string;
  uid: string;
  visits: number;
  documents: number;
  photos: number;
  events: number;
  formSubmissions: number;
  evaluationCases: number;
  consents: number;
  emailLogs: number;
  lastActivityAtUtc?: string | null;
}

interface PatientConsent {
  id: number;
  consentType: string;
  version: string;
  language: string;
  source: string;
  acceptedAtUtc: string;
  revokedAtUtc?: string | null;
  notes?: string | null;
}

interface ConsentTemplate {
  id: number;
  consentType: string;
  version: string;
  language: string;
  title: string;
  text: string;
  isActive: boolean;
  updatedAtUtc: string;
}

interface GdprRequest {
  id: number;
  requestType: string;
  status: string;
  requestedAtUtc: string;
  completedAtUtc?: string | null;
  details?: string | null;
  error?: string | null;
}

const emptyTemplate = (): Omit<ConsentTemplate, 'id' | 'updatedAtUtc'> => ({
  consentType: 'gdpr',
  version: '1',
  language: 'cs',
  title: '',
  text: '',
  isActive: true
});

const GdprPage: React.FC = () => {
  const { t, i18n } = useTranslation();
  const { user } = useAuth();
  const admin = isAdmin(user);

  const [searchQuery, setSearchQuery] = useState('');
  const [searching, setSearching] = useState(false);
  const [searchResults, setSearchResults] = useState<
    Array<{ id: number; firstName: string; lastName: string; uid: string }>
  >([]);
  const [selectedPatient, setSelectedPatient] = useState<{
    id: number;
    name: string;
    uid: string;
  } | null>(null);
  const [dataMap, setDataMap] = useState<GdprDataMap | null>(null);
  const [consents, setConsents] = useState<PatientConsent[]>([]);
  const [requests, setRequests] = useState<GdprRequest[]>([]);
  const [templates, setTemplates] = useState<ConsentTemplate[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const [showAnonymize, setShowAnonymize] = useState(false);
  const [anonymizeConfirmation, setAnonymizeConfirmation] = useState('');
  const [anonymizing, setAnonymizing] = useState(false);

  const [showTemplateModal, setShowTemplateModal] = useState(false);
  const [editingTemplate, setEditingTemplate] = useState<ConsentTemplate | null>(null);
  const [templateForm, setTemplateForm] = useState(emptyTemplate());
  const [savingTemplate, setSavingTemplate] = useState(false);

  const loadTemplates = useCallback(async () => {
    try {
      setTemplates(await apiClient.get<ConsentTemplate[]>('/api/gdpr/consent-templates'));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('gdpr.loadFailed'));
    }
  }, [t]);

  useEffect(() => {
    if (admin) {
      void loadTemplates();
    }
  }, [admin, loadTemplates]);

  const loadPatient = useCallback(
    async (patientId: number) => {
      setLoading(true);
      setError(null);
      try {
        const [map, consentRows, requestRows] = await Promise.all([
          apiClient.get<GdprDataMap>(`/api/gdpr/patients/${patientId}/data-map`),
          apiClient.get<PatientConsent[]>(`/api/gdpr/patients/${patientId}/consents`),
          apiClient.get<GdprRequest[]>(`/api/gdpr/requests?patientId=${patientId}`)
        ]);
        setDataMap(map);
        setConsents(consentRows);
        setRequests(requestRows);
        setSelectedPatient({ id: map.patientId, name: map.patientName, uid: map.uid });
      } catch (err) {
        setError(err instanceof ApiError ? err.message : t('gdpr.loadFailed'));
        setDataMap(null);
      } finally {
        setLoading(false);
      }
    },
    [t]
  );

  const handleSearch = async (event: React.FormEvent) => {
    event.preventDefault();
    if (searchQuery.trim().length < 2) {
      return;
    }
    setSearching(true);
    setError(null);
    try {
      const response = await apiClient.get<PatientSearchResponse>(
        `/api/patients/search?search=${encodeURIComponent(searchQuery.trim())}&limit=8`
      );
      setSearchResults(response.patients);
      if (response.patients.length === 0) {
        setError(t('gdpr.noPatients'));
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('gdpr.loadFailed'));
    } finally {
      setSearching(false);
    }
  };

  const handleSelectPatient = async (patientId: number) => {
    setSearchResults([]);
    await loadPatient(patientId);
  };

  const downloadExport = async (format: 'json' | 'zip') => {
    if (!selectedPatient) {
      return;
    }
    setError(null);
    try {
      const token = getAuthToken();
      const response = await fetch(
        `/api/gdpr/patients/${selectedPatient.id}/export${format === 'zip' ? '/zip' : ''}`,
        { headers: token ? { Authorization: `Bearer ${token}` } : {} }
      );
      if (!response.ok) {
        throw new Error(`${response.status}`);
      }
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download =
        response.headers.get('Content-Disposition')?.split('filename=')[1] ??
        `gdpr-patient-${selectedPatient.id}.${format}`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('gdpr.actionFailed'));
    }
  };

  const handleAnonymize = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!selectedPatient) {
      return;
    }
    setAnonymizing(true);
    setError(null);
    try {
      await apiClient.post(`/api/gdpr/patients/${selectedPatient.id}/anonymize`, {
        confirmation: anonymizeConfirmation
      });
      setShowAnonymize(false);
      setAnonymizeConfirmation('');
      setNotice(t('gdpr.anonymizeSuccess'));
      await loadPatient(selectedPatient.id);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('gdpr.actionFailed'));
    } finally {
      setAnonymizing(false);
    }
  };

  const openTemplateModal = (template: ConsentTemplate | null) => {
    setEditingTemplate(template);
    setTemplateForm(
      template
        ? {
            consentType: template.consentType,
            version: template.version,
            language: template.language,
            title: template.title,
            text: template.text,
            isActive: template.isActive
          }
        : emptyTemplate()
    );
    setShowTemplateModal(true);
  };

  const handleSaveTemplate = async (event: React.FormEvent) => {
    event.preventDefault();
    setSavingTemplate(true);
    setError(null);
    try {
      if (editingTemplate) {
        await apiClient.put(`/api/gdpr/consent-templates/${editingTemplate.id}`, templateForm);
      } else {
        await apiClient.post('/api/gdpr/consent-templates', templateForm);
      }
      setShowTemplateModal(false);
      await loadTemplates();
      setNotice(t('gdpr.templateSaved'));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('gdpr.actionFailed'));
    } finally {
      setSavingTemplate(false);
    }
  };

  const handleDeleteTemplate = async (template: ConsentTemplate) => {
    if (!window.confirm(t('gdpr.confirmDeleteTemplate', { title: template.title }))) {
      return;
    }
    try {
      await apiClient.delete(`/api/gdpr/consent-templates/${template.id}`);
      await loadTemplates();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('gdpr.actionFailed'));
    }
  };

  const formatDate = (value?: string | null) =>
    value ? new Date(value).toLocaleString(i18n.language) : '—';

  if (!admin) {
    return (
      <div className="gdpr-page">
        <h1 className="page-title">{t('gdpr.title')}</h1>
        <p>{t('adminPage.noAccess')}</p>
      </div>
    );
  }

  return (
    <div className="gdpr-page">
      <h1 className="page-title">{t('gdpr.title')}</h1>
      <p className="page-lead">{t('gdpr.lead')}</p>

      {error && <div className="alert alert-error">{error}</div>}
      {notice && <div className="alert alert-success">{notice}</div>}

      <section className="gdpr-card">
        <h2>
          <MdSearch /> {t('gdpr.patientSearch')}
        </h2>
        <form className="gdpr-search" onSubmit={handleSearch}>
          <input
            value={searchQuery}
            onChange={(event) => setSearchQuery(event.target.value)}
            placeholder={t('gdpr.searchPlaceholder')}
          />
          <button className="btn btn-primary" type="submit" disabled={searching}>
            {searching ? t('common.loading') : t('common.search')}
          </button>
        </form>
        {searchResults.length > 0 && (
          <ul className="gdpr-search-results">
            {searchResults.map((patient) => (
              <li key={patient.id}>
                <button type="button" onClick={() => void handleSelectPatient(patient.id)}>
                  <strong>
                    {patient.firstName} {patient.lastName}
                  </strong>
                  <span>UID {patient.uid}</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>

      {selectedPatient && (
        <>
          <section className="gdpr-card">
            <div className="gdpr-card-header">
              <h2>
                <MdPrivacyTip /> {selectedPatient.name}
              </h2>
              <span className="gdpr-uid">UID {selectedPatient.uid}</span>
            </div>

            {loading || !dataMap ? (
              <div>{t('common.loading')}</div>
            ) : (
              <>
                <div className="gdpr-map-grid">
                  {(
                    [
                      ['visits', dataMap.visits],
                      ['documents', dataMap.documents],
                      ['photos', dataMap.photos],
                      ['events', dataMap.events],
                      ['formSubmissions', dataMap.formSubmissions],
                      ['evaluationCases', dataMap.evaluationCases],
                      ['consents', dataMap.consents],
                      ['emailLogs', dataMap.emailLogs]
                    ] as const
                  ).map(([key, value]) => (
                    <div key={key} className="gdpr-map-stat">
                      <span>{t(`gdpr.counts.${key}`)}</span>
                      <strong>{value}</strong>
                    </div>
                  ))}
                </div>
                <p className="gdpr-muted">
                  {t('gdpr.lastActivity')}: {formatDate(dataMap.lastActivityAtUtc)}
                </p>

                <div className="gdpr-actions">
                  <button className="btn btn-secondary" onClick={() => void downloadExport('json')}>
                    <MdDownload /> {t('gdpr.exportJson')}
                  </button>
                  <button className="btn btn-secondary" onClick={() => void downloadExport('zip')}>
                    <MdDownload /> {t('gdpr.exportZip')}
                  </button>
                  <button className="btn btn-danger" onClick={() => setShowAnonymize(true)}>
                    {t('gdpr.anonymize')}
                  </button>
                </div>
              </>
            )}
          </section>

          <section className="gdpr-card">
            <h2>{t('gdpr.consents')}</h2>
            {consents.length === 0 ? (
              <p className="gdpr-muted">{t('gdpr.noConsents')}</p>
            ) : (
              <table className="gdpr-table">
                <thead>
                  <tr>
                    <th>{t('gdpr.type')}</th>
                    <th>{t('gdpr.version')}</th>
                    <th>{t('gdpr.language')}</th>
                    <th>{t('gdpr.source')}</th>
                    <th>{t('gdpr.acceptedAt')}</th>
                    <th>{t('gdpr.revokedAt')}</th>
                  </tr>
                </thead>
                <tbody>
                  {consents.map((consent) => (
                    <tr key={consent.id}>
                      <td>{consent.consentType}</td>
                      <td>{consent.version}</td>
                      <td>{consent.language}</td>
                      <td>{consent.source}</td>
                      <td>{formatDate(consent.acceptedAtUtc)}</td>
                      <td>{formatDate(consent.revokedAtUtc)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>

          <section className="gdpr-card">
            <h2>{t('gdpr.requests')}</h2>
            {requests.length === 0 ? (
              <p className="gdpr-muted">{t('gdpr.noRequests')}</p>
            ) : (
              <table className="gdpr-table">
                <thead>
                  <tr>
                    <th>{t('gdpr.type')}</th>
                    <th>{t('gdpr.status')}</th>
                    <th>{t('gdpr.requestedAt')}</th>
                    <th>{t('gdpr.details')}</th>
                  </tr>
                </thead>
                <tbody>
                  {requests.map((request) => (
                    <tr key={request.id}>
                      <td>{request.requestType}</td>
                      <td>{request.status}</td>
                      <td>{formatDate(request.requestedAtUtc)}</td>
                      <td>{request.details ?? request.error ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>
        </>
      )}

      <section className="gdpr-card">
        <div className="gdpr-card-header">
          <h2>{t('gdpr.templates')}</h2>
          <button className="btn btn-primary" onClick={() => openTemplateModal(null)}>
            + {t('gdpr.newTemplate')}
          </button>
        </div>
        {templates.length === 0 ? (
          <p className="gdpr-muted">{t('gdpr.noTemplates')}</p>
        ) : (
          <table className="gdpr-table">
            <thead>
              <tr>
                <th>{t('gdpr.type')}</th>
                <th>{t('gdpr.version')}</th>
                <th>{t('gdpr.language')}</th>
                <th>{t('gdpr.templateTitle')}</th>
                <th>{t('gdpr.active')}</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {templates.map((template) => (
                <tr key={template.id}>
                  <td>{template.consentType}</td>
                  <td>{template.version}</td>
                  <td>{template.language}</td>
                  <td>{template.title}</td>
                  <td>{template.isActive ? '✓' : '—'}</td>
                  <td className="gdpr-table-actions">
                    <button
                      className="btn btn-secondary btn-sm"
                      onClick={() => openTemplateModal(template)}
                    >
                      {t('gdpr.editTemplate')}
                    </button>
                    <button
                      className="btn btn-danger btn-sm"
                      onClick={() => void handleDeleteTemplate(template)}
                    >
                      <MdDeleteOutline />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      {showAnonymize && selectedPatient && (
        <BaseModal title={t('gdpr.anonymizeTitle')} onClose={() => setShowAnonymize(false)}>
          <form className="gdpr-form" onSubmit={handleAnonymize}>
            <p className="gdpr-warning">{t('gdpr.anonymizeWarning')}</p>
            <label>
              <span>{t('gdpr.anonymizeConfirm', { uid: selectedPatient.uid })}</span>
              <input
                value={anonymizeConfirmation}
                onChange={(event) => setAnonymizeConfirmation(event.target.value)}
                placeholder={selectedPatient.uid}
                autoFocus
              />
            </label>
            <div className="modal-footer">
              <button
                type="button"
                className="cancel-button"
                onClick={() => setShowAnonymize(false)}
              >
                {t('common.cancel')}
              </button>
              <button
                type="submit"
                className="submit-button danger"
                disabled={anonymizing || anonymizeConfirmation.trim().length === 0}
              >
                {anonymizing ? t('common.loading') : t('gdpr.anonymizeButton')}
              </button>
            </div>
          </form>
        </BaseModal>
      )}

      {showTemplateModal && (
        <BaseModal
          title={editingTemplate ? t('gdpr.editTemplate') : t('gdpr.newTemplate')}
          onClose={() => setShowTemplateModal(false)}
        >
          <form className="gdpr-form" onSubmit={handleSaveTemplate}>
            <div className="gdpr-form-row">
              <label>
                <span>{t('gdpr.type')}</span>
                <input
                  value={templateForm.consentType}
                  onChange={(event) =>
                    setTemplateForm((previous) => ({
                      ...previous,
                      consentType: event.target.value
                    }))
                  }
                  required
                />
              </label>
              <label>
                <span>{t('gdpr.version')}</span>
                <input
                  value={templateForm.version}
                  onChange={(event) =>
                    setTemplateForm((previous) => ({ ...previous, version: event.target.value }))
                  }
                  required
                />
              </label>
              <label>
                <span>{t('gdpr.language')}</span>
                <select
                  value={templateForm.language}
                  onChange={(event) =>
                    setTemplateForm((previous) => ({ ...previous, language: event.target.value }))
                  }
                >
                  <option value="cs">CS</option>
                  <option value="en">EN</option>
                  <option value="nl">NL</option>
                </select>
              </label>
            </div>
            <label>
              <span>{t('gdpr.templateTitle')}</span>
              <input
                value={templateForm.title}
                onChange={(event) =>
                  setTemplateForm((previous) => ({ ...previous, title: event.target.value }))
                }
                maxLength={200}
                required
              />
            </label>
            <label>
              <span>{t('gdpr.text')}</span>
              <textarea
                className="gdpr-template-editor"
                value={templateForm.text}
                onChange={(event) =>
                  setTemplateForm((previous) => ({ ...previous, text: event.target.value }))
                }
                rows={16}
                maxLength={20000}
                required
              />
            </label>
            <label className="gdpr-checkbox">
              <input
                type="checkbox"
                checked={templateForm.isActive}
                onChange={(event) =>
                  setTemplateForm((previous) => ({ ...previous, isActive: event.target.checked }))
                }
              />
              <span>{t('gdpr.active')}</span>
            </label>
            <div className="modal-footer">
              <button
                type="button"
                className="cancel-button"
                onClick={() => setShowTemplateModal(false)}
              >
                {t('common.cancel')}
              </button>
              <button type="submit" className="submit-button" disabled={savingTemplate}>
                {savingTemplate ? t('common.loading') : t('gdpr.save')}
              </button>
            </div>
          </form>
        </BaseModal>
      )}
    </div>
  );
};

export default GdprPage;
