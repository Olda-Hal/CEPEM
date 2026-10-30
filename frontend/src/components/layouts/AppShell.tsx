import React, { useEffect, useState } from 'react';
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../contexts/AuthContext';
import { hasRole, isAdmin } from '../../utils/roles';
import { LanguageSwitcher } from '../../components/LanguageSwitcher';
import './AppShell.css';

const fetchBuildVersion = async (): Promise<string | null> => {
  try {
    const response = await fetch('/build_number.txt', { cache: 'no-store' });
    if (!response.ok) return null;
    return (await response.text()).trim();
  } catch {
    return null;
  }
};

interface NavSubItem {
  key: string;
  to: string;
  labelKey: string;
}

interface NavSection {
  key: string;
  to: string;
  labelKey: string;
  /** Every path that should highlight this section (sub-pages included) */
  matchPrefixes: string[];
  /** Optional hover dropdown with direct links to sub-pages */
  children?: NavSubItem[];
}

const ADMINISTRATION_CHILDREN: NavSubItem[] = [
  { key: 'centers', to: '/admin/centers', labelKey: 'administration.centersTitle' },
  { key: 'employees', to: '/admin/employees', labelKey: 'administration.employeesTitle' }
];

const ADMIN_CHILDREN: NavSubItem[] = [
  { key: 'monitoring', to: '/admin/monitoring', labelKey: 'adminPage.monitoringTitle' },
  { key: 'audit-log', to: '/admin/activity-log', labelKey: 'adminPage.auditLogTitle' },
  { key: 'tests', to: '/tests', labelKey: 'adminPage.testsTitle' },
  { key: 'roles', to: '/admin/roles', labelKey: 'adminPage.rolesTitle' },
  { key: 'health', to: '/admin/health', labelKey: 'adminPage.healthTitle' }
];

const NAV_SECTIONS: NavSection[] = [
  { key: 'patients', to: '/patients', labelKey: 'patients.title', matchPrefixes: ['/patients'] },
  {
    key: 'reservations',
    to: '/reservations',
    labelKey: 'reservationSlots.title',
    matchPrefixes: ['/reservations']
  },
  {
    key: 'examinations',
    to: '/examinations',
    labelKey: 'examinations.title',
    matchPrefixes: ['/examinations']
  },
  {
    key: 'vyhodnocovani',
    to: '/vyhodnocovani',
    labelKey: 'evaluation.title',
    matchPrefixes: ['/vyhodnocovani']
  },
  {
    key: 'administration',
    to: '/administration',
    labelKey: 'shell.administration',
    matchPrefixes: ['/administration', '/admin/centers', '/admin/employees'],
    children: ADMINISTRATION_CHILDREN
  },
  {
    key: 'admin',
    to: '/admin',
    labelKey: 'shell.admin',
    matchPrefixes: ['/admin', '/tests'],
    children: ADMIN_CHILDREN
    // "/admin" prefix stays last so the more specific administration match
    // above wins on /admin/centers and /admin/employees.
  }
];

/** Navigation items the signed-in user may actually open. */
const visibleSections = (user: ReturnType<typeof useAuth>['user']): NavSection[] =>
  NAV_SECTIONS.filter((section) => {
    if (section.key === 'administration') {
      return isAdmin(user) || hasRole(user, 'Center Admin') || hasRole(user, 'Country Admin');
    }
    if (section.key === 'admin') {
      return isAdmin(user);
    }
    if (section.key === 'vyhodnocovani') {
      // Vyhodnocování belongs to the evaluating doctor role (SysAdmin sees it too).
      return isAdmin(user) || hasRole(user, 'Doctor');
    }
    return true;
  });

/**
 * Application shell: header with brand + account info + settings gear,
 * a primary navigation row underneath, page content, and a footer with the
 * running build version. All authenticated pages render inside it.
 */
