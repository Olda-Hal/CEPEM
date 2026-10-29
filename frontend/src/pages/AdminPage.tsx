import React from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  MdAdminPanelSettings,
  MdAssignment,
  MdMonitorHeart,
  MdQueryStats,
  MdScience
} from 'react-icons/md';
import { useAuth } from '../contexts/AuthContext';
import { isAdmin } from '../utils/roles';
import './AdministrationPage.css';

interface AdminMenuItem {
  key: string;
  to: string;
  icon: React.ReactNode;
  titleKey: string;
  descriptionKey: string;
  allowed: boolean;
}

/**
 * System-level admin area: audit log, monitoring, test dashboard, role
 * management and service health. Tiles are filtered by what the signed-in
 * user may open.
 */
const AdminPage: React.FC = () => {
  const { t } = useTranslation();
  const { user } = useAuth();

  const sysAdminOnly = isAdmin(user);

  const items: AdminMenuItem[] = [
    {
      key: 'monitoring',
      to: '/admin/monitoring',
      icon: <MdQueryStats size={28} />,
      titleKey: 'adminPage.monitoringTitle',
      descriptionKey: 'adminPage.monitoringDescription',
      allowed: sysAdminOnly
    },
    {
      key: 'audit-log',
      to: '/admin/activity-log',
      icon: <MdAssignment size={28} />,
      titleKey: 'adminPage.auditLogTitle',
      descriptionKey: 'adminPage.auditLogDescription',
      allowed: sysAdminOnly
    },
    {
      key: 'tests',
      to: '/tests',
      icon: <MdScience size={28} />,
      titleKey: 'adminPage.testsTitle',
      descriptionKey: 'adminPage.testsDescription',
      allowed: sysAdminOnly
    },
    {
      key: 'roles',
      to: '/admin/roles',
      icon: <MdAdminPanelSettings size={28} />,
      titleKey: 'adminPage.rolesTitle',
      descriptionKey: 'adminPage.rolesDescription',
      allowed: sysAdminOnly
    },
    {
      key: 'health',
      to: '/admin/health',
      icon: <MdMonitorHeart size={28} />,
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
