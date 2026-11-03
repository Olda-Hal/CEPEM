import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { MdAdd, MdDelete, MdEdit, MdRefresh } from 'react-icons/md';
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

/**
 * Logical template: one code/channel/scope with language variants behind it.
 * The table shows one row per logical template and the editor lets the admin
 * pick which language variant to display or edit.
 */
interface TemplateGroup {
  key: string;
  code: string;
  channel: string;
  scopeType: string;
  scopeId: number;
  scopeName: string;
  variants: CommunicationTemplate[];
  primary: CommunicationTemplate;
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
  variants: CommunicationTemplate[];
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
  isActive: true,
  variants: []
});

const renderPreview = (text: string): string =>
  text.replace(/\{\{\s*(\w+)\s*\}\}/g, (match, key: string) => SAMPLE_VALUES[key] ?? match);

const preferPrimaryVariant = (variants: CommunicationTemplate[]): CommunicationTemplate | null =>
  variants.find((variant) => variant.language === 'cs' && variant.isActive) ??
  variants.find((variant) => variant.language === 'en' && variant.isActive) ??
  variants.find((variant) => variant.language === 'cs') ??
  variants.find((variant) => variant.language === 'en') ??
  variants[0] ??
  null;

const groupTemplates = (templates: CommunicationTemplate[]): TemplateGroup[] => {
  const groups = new Map<string, TemplateGroup>();

  for (const template of templates) {
    const key = `${template.code}|${template.channel}|${template.scopeType}|${template.scopeId}`;
    const existing = groups.get(key);
    if (existing) {
      existing.variants.push(template);
      continue;
    }
    groups.set(key, {
      key,
      code: template.code,
      channel: template.channel,
      scopeType: template.scopeType,
      scopeId: template.scopeId,
      scopeName: template.scopeName,
      variants: [template],
      primary: template
    });
  }

  return Array.from(groups.values())
    .map((group) => {
      const variants = [...group.variants].sort((a, b) => a.language.localeCompare(b.language));
      const primary = preferPrimaryVariant(variants);
      return primary ? { ...group, variants, primary } : null;
    })
    .filter((group): group is TemplateGroup => group !== null)
    .sort(
      (a, b) =>
        a.code.localeCompare(b.code) ||
        a.channel.localeCompare(b.channel) ||
        a.scopeType.localeCompare(b.scopeType) ||
        a.scopeName.localeCompare(b.scopeName)
    );
};

