import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { MdAdd, MdDelete, MdEdit, MdRefresh, MdSms } from 'react-icons/md';
import { BaseModal } from '../components/BaseModal';
import { DataTable } from '../components/ui/DataTable';
import { apiClient, ApiError } from '../utils/api';
import { useToast } from '../contexts/ToastContext';
import { Hospital } from '../types';
import './CommunicationTemplatesPage.css';

interface CommunicationTemplate {
  id: number;
  code: string;
  channel: 'Email' | 'Sms' | string;
  language: string;
  scopeType: 'Global' | 'Country' | 'Center' | string;
  scopeId: number;
  scopeName: string;
  subject: string | null;
  body: string;
  isActive: boolean;
  updatedAtUtc: string;
}

interface TemplateForm {
  id: number | null;
  code: string;
  channel: string;
  language: string;
  scopeType: string;
  scopeId: number;
  subject: string;
  body: string;
  isActive: boolean;
}

interface SmsLogRow {
  id: number;
  createdAtUtc: string;
  toNumber: string;
  templateCode: string;
  status: string;
  error: string | null;
  recipientName: string | null;
  reservationSlotId: number | null;
}

const CODES = [
  'password_reset',
  'intake_reminder',
  'reservation_confirmed',
  'reservation_cancelled',
  'appointment_reminder'
];

const CODE_VARIABLES: Record<string, string[]> = {
  password_reset: ['name', 'resetUrl'],
  intake_reminder: ['patientName', 'date', 'link'],
  reservation_confirmed: ['patientName', 'clinic', 'date', 'time', 'doctor'],
  reservation_cancelled: ['patientName', 'clinic', 'date', 'time'],
  appointment_reminder: ['date', 'time', 'clinic']
};

const SAMPLE_VALUES: Record<string, string> = {
  name: 'Jan Novák',
  patientName: 'Jan Novák',
  resetUrl: 'https://cepem.example/reset/abc',
  link: 'https://cepem.example/intake/abc',
  date: '12. 11. 2026',
  time: '09:30',
  clinic: 'CEPEM Praha',
  doctor: 'MUDr. Eva Svobodová'
};

const COUNTRIES = [
  { scopeId: 203, code: 'CZ' },
  { scopeId: 276, code: 'DE' },
  { scopeId: 703, code: 'SK' },
  { scopeId: 40, code: 'AT' },
  { scopeId: 616, code: 'PL' },
  { scopeId: 528, code: 'NL' }
];

const LANGUAGES = ['cs', 'en', 'nl', 'de', 'sk', 'pl'];

const emptyForm = (): TemplateForm => ({
  id: null,
  code: CODES[0] ?? 'password_reset',
  channel: 'Email',
  language: 'cs',
  scopeType: 'Global',
  scopeId: 0,
  subject: '',
  body: '',
  isActive: true
});

const renderPreview = (text: string): string =>
  text.replace(/\{\{\s*(\w+)\s*\}\}/g, (match, key: string) => SAMPLE_VALUES[key] ?? match);

const formatTime = (value: string, locale: string) => {
  const normalized = value.endsWith('Z') ? value : `${value}Z`;
  return new Date(normalized).toLocaleString(locale, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  });
};

