import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { apiClient, ApiError } from '../utils/api';
import { BaseModal } from './BaseModal';
import './SavedViews.css';

export type SavedViewSurface = 'patients' | 'reservations' | 'employees';

interface SavedView {
  id: number;
  surface: string;
  name: string;
  filtersJson: string;
  isDefault: boolean;
  updatedAtUtc: string;
}

interface SavedViewsProps {
  surface: SavedViewSurface;
  /** Current filters, serialized when the user saves a view. */
  filters: Record<string, unknown>;
  onApply: (filters: Record<string, unknown>) => void;
  /** Apply the stored default view once when the page loads. */
  autoApplyDefault?: boolean;
}

const parseFilters = (json: string): Record<string, unknown> => {
  try {
    const parsed: unknown = JSON.parse(json);
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed)
      ? (parsed as Record<string, unknown>)
      : {};
  } catch {
    return {};
  }
};

export const SavedViews: React.FC<SavedViewsProps> = ({
  surface,
  filters,
  onApply,
  autoApplyDefault = true
}) => {
  const { t } = useTranslation();
  const [views, setViews] = useState<SavedView[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showSave, setShowSave] = useState(false);
  const [name, setName] = useState('');
  const [makeDefault, setMakeDefault] = useState(false);
  const [saving, setSaving] = useState(false);
  const onApplyRef = useRef(onApply);
  const defaultAppliedRef = useRef(false);

  onApplyRef.current = onApply;

  useEffect(() => {
    defaultAppliedRef.current = false;
    setSelectedId(null);
  }, [surface]);

  const loadViews = useCallback(
    async (applyDefault: boolean) => {
      setLoading(true);
      setError(null);
      try {
        const result = await apiClient.get<SavedView[]>(`/api/savedviews?surface=${surface}`);
        // Defensive: an older backend or a test double may not return a list.
        const loadedViews = Array.isArray(result) ? result : [];
        setViews(loadedViews);

        if (applyDefault && autoApplyDefault && !defaultAppliedRef.current) {
          defaultAppliedRef.current = true;
          const defaultView = loadedViews.find((view) => view.isDefault);
          if (defaultView) {
            setSelectedId(defaultView.id);
            onApplyRef.current(parseFilters(defaultView.filtersJson));
          }
        }
      } catch (err) {
        setError(err instanceof ApiError ? err.message : t('savedViews.loadFailed'));
      } finally {
        setLoading(false);
      }
    },
    [surface, autoApplyDefault, t]
  );

  useEffect(() => {
    void loadViews(true);
  }, [loadViews]);

  const selectedView = useMemo(
    () => views.find((view) => view.id === selectedId) ?? null,
    [views, selectedId]
  );

  const handleSelect = (value: string) => {
    const id = value ? Number(value) : null;
    setSelectedId(id);
    const view = views.find((entry) => entry.id === id);
    if (view) {
      onApply(parseFilters(view.filtersJson));
    }
  };

  const handleSave = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!name.trim()) {
      return;
    }

    setSaving(true);
    setError(null);
    try {
      const created = await apiClient.post<SavedView>('/api/savedviews', {
        surface,
        name: name.trim(),
        filtersJson: JSON.stringify(filters),
        isDefault: makeDefault
      });
      setShowSave(false);
      setName('');
      setMakeDefault(false);
      await loadViews(false);
      setSelectedId(created.id);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('savedViews.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    if (!selectedView) {
      return;
    }
    if (!window.confirm(t('savedViews.confirmDelete', { name: selectedView.name }))) {
      return;
    }

    try {
      await apiClient.delete(`/api/savedviews/${selectedView.id}`);
      setSelectedId(null);
      await loadViews(false);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('savedViews.deleteFailed'));
    }
  };

  const handleSetDefault = async () => {
    if (!selectedView) {
      return;
    }
    try {
      await apiClient.post(`/api/savedviews/${selectedView.id}/default`, {});
      await loadViews(false);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('savedViews.defaultFailed'));
    }
  };

  return (
    <div className="saved-views">
      <label className="saved-views-select">
        <span>{t('savedViews.label')}</span>
        <select
          value={selectedId ?? ''}
          onChange={(event) => handleSelect(event.target.value)}
          disabled={loading}
        >
          <option value="">{t('savedViews.none')}</option>
          {views.map((view) => (
            <option key={view.id} value={view.id}>
              {view.isDefault ? '★ ' : ''}
              {view.name}
            </option>
          ))}
        </select>
      </label>

      <button
        type="button"
        className="btn btn-secondary btn-sm"
        onClick={() => {
          setName(selectedView ? `${selectedView.name} (kopie)` : '');
          setMakeDefault(false);
          setShowSave(true);
        }}
      >
        {t('savedViews.save')}
      </button>

      {selectedView && (
        <>
          <button
            type="button"
            className="btn btn-secondary btn-sm"
            onClick={handleSetDefault}
            disabled={selectedView.isDefault}
          >
            {selectedView.isDefault ? t('savedViews.isDefault') : t('savedViews.setDefault')}
          </button>
          <button type="button" className="btn btn-danger btn-sm" onClick={handleDelete}>
            {t('savedViews.delete')}
          </button>
        </>
      )}

      {error && <span className="saved-views-error">{error}</span>}

      {showSave && (
        <BaseModal title={t('savedViews.saveTitle')} onClose={() => setShowSave(false)}>
          <form className="saved-views-form" onSubmit={handleSave}>
            <label>
              <span>{t('savedViews.name')}</span>
              <input
                value={name}
                onChange={(event) => setName(event.target.value)}
                maxLength={100}
                required
                autoFocus
              />
            </label>
            <label className="saved-views-checkbox">
              <input
                type="checkbox"
                checked={makeDefault}
                onChange={(event) => setMakeDefault(event.target.checked)}
              />
              <span>{t('savedViews.makeDefault')}</span>
            </label>
            <p className="saved-views-hint">{t('savedViews.hint')}</p>
            <div className="modal-footer">
              <button type="button" className="cancel-button" onClick={() => setShowSave(false)}>
                {t('common.cancel')}
              </button>
              <button type="submit" className="submit-button" disabled={saving || !name.trim()}>
                {saving ? t('common.loading') : t('savedViews.save')}
              </button>
            </div>
          </form>
        </BaseModal>
      )}
    </div>
  );
};

export default SavedViews;