const CommunicationTemplatesPage: React.FC = () => {
  const { t } = useTranslation();
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
  const [form, setForm] = useState<TemplateForm | null>(null);
  const [saving, setSaving] = useState(false);

  const loadTemplates = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams();
      if (channelFilter) params.set('channel', channelFilter);
      if (codeFilter) params.set('code', codeFilter);
      if (scopeFilter) params.set('scopeType', scopeFilter);
      if (search.trim()) params.set('search', search.trim());
      const result = await apiClient.get<CommunicationTemplate[]>(
        `/api/communicationtemplates?${params.toString()}`
      );
      // All language variants are loaded together so the editor can switch
      // between them; the language filter is applied to the grouped rows.
      setTemplates(Array.isArray(result) ? result : []);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('communicationTemplates.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [channelFilter, codeFilter, scopeFilter, search, t]);

  useEffect(() => {
    void loadTemplates();
  }, [loadTemplates]);

  useEffect(() => {
    void apiClient
      .get<Hospital[]>('/api/hospitals')
      .then((result) => setHospitals(Array.isArray(result) ? result : []))
      .catch(() => setHospitals([]));
  }, []);

  const groups = useMemo(() => groupTemplates(templates), [templates]);

  const visibleGroups = useMemo(
    () =>
      languageFilter
        ? groups.filter((group) =>
            group.variants.some((variant) => variant.language === languageFilter)
          )
        : groups,
    [groups, languageFilter]
  );

  const variables = useMemo(() => CODE_VARIABLES[form?.code ?? ''] ?? [], [form?.code]);

  const openCreate = () => setForm(emptyForm());

  const openEdit = (group: TemplateGroup) => {
    const primary = group.primary;
    setForm({
      id: primary.id,
      code: primary.code,
      channel: primary.channel,
      language: primary.language,
      scopeType: primary.scopeType,
      scopeId: primary.scopeId,
      subject: primary.subject ?? '',
      body: primary.body,
      isActive: primary.isActive,
      variants: group.variants
    });
  };

  const updateForm = (patch: Partial<TemplateForm>) => {
    setForm((current) => (current ? { ...current, ...patch } : current));
  };

  const handleLanguageChange = (language: string) => {
    if (!form) return;
    const existing = form.variants.find((variant) => variant.language === language);
    if (existing) {
      updateForm({
        id: existing.id,
        language,
        subject: existing.subject ?? '',
        body: existing.body,
        isActive: existing.isActive
      });
      return;
    }
    updateForm({ id: null, language, subject: '', body: '', isActive: true });
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

  const handleDelete = async (group: TemplateGroup) => {
    if (!window.confirm(t('communicationTemplates.confirmDelete', { code: group.code }))) {
      return;
    }
    try {
      await Promise.all(
        group.variants.map((variant) =>
          apiClient.delete(`/api/communicationtemplates/${variant.id}`)
        )
      );
      toast.success(t('communicationTemplates.deleted'));
      await loadTemplates();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('communicationTemplates.deleteFailed'));
    }
  };

  const scopeLabel = (group: TemplateGroup) => {
    if (group.scopeType === 'Global') return t('communicationTemplates.scopes.Global');
    if (group.scopeType === 'Country') {
      return `${t('communicationTemplates.scopes.Country')} · ${group.scopeName}`;
    }
    return `${t('communicationTemplates.scopes.Center')} · ${group.scopeName}`;
  };

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
          <option value="Email">{t('communicationTemplates.channels.email')}</option>
          <option value="Sms">{t('communicationTemplates.channels.sms')}</option>
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
            render: (group) => (
              <div className="communication-code-cell">
                <strong>{t(`communicationTemplates.codes.${group.code}`, group.code)}</strong>
                <code>{group.code}</code>
              </div>
            ),
            width: '18%'
          },
          {
            key: 'channel',
            header: t('communicationTemplates.channel'),
            render: (group) =>
              group.channel === 'Sms'
                ? t('communicationTemplates.channels.sms')
                : t('communicationTemplates.channels.email'),
            width: '8%'
          },
          {
            key: 'languages',
            header: t('communicationTemplates.languages'),
            render: (group) => (
              <div className="communication-languages">
                {group.variants.map((variant) => (
                  <span
                    key={variant.id}
                    className={`communication-language${
                      variant.isActive ? '' : ' communication-language-inactive'
                    }`}
                    title={
                      variant.isActive
                        ? t('communicationTemplates.active')
                        : t('communicationTemplates.inactive')
                    }
                  >
                    {variant.language.toUpperCase()}
                  </span>
                ))}
              </div>
            ),
            width: '14%'
          },
          {
            key: 'scope',
            header: t('communicationTemplates.scope'),
            render: (group) => (
              <span
                className={`communication-scope ${
                  group.scopeType !== 'Global' ? 'communication-scope-override' : ''
                }`}
              >
                {scopeLabel(group)}
              </span>
            ),
            width: '17%'
          },
          {
            key: 'subject',
            header: t('communicationTemplates.subject'),
            render: (group) => (
              <span className="communication-subject">
                {group.primary.subject || group.primary.body.slice(0, 80)}
              </span>
            )
          },
          {
            key: 'status',
            header: t('communicationTemplates.status'),
            render: (group) => {
              const active = group.variants.some((variant) => variant.isActive);
              return (
                <span className={active ? 'status-ok' : 'status-error'}>
                  {active
                    ? t('communicationTemplates.active')
                    : t('communicationTemplates.inactive')}
                </span>
              );
            },
            align: 'center',
            width: '8%'
          },
          {
            key: 'actions',
            header: '',
            render: (group) => (
              <div className="communication-row-actions">
                <button
                  type="button"
                  className="icon-button"
                  title={t('common.edit')}
                  aria-label={t('common.edit')}
                  onClick={(event) => {
                    event.stopPropagation();
                    openEdit(group);
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
                    void handleDelete(group);
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
        data={visibleGroups}
        rowKey={(group) => group.key}
        onRowClick={openEdit}
        loading={loading}
        emptyMessage={t('communicationTemplates.noTemplates')}
      />

      {form && (
        <BaseModal
          title={
            form.variants.length === 0
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
                <select
                  value={form.language}
                  onChange={(event) => handleLanguageChange(event.target.value)}
                >
                  {Array.from(
                    new Set([...form.variants.map((variant) => variant.language), ...LANGUAGES])
                  ).map((language) => (
                    <option key={language} value={language}>
                      {language.toUpperCase()}
                      {form.variants.some((variant) => variant.language === language)
                        ? ''
                        : ` — ${t('communicationTemplates.newLanguageVariant')}`}
                    </option>
                  ))}
                </select>
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

            <p className="communication-language-hint">
              {t('communicationTemplates.languageHint')}
            </p>

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
