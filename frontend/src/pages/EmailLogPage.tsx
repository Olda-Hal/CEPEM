import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { BaseModal } from '../components/BaseModal';
import { DataTable } from '../components/ui/DataTable';
import { apiClient, getAuthToken } from '../utils/api';
import { useToast } from '../contexts/ToastContext';
import './ActivityLogPage.css';
import './EmailLogPage.css';

interface EmailLogEntry {
  id: number;
  createdAtUtc: string;
  toAddress: string;
  subject: string;
  kind: string;
  status: string;
  error: string | null;
  personId: number | null;
  recipientName: string | null;
  reservationSlotId: number | null;
}

interface EmailLogListResponse {
  logs: EmailLogEntry[];
  totalCount: number;
  hasMore: boolean;
}

const PAGE_SIZE = 50;
const KINDS = ['PasswordReset', 'IntakeReminder'];
const STATUSES = ['Sent', 'LoggedOnly', 'Failed'];

const kindKey = (kind: string) =>
  kind === 'PasswordReset'
    ? 'passwordReset'
    : kind === 'IntakeReminder'
      ? 'intakeReminder'
      : 'other';

const statusKey = (status: string) =>
  status === 'Sent'
    ? 'sent'
    : status === 'Failed'
      ? 'failed'
      : status === 'LoggedOnly'
        ? 'loggedOnly'
        : 'other';

const statusClass = (status: string) =>
  status === 'Sent' ? 'status-ok' : status === 'Failed' ? 'status-error' : 'status-logged-only';

