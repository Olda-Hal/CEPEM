import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { MdBarChart, MdRefresh } from 'react-icons/md';
import { apiClient, ApiError } from '../utils/api';
import { useAuth } from '../contexts/AuthContext';
import { isAdmin } from '../utils/roles';
import { Hospital } from '../types';
import './AnalyticsPage.css';

interface AnalyticsSlots {
  total: number;
  available: number;
  blocked: number;
  reserved: number;
  unavailable: number;
  booked: number;
  utilizationPercent: number;
}

interface AnalyticsVisits {
  total: number;
  withIntake: number;
  withoutIntake: number;
  intakeCompletionPercent: number;
}

interface AnalyticsEvaluations {
  total: number;
  unreviewed: number;
  partiallyReviewed: number;
  completed: number;
  averageHoursToFirstReview?: number | null;
}

interface AnalyticsDay {
  date: string;
  slotsBooked: number;
  visits: number;
  intakeCompleted: number;
  evaluationsCompleted: number;
}

interface AnalyticsHospital {
  hospitalId: number;
  hospitalName: string;
  slots: number;
  booked: number;
  visits: number;
  intakeCompleted: number;
  evaluations: number;
}

interface AnalyticsDoctor {
  doctorId?: number | null;
  doctorName: string;
  bookedSlots: number;
  evaluationsCompleted: number;
}

interface AnalyticsSummary {
  fromUtc: string;
  toUtc: string;
  slots: AnalyticsSlots;
  visits: AnalyticsVisits;
  noShowProxy: number;
  evaluations: AnalyticsEvaluations;
  daily: AnalyticsDay[];
  byHospital: AnalyticsHospital[];
  byDoctor: AnalyticsDoctor[];
}

const dateInput = (date: Date) => date.toISOString().slice(0, 10);
const today = new Date();
const defaultFrom = dateInput(new Date(today.getTime() - 29 * 24 * 60 * 60 * 1000));
const defaultTo = dateInput(today);

