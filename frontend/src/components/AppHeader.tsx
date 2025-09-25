import React from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import './AppHeader.css';

interface AppHeaderProps {
  sectionTitle?: string;
  children?: React.ReactNode;
}

export const AppHeader: React.FC<AppHeaderProps> = ({ sectionTitle, children }) => {
  const navigate = useNavigate();
  const { t } = useTranslation();

  const handleLogoClick = () => {
    navigate('/dashboard');
  };

  return (
    <header className="app-header">
      <div className="app-header-content">
        <div className="header-left" onClick={handleLogoClick}>
          <div className="app-logo">
            <svg viewBox="0 0 24 24" className="logo-icon">
              <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/>
            </svg>
          </div>
          <div className="app-title">
            <h1 className="app-name">{t('dashboard.title')}</h1>
            <p className="app-subtitle">{t('dashboard.subtitle')}</p>
          </div>
        </div>
        {sectionTitle && (
          <div className="section-title">
            <h2>{sectionTitle}</h2>
          </div>
        )}
        {children && (
          <div className="header-actions">
            {children}
          </div>
        )}
      </div>
    </header>
  );
};