const EmailLogPage: React.FC = () => {
  const { t, i18n } = useTranslation();
  const toast = useToast();
  const [logs, setLogs] = useState<EmailLogEntry[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [hasMore, setHasMore] = useState(false);
  const [page, setPage] = useState(0);
  const [search, setSearch] = useState('');
  const [kindFilter, setKindFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [loading, setLoading] = useState(true);
  const [selectedLog, setSelectedLog] = useState<EmailLogEntry | null>(null);

  const buildParams = useCallback(
    (pageToLoad: number) => {
      const params = new URLSearchParams({ page: String(pageToLoad), limit: String(PAGE_SIZE) });
      if (search.trim()) {
        params.set('search', search.trim());
      }
      if (kindFilter) {
        params.set('kind', kindFilter);
      }
      if (statusFilter) {
        params.set('status', statusFilter);
      }
      return params;
    },
    [search, kindFilter, statusFilter]
  );

  const loadLogs = useCallback(
    async (pageToLoad: number) => {
      setLoading(true);
      try {
        const response = await apiClient.get<EmailLogListResponse>(
          `/api/emaillogs?${buildParams(pageToLoad).toString()}`
        );
        setLogs(response.logs);
        setTotalCount(response.totalCount);
        setHasMore(response.hasMore);
      } catch (error) {
        console.error('Error loading e-mail log', error);
        toast.error(t('emailLog.loadFailed'));
      } finally {
        setLoading(false);
      }
    },
    [buildParams, t, toast]
  );

  useEffect(() => {
    void loadLogs(page);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, kindFilter, statusFilter]);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setPage(0);
    void loadLogs(0);
  };

  const handleExport = async () => {
    try {
      const token = getAuthToken();
      const response = await fetch(`/api/emaillogs/export?${buildParams(0).toString()}`, {
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
        response.headers.get('Content-Disposition')?.split('filename=')[1] ?? 'email-log.csv';
      link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      console.error('Export failed', error);
      toast.error(t('emailLog.exportFailed'));
    }
  };

  const formatTime = (timestamp: string) => {
    const value = timestamp.endsWith('Z') ? timestamp : `${timestamp}Z`;
    return new Date(value).toLocaleString(i18n.language, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    });
  };

  return (
    <div className="activity-log-page">
      <h1 className="page-title">{t('emailLog.title')}</h1>

      <div className="activity-log-content">
        <form className="activity-log-filters" onSubmit={handleSearch}>
          <input
            type="text"
            placeholder={t('emailLog.searchPlaceholder')}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <select
            aria-label={t('emailLog.kindFilter')}
            value={kindFilter}
            onChange={(e) => {
              setKindFilter(e.target.value);
              setPage(0);
            }}
          >
            <option value="">
              {t('emailLog.kindFilter')}: {t('emailLog.all')}
            </option>
            {KINDS.map((kind) => (
              <option key={kind} value={kind}>
                {t(`emailLog.kinds.${kindKey(kind)}`)}
              </option>
            ))}
          </select>
          <select
            aria-label={t('emailLog.statusFilter')}
            value={statusFilter}
            onChange={(e) => {
              setStatusFilter(e.target.value);
              setPage(0);
            }}
          >
            <option value="">
              {t('emailLog.statusFilter')}: {t('emailLog.all')}
            </option>
            {STATUSES.map((status) => (
              <option key={status} value={status}>
                {t(`emailLog.statuses.${statusKey(status)}`)}
              </option>
            ))}
          </select>
          <button className="btn btn-primary" type="submit" disabled={loading}>
            {t('common.search')}
          </button>
          <button className="btn btn-secondary" type="button" onClick={handleExport}>
            {t('emailLog.exportCsv')}
          </button>
        </form>

        <div className="activity-log-meta">
          {loading ? t('common.loading') : t('emailLog.totalEntries', { count: totalCount })}
        </div>

        <DataTable
          columns={[
            {
              key: 'time',
              header: t('emailLog.time'),
              render: (log) => formatTime(log.createdAtUtc),
              width: '18%'
            },
            {
              key: 'recipient',
              header: t('emailLog.recipient'),
              render: (log) => (
                <span>
                  <strong>{log.recipientName || log.toAddress}</strong>
                  {log.recipientName ? (
                    <span className="email-log-address"> {log.toAddress}</span>
                  ) : null}
                </span>
              )
            },
            {
              key: 'subject',
              header: t('emailLog.subject'),
              render: (log) => <span className="email-log-subject">{log.subject}</span>
            },
            {
              key: 'kind',
              header: t('emailLog.kind'),
              render: (log) => t(`emailLog.kinds.${kindKey(log.kind)}`),
              width: '12%'
            },
            {
              key: 'status',
              header: t('emailLog.status'),
              render: (log) => (
                <span className={statusClass(log.status)}>
                  {t(`emailLog.statuses.${statusKey(log.status)}`)}
                </span>
              ),
              align: 'center',
              width: '10%'
            }
          ]}
          data={logs}
          rowKey={(log) => log.id}
          onRowClick={(log) => setSelectedLog(log)}
          loading={loading}
          emptyMessage={t('emailLog.noEntries')}
        />

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
        <BaseModal title={t('emailLog.detailTitle')} onClose={() => setSelectedLog(null)}>
          <dl className="activity-log-detail">
            <dt>{t('emailLog.time')}</dt>
            <dd>{formatTime(selectedLog.createdAtUtc)}</dd>
            <dt>{t('emailLog.recipient')}</dt>
            <dd>
              {selectedLog.recipientName || '—'}
              {selectedLog.personId != null ? ` (#${selectedLog.personId})` : ''}
              <div>{selectedLog.toAddress}</div>
            </dd>
            <dt>{t('emailLog.subject')}</dt>
            <dd>{selectedLog.subject}</dd>
            <dt>{t('emailLog.kind')}</dt>
            <dd>{t(`emailLog.kinds.${kindKey(selectedLog.kind)}`)}</dd>
            <dt>{t('emailLog.status')}</dt>
            <dd className={statusClass(selectedLog.status)}>
              {t(`emailLog.statuses.${statusKey(selectedLog.status)}`)}
            </dd>
            {selectedLog.reservationSlotId != null && (
              <>
                <dt>{t('emailLog.slot')}</dt>
                <dd>#{selectedLog.reservationSlotId}</dd>
              </>
            )}
            <dt>{t('emailLog.error')}</dt>
            <dd className="detail-value-multiline">{selectedLog.error || '—'}</dd>
          </dl>
        </BaseModal>
      )}
    </div>
  );
};

export default EmailLogPage;
