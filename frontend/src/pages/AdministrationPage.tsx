import React from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../contexts/AuthContext';
import { hasRole, isAdmin } from '../utils/roles';
import './AdministrationPage.css';

interface AdministrationMenuItem {
  key: string;
  to: string;
  icon: string;
  titleKey: string;
  descriptionKey: string;
  allowed: boolean;
}

/**
 * Back-office hub: center management and employee management. Creating a new
 * employee lives on the employees screen as a button, not as a separate tile.
 * Tiles are filtered by what the signed-in user may open.
 */
const AdministrationPage: React.FC = () => {
  const { t } = useTranslation();
  const { user } = useAuth();

  const items: AdministrationMenuItem[] = [
    {
      key: 'centers',
      to: '/admin/centers',
      icon: '🏥',
      titleKey: 'administration.centersTitle',
      descriptionKey: 'administration.centersDescription',
      allowed: isAdmin(user) || hasRole(user, 'Center Admin') || hasRole(user, 'Country Admin')
    },
    {
      key: 'employees',
      to: '/admin/employees',
      icon: '👥',
      titleKey: 'administration.employeesTitle',
      descriptionKey: 'administration.employeesDescription',
      allowed: isAdmin(user) || hasRole(user, 'Country Admin')
    }
  ].filter((item) => item.allowed);

  return (
    <div className="administration-page">
      <h1 className="page-title">{t('administration.title')}</h1>
      <p className="page-lead">{t('administration.lead')}</p>

      {items.length > 0 ? (
        <div className="administration-menu">
          {items.map((item) => (
            <Link key={item.key} to={item.to} className="administration-menu-item">
              <span className="administration-menu-icon">{item.icon}</span>
              <span className="administration-menu-text">
                <strong>{t(item.titleKey)}</strong>
                <small>{t(item.descriptionKey)}</small>
              </span>
              <span className="administration-menu-chevron">›</span>
            </Link>
          ))}
        </div>
      ) : (
        <p className="page-lead">{t('administration.noAccess')}</p>
      )}
    </div>
  );
};

export default AdministrationPage;