export const AppShell: React.FC = () => {
  const { t } = useTranslation();
  const { user, logout, impersonation, stopImpersonation } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [buildVersion, setBuildVersion] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    void fetchBuildVersion().then((version) => {
      if (!cancelled) setBuildVersion(version);
    });
    return () => {
      cancelled = true;
    };
  }, []);

  const handleLogout = async () => {
    await logout();
    navigate('/login');
  };

  const handleStopImpersonation = async () => {
    await stopImpersonation();
    navigate('/admin/employees');
  };

  const activeSection = [...NAV_SECTIONS]
    .sort((a, b) => {
      const aLen = Math.max(...a.matchPrefixes.map((prefix) => prefix.length));
      const bLen = Math.max(...b.matchPrefixes.map((prefix) => prefix.length));

      return bLen - aLen;
    })
    .find((section) =>
      section.matchPrefixes.some(
        (prefix) => location.pathname === prefix || location.pathname.startsWith(`${prefix}/`)
      )
    );

  return (
    <div className="app-shell">
      <header className="shell-header">
        <div className="shell-header-inner">
          <Link to="/" className="shell-brand" aria-label={t('app.defaultName')}>
            <span className="shell-brand-mark">+</span>
            <span className="shell-brand-name">CEPEM</span>
          </Link>

          <nav className="shell-nav" aria-label={t('shell.mainNavigation')}>
            {visibleSections(user).map((section) => (
              <div key={section.key} className="shell-nav-item">
                <Link
                  to={section.to}
                  className={`shell-nav-link${activeSection?.key === section.key ? ' active' : ''}`}
                >
                  {t(section.labelKey)}
                  {section.children && (
                    <span className="shell-nav-caret" aria-hidden="true">
                      ▾
                    </span>
                  )}
                </Link>
                {section.children && (
                  <div className="shell-dropdown" role="menu">
                    {section.children.map((child) => (
                      <Link
                        key={child.key}
                        to={child.to}
                        className="shell-dropdown-link"
                        role="menuitem"
                      >
                        {t(child.labelKey)}
                      </Link>
                    ))}
                  </div>
                )}
              </div>
            ))}
          </nav>

          <div className="shell-header-right">
            <LanguageSwitcher />
            <Link
              to="/settings"
              className="shell-icon-button"
              title={t('shell.settings')}
              aria-label={t('shell.settings')}
            >
              ⚙
            </Link>
            <div
              className="shell-account"
              title={`${user?.email ?? ''}${user?.roles?.length ? ` — ${user.roles.join(', ')}` : ''}`}
            >
              <span className="shell-account-name">
                {user?.titleBefore ? `${user.titleBefore} ` : ''}
                {user?.firstName} {user?.lastName}
                {user?.titleAfter ? `, ${user.titleAfter}` : ''}
              </span>
              <span className="shell-account-meta">
                {user?.roles?.length ? user.roles.join(', ') : user?.email}
              </span>
            </div>
            <button
              className="shell-icon-button"
              onClick={handleLogout}
              title={t('shell.logout')}
              aria-label={t('shell.logout')}
            >
              ⏻
            </button>
          </div>
        </div>
      </header>

      {impersonation && (
        <div className="shell-impersonation-banner" role="status">
          <span className="shell-impersonation-text">
            <strong>{t('shell.impersonationActive')}</strong>{' '}
            {t('shell.impersonationDetail', {
              name: impersonation.fullName,
              admin: impersonation.adminName || t('admin.roleNames.sysAdmin')
            })}
          </span>
          <button
            type="button"
            className="shell-impersonation-exit"
            onClick={handleStopImpersonation}
          >
            {t('shell.stopImpersonation')}
          </button>
        </div>
      )}

      <main className="shell-content">
        <Outlet />
      </main>

      <footer className="shell-footer">
        <span>© {new Date().getFullYear()} CEPEM</span>
        <span className="shell-footer-version">
          {buildVersion ? `v0.1.${buildVersion}` : t('shell.versionUnknown')}
        </span>
      </footer>
    </div>
  );
};

export default AppShell;
