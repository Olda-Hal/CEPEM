import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AppHeader } from '../components/AppHeader';
import { BaseModal } from '../components/BaseModal';
import { apiClient, getAuthToken } from '../utils/api';
import { useToast } from '../contexts/ToastContext';
import './ActivityLogPage.css';

interface ActivityLogEntry {
  id: number;
  timestamp: string;
  service: string;
  action: string;
  details: string | null;
  employeeId: number | null;
  employeeEmail: string | null;
  method: string | null;
  statusCode: number | null;
}

interface ActivityLogListResponse {
  logs: ActivityLogEntry[];
  totalCount: number;
  hasMore: boolean;
}

const PAGE_SIZE = 50;
const HTTP_METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'];

const ActivityLogPage: React.FC = () => {
  const { t, i18n } = useTranslation();
  const toast = useToast();
  const [logs, setLogs] = useState<ActivityLogEntry[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [hasMore, setHasMore] = useState(false);
  const [page, setPage] = useState(0);
  const [search, setSearch] = useState('');
  const [methodFilter, setMethodFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [loading, setLoading] = useState(true);
  const [selectedLog, setSelectedLog] = useState<ActivityLogEntry | null>(null);

  const buildParams = useCallback(
    (pageToLoad: number) => {
      const params = new URLSearchParams({ page: String(pageToLoad), limit: String(PAGE_SIZE) });
      if (search.trim()) {
        params.set('search', search.trim());
      }
      if (methodFilter) {
        params.set('method', methodFilter);
      }
      if (statusFilter) {
        params.set('statusCode', statusFilter);
      }
      return params;
    },
    [search, methodFilter, statusFilter]
  );

  const loadLogs = useCallback(
    async (pageToLoad: number) => {
      setLoading(true);
      try {
        const response = await apiClient.get<ActivityLogListResponse>(
          `/api/activitylogs?${buildParams(pageToLoad).toString()}`
        );
        setLogs(response.logs);
        setTotalCount(response.totalCount);
        setHasMore(response.hasMore);
      } catch (error) {
        console.error('Error loading activity log', error);
        toast.error(t('activityLog.loadFailed'));
      } finally {
        setLoading(false);
      }
    },
    [buildParams, t, toast]
  );

  useEffect(() => {
    void loadLogs(page);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, methodFilter, statusFilter]);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setPage(0);
    void loadLogs(0);
  };

  const handleExport = async () => {
    try {
      const token = getAuthToken();
      const response = await fetch(`/api/activitylogs/export?${buildParams(0).toString()}`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {}
      });
      if (!response.ok) {
        throw new Error(`Export failed with status ${response.status}`);
      }
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download =
        response.headers.get('Content-Disposition')?.split('filename=')[1] ?? 'activity-log.csv';
      link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      console.error('Export failed', error);
      toast.error(t('activityLog.exportFailed'));
    }
  };

  const formatTime = (timestamp: string) =>
    new Date(timestamp).toLocaleString(i18n.language, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    });

  return (
    <div className="activity-log-page">
      <AppHeader sectionTitle={t('activityLog.title')} />

      <div className="activity-log-content">
        <form className="activity-log-filters" onSubmit={handleSearch}>
          <input
            type="text"
            placeholder={t('activityLog.searchPlaceholder')}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <select
            aria-label={t('activityLog.actionFilter')}
            value={methodFilter}
            onChange={(e) => {
              setMethodFilter(e.target.value);
              setPage(0);
            }}
          >
            <option value="">
              {t('activityLog.actionFilter')}: {t('activityLog.all')}
            </option>
            {HTTP_METHODS.map((method) => (
              <option key={method} value={method}>
                {method}
              </option>
            ))}
          </select>
          <select
            aria-label={t('activityLog.statusFilter')}
            value={statusFilter}
            onChange={(e) => {
              setStatusFilter(e.target.value);
              setPage(0);
            }}
          >
            <option value="">
              {t('activityLog.statusFilter')}: {t('activityLog.all')}
            </option>
            <option value="200">200</option>
            <option value="201">201</option>
            <option value="400">400</option>
            <option value="401">401</option>
            <option value="403">403</option>
            <option value="404">404</option>
            <option value="409">409</option>
            <option value="500">500</option>
          </select>
          <button className="btn btn-primary" type="submit" disabled={loading}>
            {t('common.search')}
          </button>
          <button className="btn btn-secondary" type="button" onClick={handleExport}>
            {t('activityLog.exportCsv')}
          </button>
        </form>

        <div className="activity-log-meta">
          {loading ? t('common.loading') : t('activityLog.totalEntries', { count: totalCount })}
        </div>

        {!loading && logs.length === 0 ? (
          <div className="no-data">{t('activityLog.noEntries')}</div>
        ) : (
          <table className="activity-log-table">
            <thead>
              <tr>
                <th>{t('activityLog.time')}</th>
                <th>{t('activityLog.user')}</th>
                <th>{t('activityLog.method')}</th>
                <th>{t('activityLog.action')}</th>
                <th>{t('activityLog.status')}</th>
              </tr>
            </thead>
            <tbody>
              {logs.map((log) => (
                <tr
                  key={log.id}
                  className={`log-row-clickable${log.statusCode && log.statusCode >= 400 ? ' log-row-error' : ''}`}
                  onClick={() => setSelectedLog(log)}
                  tabIndex={0}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      setSelectedLog(log);
                    }
                  }}
                >
                  <td className="log-time">{formatTime(log.timestamp)}</td>
                  <td className="log-user">{log.employeeEmail ?? '—'}</td>
                  <td className="log-method">{log.method ?? '—'}</td>
                  <td className="log-action" title={log.details ?? undefined}>
                    {log.action}
                  </td>
                  <td
                    className={`log-status ${log.statusCode && log.statusCode >= 400 ? 'status-error' : 'status-ok'}`}
                  >
                    {log.statusCode ?? '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        <div className="activity-log-pagination">
          <button
            className="btn btn-secondary"
            disabled={page === 0 || loading}
            onClick={() => setPage((p) => Math.max(0, p - 1))}
          >
            {t('common.previous')}
          </button>
          <span className="pagination-info">
            {page + 1} / {Math.max(1, Math.ceil(totalCount / PAGE_SIZE))}
          </span>
          <button
            className="btn btn-secondary"
            disabled={!hasMore || loading}
            onClick={() => setPage((p) => p + 1)}
          >
            {t('common.next')}
          </button>
        </div>
      </div>

      {selectedLog && (
        <BaseModal title={t('activityLog.details')} onClose={() => setSelectedLog(null)}>
          <dl className="activity-log-detail">
            <dt>{t('activityLog.time')}</dt>
            <dd>{formatTime(selectedLog.timestamp)}</dd>
            <dt>{t('activityLog.user')}</dt>
            <dd>
              {selectedLog.employeeEmail ?? '—'}
              {selectedLog.employeeId != null ? ` (#${selectedLog.employeeId})` : ''}
            </dd>
            <dt>{t('activityLog.method')}</dt>
            <dd>{selectedLog.method ?? '—'}</dd>
            <dt>{t('activityLog.action')}</dt>
            <dd>{selectedLog.action}</dd>
            <dt>{t('activityLog.status')}</dt>
            <dd>{selectedLog.statusCode ?? '—'}</dd>
            <dt>{t('activityLog.details')}</dt>
            <dd className="detail-value-multiline">{selectedLog.details ?? '—'}</dd>
          </dl>
        </BaseModal>
      )}
    </div>
  );
};

export default ActivityLogPage;
