import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { MdRefresh } from 'react-icons/md';
import { DataTable } from './ui/DataTable';
import { apiClient, ApiError } from '../utils/api';
import { useToast } from '../contexts/ToastContext';
import './SmsLogTable.css';

export interface SmsLogRow {
  id: number;
  createdAtUtc: string;
  toNumber: string;
  templateCode: string;
  status: string;
  error: string | null;
  recipientName: string | null;
  reservationSlotId: number | null;
}

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

const statusClass = (status: string) =>
  status === 'Failed'
    ? 'status-error'
    : status === 'LoggedOnly'
      ? 'status-logged-only'
      : 'status-ok';

/** SMS delivery log shared by the unified communication log page. */
export const SmsLogTable: React.FC = () => {
  const { t, i18n } = useTranslation();
  const toast = useToast();
  const [rows, setRows] = useState<SmsLogRow[]>([]);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await apiClient.get<SmsLogRow[]>(
        '/api/communicationtemplates/sms-log?limit=100'
      );
      setRows(Array.isArray(result) ? result : []);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('emailLog.smsLoadFailed'));
    } finally {
      setLoading(false);
    }
  }, [t, toast]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <div className="sms-log-table">
      <div className="sms-log-header">
        <div>
          <h2>{t('emailLog.smsTitle')}</h2>
          <p>{t('emailLog.smsLead')}</p>
        </div>
        <button
          className="btn btn-secondary"
          type="button"
          onClick={() => void load()}
          disabled={loading}
        >
          <MdRefresh /> {t('common.refresh')}
        </button>
      </div>

      <DataTable
        columns={[
          {
            key: 'time',
            header: t('emailLog.time'),
            render: (row) => formatTime(row.createdAtUtc, i18n.language),
            width: '18%'
          },
          {
            key: 'recipient',
            header: t('emailLog.recipient'),
            render: (row) => (
              <div>
                <strong>{row.recipientName || row.toNumber}</strong>
                {row.recipientName && <div className="sms-log-muted">{row.toNumber}</div>}
              </div>
            )
          },
          {
            key: 'template',
            header: t('emailLog.template'),
            render: (row) =>
              t(`communicationTemplates.codes.${row.templateCode}`, row.templateCode),
            width: '20%'
          },
          {
            key: 'status',
            header: t('emailLog.status'),
            render: (row) => <span className={statusClass(row.status)}>{row.status}</span>,
            align: 'center',
            width: '10%'
          },
          {
            key: 'error',
            header: t('emailLog.error'),
            render: (row) => <span className="sms-log-muted">{row.error || '—'}</span>
          }
        ]}
        data={rows}
        rowKey={(row) => row.id}
        loading={loading}
        emptyMessage={t('emailLog.smsNoEntries')}
      />
    </div>
  );
};

export default SmsLogTable;
