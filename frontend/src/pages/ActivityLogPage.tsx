import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AppHeader } from '../components/AppHeader';
import { apiClient } from '../utils/api';
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

const ActivityLogPage: React.FC = () => {
  const { t, i18n } = useTranslation();
  const toast = useToast();
  const [logs, setLogs] = useState<ActivityLogEntry[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [hasMore, setHasMore] = useState(false);
  const [page, setPage] = useState(0);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);

  const loadLogs = useCallback(async (pageToLoad: number, searchTerm: string) => {
    setLoading(true);
    try {
      const params = new URLSearchParams({ page: String(pageToLoad), limit: String(PAGE_SIZE) });
      if (searchTerm.trim()) {
        params.set('search', searchTerm.trim());
      }
      const response = await apiClient.get<ActivityLogListResponse>(`/api/activitylogs?${params.toString()}`);
      setLogs(response.logs);
      setTotalCount(response.totalCount);
      setHasMore(response.hasMore);
    } catch (error) {
      console.error('Error loading activity log', error);
      toast.error(t('activityLog.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [t, toast]);

  useEffect(() => {
    void loadLogs(page, search);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page]);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setPage(0);
    void loadLogs(0, search);
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
          <button className="btn btn-primary" type="submit" disabled={loading}>
            {t('common.search')}
          </button>
        </form>

        <div className="activity-log-meta">
          {loading ? (
            t('common.loading')
          ) : (
            t('activityLog.totalEntries', { count: totalCount })
          )}
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
                <tr key={log.id} className={log.statusCode && log.statusCode >= 400 ? 'log-row-error' : undefined}>
                  <td className="log-time">{formatTime(log.timestamp)}</td>
                  <td className="log-user">{log.employeeEmail ?? '—'}</td>
                  <td className="log-method">{log.method ?? '—'}</td>
                  <td className="log-action" title={log.details ?? undefined}>{log.action}</td>
                  <td className={`log-status ${log.statusCode && log.statusCode >= 400 ? 'status-error' : 'status-ok'}`}>
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
    </div>
  );
};

export default ActivityLogPage;
