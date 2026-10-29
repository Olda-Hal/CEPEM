import React from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../contexts/AuthContext';
import { isAdmin } from '../utils/roles';
import './AdministrationPage.css';

interface AdminMenuItem {
  key: string;
  to: string;
  icon: string;
  titleKey: string;
  descriptionKey: string;
  allowed: boolean;
}

/**
 * System-level admin area: audit log, test dashboard, role management and
 * service health. Tiles are filtered by what the signed-in user may open.
 */
const AdminPage: React.FC = () => {
  const { t } = useTranslation();
  const { user } = useAuth();

  const sysAdminOnly = isAdmin(user);

  const items: AdminMenuItem[] = [
    {
      key: 'audit-log',
      to: '/admin/activity-log',
      icon: '📋',
      titleKey: 'adminPage.auditLogTitle',
      descriptionKey: 'adminPage.auditLogDescription',
      allowed: sysAdminOnly
    },
    {
      key: 'tests',
      to: '/tests',
      icon: '🧪',
      titleKey: 'adminPage.testsTitle',
      descriptionKey: 'adminPage.testsDescription',
      allowed: sysAdminOnly
    },
    {
      key: 'roles',
      to: '/admin/roles',
      icon: '🔐',
      titleKey: 'adminPage.rolesTitle',
      descriptionKey: 'adminPage.rolesDescription',
      allowed: sysAdminOnly
    },
    {
      key: 'health',
      to: '/admin/health',
      icon: '🩺',
      titleKey: 'adminPage.healthTitle',
      descriptionKey: 'adminPage.healthDescription',
      allowed: sysAdminOnly
    }
  ].filter((item) => item.allowed);

  return (
    <div className="administration-page">
      <h1 className="page-title">{t('adminPage.title')}</h1>
      <p className="page-lead">{t('adminPage.lead')}</p>

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
        <p className="page-lead">{t('adminPage.noAccess')}</p>
      )}
    </div>
  );
};

export default AdminPage;
