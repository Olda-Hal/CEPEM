import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AppHeader } from '../components/AppHeader';
import { apiClient } from '../utils/api';
import './HealthDashboardPage.css';

type ServiceState = 'loading' | 'healthy' | 'unreachable';

interface ServiceStatus {
  name: string;
  url: string;
  state: ServiceState;
  detail?: string;
}

const PROBED_SERVICES: Array<{ name: string; url: string }> = [
  { name: 'HealthcareAPI', url: '/health' },
  { name: 'DatabaseAPI', url: '/api/health' }
];

/**
 * Simple overview of service health: polls the services' /health endpoints
 * and renders a status card per service.
 */
const HealthDashboardPage: React.FC = () => {
  const { t } = useTranslation();
  const [services, setServices] = useState<ServiceStatus[]>(
    PROBED_SERVICES.map(service => ({ ...service, state: 'loading' }))
  );

  useEffect(() => {
    let cancelled = false;

    const probeAll = async () => {
      const results = await Promise.all(
        PROBED_SERVICES.map(async (service): Promise<ServiceStatus> => {
          try {
            const response = await apiClient.get<{ status?: string; service?: string }>(service.url);
            return { ...service, state: 'healthy', detail: response.status ?? 'OK' };
          } catch {
            return { ...service, state: 'unreachable' };
          }
        })
      );
      if (!cancelled) {
        setServices(results);
      }
    };

    void probeAll();
    const interval = setInterval(probeAll, 30_000);
    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, []);

  return (
    <div className="health-dashboard-page">
      <AppHeader sectionTitle={t('health.title')} />

      <div className="services-grid">
        {services.map(service => (
          <div key={service.name} className={`service-card service-${service.state}`}>
            <h3>{service.name}</h3>
            <span className={`health-badge ${service.state}`}>
              {service.state === 'loading'
                ? t('common.loading')
                : service.state === 'healthy'
                  ? t('health.healthy')
                  : t('health.unreachable')}
            </span>
            {service.detail && <small>{service.detail}</small>}
          </div>
        ))}
      </div>
    </div>
  );
};

export default HealthDashboardPage;