const CommunicationTemplatesPage: React.FC = () => {
  const { t, i18n } = useTranslation();
  const toast = useToast();

  const [templates, setTemplates] = useState<CommunicationTemplate[]>([]);
  const [hospitals, setHospitals] = useState<Hospital[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [channelFilter, setChannelFilter] = useState('');
  const [codeFilter, setCodeFilter] = useState('');
  const [languageFilter, setLanguageFilter] = useState('');
  const [scopeFilter, setScopeFilter] = useState('');
  const [search, setSearch] = useState('');
  const [activeTab, setActiveTab] = useState<'templates' | 'smsLog'>('templates');
  const [form, setForm] = useState<TemplateForm | null>(null);
  const [saving, setSaving] = useState(false);
  const [smsLog, setSmsLog] = useState<SmsLogRow[]>([]);
  const [smsLogLoading, setSmsLogLoading] = useState(false);

  const loadTemplates = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams();
      if (channelFilter) params.set('channel', channelFilter);
      if (codeFilter) params.set('code', codeFilter);
      if (languageFilter) params.set('language', languageFilter);
      if (scopeFilter) params.set('scopeType', scopeFilter);
      if (search.trim()) params.set('search', search.trim());
      const result = await apiClient.get<CommunicationTemplate[]>(
        `/api/communicationtemplates?${params.toString()}`
      );
      setTemplates(Array.isArray(result) ? result : []);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('communicationTemplates.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [channelFilter, codeFilter, languageFilter, scopeFilter, search, t]);

  useEffect(() => {
    void loadTemplates();
  }, [loadTemplates]);

  useEffect(() => {
    void apiClient
      .get<Hospital[]>('/api/hospitals')
      .then((result) => setHospitals(Array.isArray(result) ? result : []))
      .catch(() => setHospitals([]));
  }, []);

  const loadSmsLog = useCallback(async () => {
    setSmsLogLoading(true);
    try {
      const rows = await apiClient.get<SmsLogRow[]>(
        '/api/communicationtemplates/sms-log?limit=100'
      );
      setSmsLog(Array.isArray(rows) ? rows : []);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('communicationTemplates.loadFailed'));
    } finally {
      setSmsLogLoading(false);
    }
  }, [t, toast]);

  useEffect(() => {
    if (activeTab === 'smsLog') {
      void loadSmsLog();
    }
  }, [activeTab, loadSmsLog]);

  const variables = useMemo(() => CODE_VARIABLES[form?.code ?? ''] ?? [], [form?.code]);

  const openCreate = () => setForm(emptyForm());

  const openEdit = (template: CommunicationTemplate) => {
    setForm({
      id: template.id,
      code: template.code,
      channel: template.channel,
      language: template.language,
      scopeType: template.scopeType,
      scopeId: template.scopeId,
      subject: template.subject ?? '',
      body: template.body,
      isActive: template.isActive
    });
  };

  const updateForm = (patch: Partial<TemplateForm>) => {
    setForm((current) => (current ? { ...current, ...patch } : current));
  };

  const handleScopeTypeChange = (scopeType: string) => {
    const scopeId =
      scopeType === 'Country'
        ? (COUNTRIES[0]?.scopeId ?? 203)
        : scopeType === 'Center'
          ? (hospitals[0]?.id ?? 0)
          : 0;
    updateForm({ scopeType, scopeId });
  };

  const handleSave = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!form) return;
    if (form.channel === 'Email' && !form.subject.trim()) {
      toast.error(t('communicationTemplates.subjectRequired'));
      return;
    }
    if (!form.body.trim()) {
      toast.error(t('communicationTemplates.bodyRequired'));
      return;
    }

    setSaving(true);
    const payload = {
      code: form.code,
      channel: form.channel,
      language: form.language,
      scopeType: form.scopeType,
      scopeId: form.scopeId,
      subject: form.channel === 'Email' ? form.subject : null,
      body: form.body,
      isActive: form.isActive
    };
    try {
      if (form.id == null) {
        await apiClient.post('/api/communicationtemplates', payload);
      } else {
        await apiClient.put(`/api/communicationtemplates/${form.id}`, payload);
      }
      toast.success(t('communicationTemplates.saved'));
      setForm(null);
      await loadTemplates();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('communicationTemplates.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (template: CommunicationTemplate) => {
    if (!window.confirm(t('communicationTemplates.confirmDelete', { code: template.code }))) {
      return;
    }
    try {
      await apiClient.delete(`/api/communicationtemplates/${template.id}`);
      toast.success(t('communicationTemplates.deleted'));
      await loadTemplates();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('communicationTemplates.deleteFailed'));
    }
  };

  const scopeLabel = (template: CommunicationTemplate) => {
    if (template.scopeType === 'Global') return t('communicationTemplates.scopes.Global');
    if (template.scopeType === 'Country') {
      return `${t('communicationTemplates.scopes.Country')} · ${template.scopeName}`;
    }
    return `${t('communicationTemplates.scopes.Center')} · ${template.scopeName}`;
  };

  const isOverridden = (template: CommunicationTemplate) => template.scopeType !== 'Global';

  return (
    <div className="communication-templates-page">
      <div className="communication-header">
        <div>
          <h1 className="page-title">{t('communicationTemplates.title')}</h1>
          <p className="page-lead">{t('communicationTemplates.lead')}</p>
        </div>
        <div className="communication-header-actions">
          <button className="btn btn-secondary" type="button" onClick={() => void loadTemplates()}>
            <MdRefresh /> {t('communicationTemplates.refresh')}
          </button>
          <button className="btn btn-primary" type="button" onClick={openCreate}>
            <MdAdd /> {t('communicationTemplates.newTemplate')}
          </button>
        </div>
      </div>

      <div className="communication-tabs" role="tablist">
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'templates'}
          className={activeTab === 'templates' ? 'active' : ''}
          onClick={() => setActiveTab('templates')}
        >
          {t('communicationTemplates.tabTemplates')}
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'smsLog'}
          className={activeTab === 'smsLog' ? 'active' : ''}
          onClick={() => setActiveTab('smsLog')}
        >
          {t('communicationTemplates.tabSmsLog')}
        </button>
      </div>

      {activeTab === 'templates' ? (
        <>
          <form
            className="communication-filters"
            onSubmit={(event) => {
              event.preventDefault();
              void loadTemplates();
            }}
          >
            <input
              type="search"
              placeholder={t('communicationTemplates.searchPlaceholder')}
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
            <select
              aria-label={t('communicationTemplates.channel')}
              value={channelFilter}
              onChange={(event) => setChannelFilter(event.target.value)}
            >
              <option value="">{t('communicationTemplates.allChannels')}</option>
              <option value="Email">E-mail</option>
              <option value="Sms">SMS</option>
            </select>
            <select
              aria-label={t('communicationTemplates.code')}
              value={codeFilter}
              onChange={(event) => setCodeFilter(event.target.value)}
            >
              <option value="">{t('communicationTemplates.allCodes')}</option>
              {CODES.map((code) => (
                <option key={code} value={code}>
                  {t(`communicationTemplates.codes.${code}`, code)}
                </option>
              ))}
            </select>
            <select
              aria-label={t('communicationTemplates.language')}
              value={languageFilter}
              onChange={(event) => setLanguageFilter(event.target.value)}
            >
              <option value="">{t('communicationTemplates.allLanguages')}</option>
              {LANGUAGES.map((language) => (
                <option key={language} value={language}>
                  {language.toUpperCase()}
                </option>
              ))}
            </select>
            <select
              aria-label={t('communicationTemplates.scope')}
              value={scopeFilter}
              onChange={(event) => setScopeFilter(event.target.value)}
            >
              <option value="">{t('communicationTemplates.allScopes')}</option>
              <option value="Global">{t('communicationTemplates.scopes.Global')}</option>
              <option value="Country">{t('communicationTemplates.scopes.Country')}</option>
              <option value="Center">{t('communicationTemplates.scopes.Center')}</option>
            </select>
            <button className="btn btn-primary" type="submit" disabled={loading}>
              {t('common.search')}
            </button>
          </form>

          {error && <div className="communication-error">{error}</div>}

          <DataTable
            columns={[
              {
                key: 'code',
                header: t('communicationTemplates.code'),
                render: (template) => (
                  <div className="communication-code-cell">
                    <strong>
                      {t(`communicationTemplates.codes.${template.code}`, template.code)}
                    </strong>
                    <code>{template.code}</code>
                  </div>
                ),
                width: '18%'
              },
              {
                key: 'channel',
                header: t('communicationTemplates.channel'),
                render: (template) =>
                  template.channel === 'Sms'
                    ? t('communicationTemplates.channels.sms')
                    : t('communicationTemplates.channels.email'),
                width: '8%'
              },
              {
                key: 'language',
                header: t('communicationTemplates.language'),
                render: (template) => template.language.toUpperCase(),
                width: '6%'
              },
              {
                key: 'scope',
                header: t('communicationTemplates.scope'),
                render: (template) => (
                  <span
                    className={`communication-scope ${
                      isOverridden(template) ? 'communication-scope-override' : ''
                    }`}
                  >
                    {scopeLabel(template)}
                  </span>
                ),
                width: '18%'
              },
              {
                key: 'subject',
                header: t('communicationTemplates.subject'),
                render: (template) => (
                  <span className="communication-subject">
                    {template.subject || template.body.slice(0, 80)}
                  </span>
                )
              },
              {
                key: 'status',
                header: t('communicationTemplates.status'),
                render: (template) => (
                  <span className={template.isActive ? 'status-ok' : 'status-error'}>
                    {template.isActive
                      ? t('communicationTemplates.active')
                      : t('communicationTemplates.inactive')}
                  </span>
                ),
                align: 'center',
                width: '8%'
              },
              {
                key: 'actions',
                header: '',
                render: (template) => (
                  <div className="communication-row-actions">
                    <button
                      type="button"
                      className="icon-button"
                      title={t('common.edit')}
                      aria-label={t('common.edit')}
                      onClick={(event) => {
                        event.stopPropagation();
                        openEdit(template);
                      }}
                    >
                      <MdEdit />
                    </button>
                    <button
                      type="button"
                      className="icon-button danger"
                      title={t('common.delete')}
                      aria-label={t('common.delete')}
                      onClick={(event) => {
                        event.stopPropagation();
                        void handleDelete(template);
                      }}
                    >
                      <MdDelete />
                    </button>
                  </div>
                ),
                align: 'right',
                width: '8%'
              }
            ]}
            data={templates}
            rowKey={(template) => template.id}
            onRowClick={openEdit}
            loading={loading}
            emptyMessage={t('communicationTemplates.noTemplates')}
          />
        </>
      ) : (
        <div className="communication-sms-log">
          <div className="communication-sms-log-header">
            <div>
              <h2>{t('communicationTemplates.smsLogTitle')}</h2>
              <p>{t('communicationTemplates.smsLogLead')}</p>
            </div>
            <button
              className="btn btn-secondary"
              type="button"
              onClick={() => void loadSmsLog()}
              disabled={smsLogLoading}
            >
              <MdRefresh /> {t('communicationTemplates.refresh')}
            </button>
          </div>
          <DataTable
            columns={[
              {
                key: 'time',
                header: t('communicationTemplates.time'),
                render: (row) => formatTime(row.createdAtUtc, i18n.language),
                width: '18%'
              },
              {
                key: 'recipient',
                header: t('communicationTemplates.recipient'),
                render: (row) => (
                  <div>
                    <strong>{row.recipientName || row.toNumber}</strong>
                    {row.recipientName && <div className="communication-muted">{row.toNumber}</div>}
                  </div>
                )
              },
              {
                key: 'template',
                header: t('communicationTemplates.code'),
                render: (row) => (
                  <span>
                    <MdSms />{' '}
                    {t(`communicationTemplates.codes.${row.templateCode}`, row.templateCode)}
                  </span>
                ),
                width: '18%'
              },
              {
                key: 'status',
                header: t('communicationTemplates.status'),
                render: (row) => (
                  <span
                    className={
                      row.status === 'Failed'
                        ? 'status-error'
                        : row.status === 'LoggedOnly'
                          ? 'status-logged-only'
                          : 'status-ok'
                    }
                  >
                    {row.status}
                  </span>
                ),
                align: 'center',
                width: '10%'
              },
              {
                key: 'error',
                header: t('communicationTemplates.error'),
                render: (row) => <span className="communication-muted">{row.error || '—'}</span>
              }
            ]}
            data={smsLog}
            rowKey={(row) => row.id}
            loading={smsLogLoading}
            emptyMessage={t('communicationTemplates.noSmsLog')}
          />
        </div>
      )}

      {form && (
        <BaseModal
          title={
            form.id == null
              ? t('communicationTemplates.newTemplate')
              : t('communicationTemplates.editTemplate')
          }
          onClose={() => setForm(null)}
          contentClassName="communication-modal"
        >
          <form className="communication-form" onSubmit={handleSave}>
            <div className="communication-form-grid">
              <label>
                <span>{t('communicationTemplates.code')}</span>
                <input
                  list="communication-template-codes"
                  value={form.code}
                  onChange={(event) => updateForm({ code: event.target.value })}
                  required
                />
                <datalist id="communication-template-codes">
                  {CODES.map((code) => (
                    <option key={code} value={code}>
                      {t(`communicationTemplates.codes.${code}`, code)}
                    </option>
                  ))}
                </datalist>
              </label>
              <label>
                <span>{t('communicationTemplates.channel')}</span>
                <select
                  value={form.channel}
                  onChange={(event) => updateForm({ channel: event.target.value })}
                >
                  <option value="Email">{t('communicationTemplates.channels.email')}</option>
                  <option value="Sms">{t('communicationTemplates.channels.sms')}</option>
                </select>
              </label>
              <label>
                <span>{t('communicationTemplates.language')}</span>
                <input
                  list="communication-template-languages"
                  value={form.language}
                  onChange={(event) => updateForm({ language: event.target.value })}
                  required
                />
                <datalist id="communication-template-languages">
                  {LANGUAGES.map((language) => (
                    <option key={language} value={language} />
                  ))}
                </datalist>
              </label>
              <label>
                <span>{t('communicationTemplates.scope')}</span>
                <select
                  value={form.scopeType}
                  onChange={(event) => handleScopeTypeChange(event.target.value)}
                >
                  <option value="Global">{t('communicationTemplates.scopes.Global')}</option>
                  <option value="Country">{t('communicationTemplates.scopes.Country')}</option>
                  <option value="Center">{t('communicationTemplates.scopes.Center')}</option>
                </select>
              </label>
              {form.scopeType === 'Country' && (
                <label>
                  <span>{t('communicationTemplates.country')}</span>
                  <select
                    value={form.scopeId}
                    onChange={(event) => updateForm({ scopeId: Number(event.target.value) })}
                  >
                    {COUNTRIES.map((country) => (
                      <option key={country.scopeId} value={country.scopeId}>
                        {country.code}
                      </option>
                    ))}
                  </select>
                </label>
              )}
              {form.scopeType === 'Center' && (
                <label>
                  <span>{t('communicationTemplates.center')}</span>
                  <select
                    value={form.scopeId}
                    onChange={(event) => updateForm({ scopeId: Number(event.target.value) })}
                    required
                  >
                    <option value={0} disabled>
                      {t('communicationTemplates.selectCenter')}
                    </option>
                    {hospitals.map((hospital) => (
                      <option key={hospital.id} value={hospital.id}>
                        {hospital.name}
                      </option>
                    ))}
                  </select>
                </label>
              )}
            </div>

            <label className="communication-checkbox">
              <input
                type="checkbox"
                checked={form.isActive}
                onChange={(event) => updateForm({ isActive: event.target.checked })}
              />
              <span>{t('communicationTemplates.active')}</span>
            </label>

            {form.channel === 'Email' && (
              <label>
                <span>{t('communicationTemplates.subject')}</span>
                <input
                  value={form.subject}
                  onChange={(event) => updateForm({ subject: event.target.value })}
                  maxLength={300}
                  required
                />
              </label>
            )}

            <div className="communication-editor">
              <label>
                <span>{t('communicationTemplates.body')}</span>
                <textarea
                  value={form.body}
                  onChange={(event) => updateForm({ body: event.target.value })}
                  rows={12}
                  required
                />
              </label>
              <div className="communication-preview">
                <h3>{t('communicationTemplates.preview')}</h3>
                {form.channel === 'Email' && (
                  <div className="communication-preview-subject">
                    {renderPreview(form.subject) || '—'}
                  </div>
                )}
                <pre>{renderPreview(form.body)}</pre>
                <p className="communication-preview-note">
                  {t('communicationTemplates.previewNote')}
                </p>
              </div>
            </div>

            <div className="communication-variables">
              <strong>{t('communicationTemplates.variables')}</strong>
              {variables.length > 0 ? (
                variables.map((variable) => (
                  <button
                    key={variable}
                    type="button"
                    className="communication-variable"
                    onClick={() => updateForm({ body: `${form.body}{{${variable}}}` })}
                  >
                    {`{{${variable}}}`}
                  </button>
                ))
              ) : (
                <span className="communication-muted">
                  {t('communicationTemplates.noVariables')}
                </span>
              )}
            </div>

            <div className="communication-form-actions">
              <button
                className="btn btn-secondary"
                type="button"
                onClick={() => setForm(null)}
                disabled={saving}
              >
                {t('common.cancel')}
              </button>
              <button className="btn btn-primary" type="submit" disabled={saving}>
                {saving ? t('common.saving') : t('common.save')}
              </button>
            </div>
          </form>
        </BaseModal>
      )}
    </div>
  );
};

export default CommunicationTemplatesPage;
