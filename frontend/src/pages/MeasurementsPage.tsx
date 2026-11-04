import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { apiClient } from '../utils/api';
import { MeasurementListResponse, MeasurementVisit } from '../types';
import { useAuth } from '../contexts/AuthContext';
import { useToast } from '../contexts/ToastContext';
import { hasRole, isAdmin } from '../utils/roles';
import './MeasurementsPage.css';

type EvaluatedFilter = 'all' | 'yes' | 'no' | 'partial';

/**
 * Measurements overview: one row per visit that carries a MEIK .tdb file.
 * The row opens the visit; a visit can be closed as evaluated without waiting
 * for two doctors, with the same effect on the Vyhodnocování queue.
 */
const MeasurementsPage: React.FC = () => {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const toast = useToast();
  const { user } = useAuth();

  const [items, setItems] = useState<MeasurementVisit[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [evaluated, setEvaluated] = useState<EvaluatedFilter>('all');
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [markingVisitId, setMarkingVisitId] = useState<number | null>(null);

  const canMarkEvaluated = isAdmin(user) || hasRole(user, 'Country Admin');

  useEffect(() => {
    const timeoutId = setTimeout(() => setDebouncedSearch(search.trim()), 300);
    return () => clearTimeout(timeoutId);
  }, [search]);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams({
        evaluated,
        page: '0',
        pageSize: '200'
      });
      if (debouncedSearch) {
        params.set('search', debouncedSearch);
      }
      const response = await apiClient.get<MeasurementListResponse>(
        `/api/measurements?${params.toString()}`
      );
      setItems(response.items ?? []);
      setTotal(response.total ?? 0);
    } catch (err) {
      console.error('Failed to load measurements', err);
      setError(t('measurements.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [debouncedSearch, evaluated, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const openVisit = (item: MeasurementVisit) => {
    navigate(`/patients/${item.patientId}/visits/${item.visitId}`);
  };

  const handleMarkEvaluated = async (item: MeasurementVisit) => {
    if (!window.confirm(t('measurements.markConfirm', { name: item.patientName }))) {
      return;
    }

    setMarkingVisitId(item.visitId);
    try {
      await apiClient.post(`/api/measurements/visits/${item.visitId}/mark-evaluated`);
      toast.success(t('measurements.markSuccess'));
      await load();
    } catch (err) {
      console.error('Failed to mark the visit as evaluated', err);
      toast.error(err instanceof Error && err.message ? err.message : t('measurements.markFailed'));
    } finally {
      setMarkingVisitId(null);
    }
  };

  const statusLabel = useMemo(
    () => ({
      evaluated: t('measurements.status.evaluated'),
      partial: t('measurements.status.partial'),
      pending: t('measurements.status.pending')
    }),
    [t]
  );

  return (
    <div className="measurements-page">
      <div className="page-heading">
        <h1 className="page-title">{t('measurements.title')}</h1>
      </div>
      <p className="page-lead">{t('measurements.lead')}</p>

      <div className="measurements-toolbar">
        <input
          type="search"
          className="measurements-search"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder={t('measurements.searchPlaceholder')}
          aria-label={t('measurements.searchPlaceholder')}
        />
        <label className="measurements-filter">
          <span>{t('measurements.filterLabel')}</span>
          <select
            value={evaluated}
            onChange={(event) => setEvaluated(event.target.value as EvaluatedFilter)}
          >
            <option value="all">{t('measurements.filter.all')}</option>
            <option value="yes">{t('measurements.filter.evaluated')}</option>
            <option value="no">{t('measurements.filter.notEvaluated')}</option>
            <option value="partial">{t('measurements.filter.partial')}</option>
          </select>
        </label>
        <span className="measurements-count">{t('measurements.count', { count: total })}</span>
      </div>

      {error && <div className="alert alert-error">{error}</div>}

      {loading ? (
        <div className="loading">{t('common.loading')}</div>
      ) : items.length === 0 ? (
        <p className="measurements-empty">{t('measurements.empty')}</p>
      ) : (
        <div className="measurements-table-wrapper">
          <table className="measurements-table">
            <thead>
              <tr>
                <th>{t('measurements.patient')}</th>
                <th>{t('measurements.visitDate')}</th>
                <th>{t('measurements.documents')}</th>
                <th>{t('measurements.statusLabel')}</th>
                <th aria-label={t('measurements.actions')} />
              </tr>
            </thead>
            <tbody>
              {items.map((item) => {
                const isEvaluated = item.status === 'evaluated';
                return (
                  <tr
                    key={item.visitId}
                    className="measurements-row"
                    tabIndex={0}
                    onClick={() => openVisit(item)}
                    onKeyDown={(event) => {
                      if (event.key === 'Enter' || event.key === ' ') {
                        event.preventDefault();
                        openVisit(item);
                      }
                    }}
                  >
                    <td>
                      <strong className="measurements-patient-name">{item.patientName}</strong>
                      <span className="measurements-patient-uid">{item.patientUid}</span>
                    </td>
                    <td>{new Date(item.visitDate).toLocaleDateString(i18n.language)}</td>
                    <td>
                      <ul className="measurements-documents">
                        {item.documents.map((document) => (
                          <li key={document.documentId}>{document.originalFileName}</li>
                        ))}
                      </ul>
                    </td>
                    <td>
                      <span className={`measurements-status ${item.status}`}>
                        {statusLabel[item.status]}
                      </span>
                      {item.isManuallyEvaluated && (
                        <span className="measurements-manual">{t('measurements.manualBadge')}</span>
                      )}
                    </td>
                    <td className="measurements-actions-cell">
                      {!isEvaluated && canMarkEvaluated && (
                        <button
                          type="button"
                          className="btn btn-secondary btn-sm"
                          disabled={markingVisitId === item.visitId}
                          title={t('measurements.markHint')}
                          onClick={(event) => {
                            event.stopPropagation();
                            void handleMarkEvaluated(item);
                          }}
                        >
                          {markingVisitId === item.visitId
                            ? t('common.loading')
                            : t('measurements.markEvaluated')}
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};

export default MeasurementsPage;