const AnalyticsPage: React.FC = () => {
  const { t, i18n } = useTranslation();
  const { user } = useAuth();
  const admin = isAdmin(user);

  const [from, setFrom] = useState(defaultFrom);
  const [to, setTo] = useState(defaultTo);
  const [hospitalId, setHospitalId] = useState('');
  const [hospitals, setHospitals] = useState<Hospital[]>([]);
  const [summary, setSummary] = useState<AnalyticsSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [refreshToken, setRefreshToken] = useState(0);

  useEffect(() => {
    if (!admin) {
      return;
    }
    void apiClient
      .get<Hospital[]>('/api/hospitals')
      .then(setHospitals)
      .catch((err) => setError(err instanceof ApiError ? err.message : t('analytics.loadFailed')));
  }, [admin, t]);

  useEffect(() => {
    if (!admin) {
      return;
    }
    let cancelled = false;
    setLoading(true);
    setError(null);
    const params = new URLSearchParams({ from, to });
    if (hospitalId) {
      params.set('hospitalId', hospitalId);
    }

    apiClient
      .get<AnalyticsSummary>(`/api/analytics/summary?${params.toString()}`)
      .then((data) => {
        if (!cancelled) {
          setSummary(data);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setSummary(null);
          setError(err instanceof ApiError ? err.message : t('analytics.loadFailed'));
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [admin, from, to, hospitalId, refreshToken, t]);

  const chartMax = useMemo(() => {
    if (!summary) {
      return 1;
    }
    return Math.max(
      1,
      ...summary.daily.map((day) => Math.max(day.slotsBooked, day.visits, day.evaluationsCompleted))
    );
  }, [summary]);

  const formatDay = (value: string) =>
    new Date(value).toLocaleDateString(i18n.language, { day: '2-digit', month: '2-digit' });

  const formatNumber = (value: number | null | undefined, suffix = '') =>
    value === null || value === undefined ? '—' : `${value}${suffix}`;

  if (!admin) {
    return (
      <div className="analytics-page">
        <h1 className="page-title">{t('analytics.title')}</h1>
        <p>{t('adminPage.noAccess')}</p>
      </div>
    );
  }

  const backlog = summary
    ? summary.evaluations.unreviewed + summary.evaluations.partiallyReviewed
    : 0;

  return (
    <div className="analytics-page">
      <div className="analytics-heading">
        <div>
          <h1 className="page-title">
            <MdBarChart /> {t('analytics.title')}
          </h1>
          <p className="page-lead">{t('analytics.lead')}</p>
        </div>
        <button
          type="button"
          className="btn btn-secondary"
          onClick={() => setRefreshToken((value) => value + 1)}
          disabled={loading}
        >
          <MdRefresh /> {t('analytics.refresh')}
        </button>
      </div>

      <div className="analytics-filters">
        <label>
          <span>{t('analytics.from')}</span>
          <input type="date" value={from} onChange={(event) => setFrom(event.target.value)} />
        </label>
        <label>
          <span>{t('analytics.to')}</span>
          <input type="date" value={to} onChange={(event) => setTo(event.target.value)} />
        </label>
        <label>
          <span>{t('analytics.hospital')}</span>
          <select value={hospitalId} onChange={(event) => setHospitalId(event.target.value)}>
            <option value="">{t('analytics.allHospitals')}</option>
            {hospitals.map((hospital) => (
              <option key={hospital.id} value={hospital.id}>
                {hospital.name}
              </option>
            ))}
          </select>
        </label>
      </div>

      {error && <div className="alert alert-error">{error}</div>}
      {loading && !summary && <div className="analytics-state">{t('common.loading')}</div>}

      {summary && (
        <>
          <section className="analytics-kpis">
            <article className="analytics-kpi">
              <span>{t('analytics.utilization')}</span>
              <strong>{formatNumber(summary.slots.utilizationPercent, ' %')}</strong>
              <small>
                {t('analytics.utilizationDetail', {
                  booked: summary.slots.booked,
                  total: summary.slots.total
                })}
              </small>
            </article>
            <article className="analytics-kpi">
              <span>{t('analytics.visits')}</span>
              <strong>{summary.visits.total}</strong>
              <small>
                {t('analytics.intakeDetail', {
                  percent: summary.visits.intakeCompletionPercent,
                  missing: summary.visits.withoutIntake
                })}
              </small>
            </article>
            <article className="analytics-kpi warning">
              <span>{t('analytics.noShow')}</span>
              <strong>{summary.noShowProxy}</strong>
              <small>{t('analytics.noShowDetail')}</small>
            </article>
            <article className="analytics-kpi">
              <span>{t('analytics.backlog')}</span>
              <strong>{backlog}</strong>
              <small>
                {t('analytics.backlogDetail', {
                  unreviewed: summary.evaluations.unreviewed,
                  partial: summary.evaluations.partiallyReviewed
                })}
              </small>
            </article>
            <article className="analytics-kpi">
              <span>{t('analytics.timeToReport')}</span>
              <strong>{formatNumber(summary.evaluations.averageHoursToFirstReview, ' h')}</strong>
              <small>{t('analytics.timeToReportDetail')}</small>
            </article>
          </section>

          <section className="analytics-card">
            <h2>{t('analytics.dailyTitle')}</h2>
            <div className="analytics-legend">
              <span className="legend-booked">{t('analytics.slotsBooked')}</span>
              <span className="legend-visits">{t('analytics.visits')}</span>
              <span className="legend-evaluations">{t('analytics.evaluations')}</span>
            </div>
            <div className="analytics-chart" role="img" aria-label={t('analytics.dailyTitle')}>
              {summary.daily.map((day) => (
                <div
                  key={day.date}
                  className="analytics-chart-day"
                  title={`${formatDay(day.date)}`}
                >
                  <div className="analytics-chart-bars">
                    <span
                      className="bar booked"
                      style={{ height: `${(day.slotsBooked / chartMax) * 100}%` }}
                      title={`${t('analytics.slotsBooked')}: ${day.slotsBooked}`}
                    />
                    <span
                      className="bar visits"
                      style={{ height: `${(day.visits / chartMax) * 100}%` }}
                      title={`${t('analytics.visits')}: ${day.visits}`}
                    />
                    <span
                      className="bar evaluations"
                      style={{ height: `${(day.evaluationsCompleted / chartMax) * 100}%` }}
                      title={`${t('analytics.evaluations')}: ${day.evaluationsCompleted}`}
                    />
                  </div>
                  <span className="analytics-chart-label">{formatDay(day.date)}</span>
                </div>
              ))}
            </div>
          </section>

          <section className="analytics-card">
            <h2>{t('analytics.byHospital')}</h2>
            {summary.byHospital.length === 0 ? (
              <p className="analytics-muted">{t('analytics.noData')}</p>
            ) : (
              <table className="analytics-table">
                <thead>
                  <tr>
                    <th>{t('analytics.hospital')}</th>
                    <th>{t('analytics.slots')}</th>
                    <th>{t('analytics.slotsBooked')}</th>
                    <th>{t('analytics.visits')}</th>
                    <th>{t('analytics.intakeDone')}</th>
                    <th>{t('analytics.evaluations')}</th>
                  </tr>
                </thead>
                <tbody>
                  {summary.byHospital.map((row) => (
                    <tr key={row.hospitalId}>
                      <td>{row.hospitalName}</td>
                      <td>{row.slots}</td>
                      <td>{row.booked}</td>
                      <td>{row.visits}</td>
                      <td>{row.intakeCompleted}</td>
                      <td>{row.evaluations}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>

          <section className="analytics-card">
            <h2>{t('analytics.byDoctor')}</h2>
            {summary.byDoctor.length === 0 ? (
              <p className="analytics-muted">{t('analytics.noData')}</p>
            ) : (
              <table className="analytics-table">
                <thead>
                  <tr>
                    <th>{t('analytics.doctor')}</th>
                    <th>{t('analytics.slotsBooked')}</th>
                    <th>{t('analytics.evaluationsCompleted')}</th>
                  </tr>
                </thead>
                <tbody>
                  {summary.byDoctor.map((row) => (
                    <tr key={row.doctorId ?? row.doctorName}>
                      <td>{row.doctorName}</td>
                      <td>{row.bookedSlots}</td>
                      <td>{row.evaluationsCompleted}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>
        </>
      )}
    </div>
  );
};

export default AnalyticsPage;
