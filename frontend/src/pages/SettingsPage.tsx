import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { PasswordChangeModal } from '../components/PasswordChangeModal';
import './SettingsPage.css';

interface SettingsSection {
  key: string;
  icon: string;
  titleKey: string;
  descriptionKey: string;
  action: () => void;
}

/**
 * Personal settings, expandable: sections added here appear as settings cards.
 * For now, only the password change lives here.
 */
const SettingsPage: React.FC = () => {
  const { t } = useTranslation();
  const [showPasswordModal, setShowPasswordModal] = useState(false);

  const sections: SettingsSection[] = [
    {
      key: 'password',
      icon: '🔑',
      titleKey: 'settings.changePasswordTitle',
      descriptionKey: 'settings.changePasswordDescription',
      action: () => setShowPasswordModal(true)
    }
  ];

  return (
    <div className="settings-page">
      <h1 className="page-title">{t('settings.title')}</h1>
      <p className="page-lead">{t('settings.lead')}</p>

      <div className="settings-grid">
        {sections.map((section) => (
          <button key={section.key} className="settings-card" onClick={section.action}>
            <span className="settings-card-icon">{section.icon}</span>
            <span className="settings-card-text">
              <strong>{t(section.titleKey)}</strong>
              <small>{t(section.descriptionKey)}</small>
            </span>
          </button>
        ))}
      </div>

      {showPasswordModal && (
        <PasswordChangeModal isOpen onClose={() => setShowPasswordModal(false)} />
      )}
    </div>
  );
};

export default SettingsPage;
