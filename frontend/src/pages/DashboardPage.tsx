import React, { useState, useEffect } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { LanguageSwitcher } from '../components/LanguageSwitcher';
import { ThemeSelector } from '../components/ThemeSelector';
import { PasswordChangeModal } from '../components/PasswordChangeModal';
import { CreateEmployeeModal } from '../components/CreateEmployeeModal';
import { AppHeader } from '../components/AppHeader';
import { DashboardStats } from '../types';
import { apiClient } from '../utils/api';
import { isAdmin } from '../utils/roles';
import './DashboardPage.css';

export const DashboardPage: React.FC = () => {
  const { user, logout } = useAuth();
  const { t, i18n } = useTranslation();
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [showPasswordModal, setShowPasswordModal] = useState(false);
  const [showCreateEmployeeModal, setShowCreateEmployeeModal] = useState(false);
  const [isOverviewExpanded, setIsOverviewExpanded] = useState(true);
  const [isActionsExpanded, setIsActionsExpanded] = useState(true);

  useEffect(() => {
    loadDashboardStats();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const loadDashboardStats = async () => {
    try {
      const dashboardStats = await apiClient.get<DashboardStats>('/api/employees/dashboard-stats');
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

  const handleEmployeeCreated = () => {
    loadDashboardStats();
  };

  return (
    <div className="dashboard-container">
      <AppHeader>
        <LanguageSwitcher />
        <ThemeSelector className="compact" />
        <span 
          className="welcome-text"
          title={t('dashboard.welcome', { 
            firstName: user?.firstName || '', 
            lastName: user?.lastName || '' 
          })}
        >
          {t('dashboard.welcome', { 
            firstName: user?.firstName || '', 
            lastName: user?.lastName || '' 
          })}
        </span>
        <button onClick={logout} className="logout-button">
          {t('dashboard.logout')}
        </button>
      </AppHeader>

      <main className="dashboard-main">
        <div className="dashboard-content">
          <div className="collapsible-section">
            <div className="section-header" onClick={() => setIsOverviewExpanded(!isOverviewExpanded)}>
              <h2>{t('dashboard.systemOverview')}</h2>
              <button className="collapse-button">
                {isOverviewExpanded ? '▼' : '▶'}
              </button>
            </div>
            {isOverviewExpanded && (
              <div className="section-content">
                <p>{t('dashboard.welcomeMessage')}</p>
                <div className="stats-grid">
                  <div className="stat-card">
                    <div className="stat-icon">👨‍⚕️</div>
                    <div className="stat-content">
                      <h3>{t('dashboard.myProfile')}</h3>
                      <p 
                        className="stat-value"
                        title={`${user?.firstName || ''} ${user?.lastName || ''}`}
                      >
                        {user?.firstName} {user?.lastName}
                      </p>
                      <p className="stat-label">
                        {t('dashboard.uid', { uid: user?.uid || '' })}
                      </p>
                      <p className="stat-label">
                        {t('dashboard.email', { email: user?.email || '' })}
                      </p>
                    </div>
                  </div>

                  {isAdmin(user) && (
                    <div className="stat-card admin-stat">
                      <div className="stat-icon">📊</div>
                      <div className="stat-content">
                        <h3>{t('dashboard.systemStats')}</h3>
                        {loading ? (
                          <p className="stat-value">{t('dashboard.loading')}</p>
                        ) : (
                          <>
                            <p className="stat-value">
                              {t('dashboard.totalEmployees', { count: stats?.totalEmployees || 0 })}
                            </p>
                            <p className="stat-label">{t('dashboard.totalInSystem')}</p>
                            <p className="stat-label">
                              {t('dashboard.status', { status: stats?.systemStatus || '' })}
                            </p>
                          </>
                        )}
                      </div>
                    </div>
                  )}

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
                        {t('dashboard.employeeId', { uid: user?.uid || '' })}
                      </p>
                    </div>
                  </div>
                </div>
              </div>
            )}
          </div>

          <div className="collapsible-section">
            <div className="section-header" onClick={() => setIsActionsExpanded(!isActionsExpanded)}>
              <h3>{t('dashboard.quickActions')}</h3>
              <button className="collapse-button">
                {isActionsExpanded ? '▼' : '▶'}
              </button>
            </div>
            {isActionsExpanded && (
              <div className="section-content">
                <div className="actions-grid">
                  <div className="action-card">
                    <h4>{t('dashboard.patients')}</h4>
                    <p>{t('dashboard.patientsDesc')}</p>
                    <Link to="/patients">
                      <button className="action-button">
                        {t('dashboard.patients')}
                      </button>
                    </Link>
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
                  
                  {isAdmin(user) && (
                    <div className="action-card admin-action">
                      <h4>{t('testDashboard.title')}</h4>
                      <p>{t('testDashboard.description')}</p>
                      <Link to="/tests">
                        <button className="action-button admin-button">
                          View Tests
                        </button>
                      </Link>
                    </div>
                  )}
                  
                  <div className="action-card">
                    <h4>{t('dashboard.settings')}</h4>
                    <p>{t('dashboard.settingsDesc')}</p>
                    <button 
                      className="action-button"
                      onClick={() => setShowPasswordModal(true)}
                    >
                      {t('passwordChange.title')}
                    </button>
                  </div>

                  {isAdmin(user) && (
                    <div className="action-card admin-action">
                      <h4>{t('createEmployee.title')}</h4>
                      <p>{t('createEmployee.description')}</p>
                      <button 
                        className="action-button admin-button"
                        onClick={() => setShowCreateEmployeeModal(true)}
                      >
                        {t('createEmployee.buttonText')}
                      </button>
                    </div>
                  )}

                  {isAdmin(user) && (
                    <div className="action-card admin-action">
                      <h4>{t('admin.employeeManagement')}</h4>
                      <p>{t('admin.employeeManagementDescription')}</p>
                      <Link to="/admin/employees">
                        <button className="action-button admin-button">
                          {t('admin.employeeManagement')}
                        </button>
                      </Link>
                    </div>
                  )}
                </div>
              </div>
            )}
          </div>
        </div>
      </main>

      <PasswordChangeModal 
        isOpen={showPasswordModal}
        onClose={() => setShowPasswordModal(false)}
        isForced={false}
      />

      <CreateEmployeeModal
        isOpen={showCreateEmployeeModal}
        onClose={() => setShowCreateEmployeeModal(false)}
        onEmployeeCreated={handleEmployeeCreated}
      />
    </div>
  );
};
