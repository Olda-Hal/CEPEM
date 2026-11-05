import React from 'react';
import { useTranslation } from 'react-i18next';
import { MdOpenInNew } from 'react-icons/md';
import './MonitoringPage.css';

/**
 * Statistics page: embeds the provisioned Grafana dashboard (traffic by
 * endpoint, status codes, latency, source IPs) served by the observability
 * stack (prometheus + grafana services).
 */
// VITE_GRAFANA_URL is baked at build time. In production it defaults to the
// same-origin /grafana path proxied by nginx, so a missing build arg no longer
// points users at their own localhost.
const GRAFANA_BASE_URL =
  (import.meta.env.VITE_GRAFANA_URL as string | undefined) ||
  (import.meta.env.DEV ? 'http://localhost:3001' : '/grafana');

const DASHBOARD_URL = `${GRAFANA_BASE_URL}/d/cepem-http?kiosk`;

const MonitoringPage: React.FC = () => {
  const { t } = useTranslation();

  return (
    <div className="monitoring-page">
      <div className="page-heading">
        <h1 className="page-title">{t('adminPage.monitoringTitle')}</h1>
        <div className="page-heading-actions">
          <a className="btn btn-secondary" href={DASHBOARD_URL} target="_blank" rel="noreferrer">
            <MdOpenInNew size={16} />
            {t('adminPage.openInGrafana')}
          </a>
        </div>
      </div>
      <iframe className="monitoring-grafana-frame" src={DASHBOARD_URL} title="Grafana" />
    </div>
  );
};

export default MonitoringPage;
