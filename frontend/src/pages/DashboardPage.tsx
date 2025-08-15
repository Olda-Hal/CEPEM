import React, { useState, useEffect } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { LanguageSwitcher } from '../components/LanguageSwitcher';
import { DashboardStats } from '../types';
import { apiClient } from '../utils/api';
import './DashboardPage.css';

export const DashboardPage: React.FC = () => {
  const { user, logout } = useAuth();
  const { t, i18n } = useTranslation();
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadDashboardStats();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const loadDashboardStats = async () => {
    try {
      const dashboardStats = await apiClient.get<DashboardStats>('/api/doctors/dashboard-stats');
      setStats(dashboardStats);
    } catch (error) {
      console.error(t('errors.loadingStats'), error);
    } finally {
      setLoading(false);
    }
  };

  const formatDate = (dateString: string | undefined) => {
    if (!dateString) return t('dashboard.never');
    const locale = i18n.language === 'cs' ? 'cs-CZ' : 'en-US';
    return new Date(dateString).toLocaleString(locale);
  };

  const getCurrentTime = () => {
    const locale = i18n.language === 'cs' ? 'cs-CZ' : 'en-US';
    return new Date().toLocaleString(locale);
  };

  return (
    <div className="dashboard-container">
      <header className="dashboard-header">
        <div className="header-content">
          <div className="header-left">
            <h1>{t('dashboard.title')}</h1>
            <span className="subtitle">{t('dashboard.subtitle')}</span>
          </div>
          <div className="header-right">
            <LanguageSwitcher />
            <span className="welcome-text">
              {t('dashboard.welcome', { 
                firstName: user?.firstName || '', 
                lastName: user?.lastName || '' 
              })}
            </span>
            <button onClick={logout} className="logout-button">
              {t('dashboard.logout')}
            </button>
          </div>
        </div>
      </header>

      <main className="dashboard-main">
        <div className="dashboard-content">
          <div className="welcome-section">
            <h2>{t('dashboard.systemOverview')}</h2>
            <p>{t('dashboard.welcomeMessage')}</p>
          </div>

          <div className="stats-grid">
            <div className="stat-card">
              <div className="stat-icon">👨‍⚕️</div>
              <div className="stat-content">
                <h3>{t('dashboard.myProfile')}</h3>
                <p className="stat-value">{user?.firstName} {user?.lastName}</p>
                <p className="stat-label">
                  {t('dashboard.specialization', { specialization: user?.specialization || '' })}
                </p>
                <p className="stat-label">
                  {t('dashboard.email', { email: user?.email || '' })}
                </p>
              </div>
            </div>

            <div className="stat-card">
              <div className="stat-icon">📊</div>
              <div className="stat-content">
                <h3>{t('dashboard.systemStats')}</h3>
                {loading ? (
                  <p className="stat-value">{t('dashboard.loading')}</p>
                ) : (
                  <>
                    <p className="stat-value">
                      {t('dashboard.totalDoctors', { count: stats?.totalDoctors || 0 })}
                    </p>
                    <p className="stat-label">{t('dashboard.totalInSystem')}</p>
                    <p className="stat-label">
                      {t('dashboard.status', { status: stats?.systemStatus || '' })}
                    </p>
                  </>
                )}
              </div>
            </div>

            <div className="stat-card">
              <div className="stat-icon">🕐</div>
              <div className="stat-content">
                <h3>{t('dashboard.timeAndLogin')}</h3>
                <p className="stat-value">{getCurrentTime()}</p>
                <p className="stat-label">{t('dashboard.currentTime')}</p>
                <p className="stat-label">
                  {t('dashboard.lastLogin', { time: formatDate(stats?.lastLogin) })}
                </p>
              </div>
            </div>

            <div className="stat-card">
              <div className="stat-icon">🏥</div>
              <div className="stat-content">
                <h3>{t('dashboard.systemInfo')}</h3>
                <p className="stat-value">{t('dashboard.systemVersion')}</p>
                <p className="stat-label">{t('dashboard.version')}</p>
                <p className="stat-label">
                  {t('dashboard.license', { licenseNumber: user?.licenseNumber || '' })}
                </p>
              </div>
            </div>
          </div>

          <div className="actions-section">
            <h3>{t('dashboard.quickActions')}</h3>
            <div className="actions-grid">
              <div className="action-card">
                <h4>{t('dashboard.patients')}</h4>
                <p>{t('dashboard.patientsDesc')}</p>
                <button className="action-button" disabled>
                  {t('dashboard.comingSoon')}
                </button>
              </div>
              
              <div className="action-card">
                <h4>{t('dashboard.appointments')}</h4>
                <p>{t('dashboard.appointmentsDesc')}</p>
                <button className="action-button" disabled>
                  {t('dashboard.comingSoon')}
                </button>
              </div>
              
              <div className="action-card">
                <h4>{t('dashboard.reports')}</h4>
                <p>{t('dashboard.reportsDesc')}</p>
                <button className="action-button" disabled>
                  {t('dashboard.comingSoon')}
                </button>
              </div>
              
              <div className="action-card">
                <h4>{t('testDashboard.title')}</h4>
                <p>{t('testDashboard.description')}</p>
                <Link to="/tests">
                  <button className="action-button">
                    View Tests
                  </button>
                </Link>
              </div>
              
              <div className="action-card">
                <h4>{t('dashboard.settings')}</h4>
                <p>{t('dashboard.settingsDesc')}</p>
                <button className="action-button" disabled>
                  {t('dashboard.comingSoon')}
                </button>
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
};
