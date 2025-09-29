import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { QuickPreviewSettings } from '../types';
import './QuickPreviewSettingsModal.css';

interface QuickPreviewSettingsModalProps {
  isOpen: boolean;
  onClose: () => void;
  settings: QuickPreviewSettings;
  onSave: (settings: QuickPreviewSettings) => void;
}

const QuickPreviewSettingsModal: React.FC<QuickPreviewSettingsModalProps> = ({
  isOpen,
  onClose,
  settings,
  onSave
}) => {
  const { t } = useTranslation();
  const [localSettings, setLocalSettings] = useState<QuickPreviewSettings>(settings);

  const handleChange = (key: keyof QuickPreviewSettings) => {
    setLocalSettings(prev => ({
      ...prev,
      [key]: !prev[key]
    }));
  };

  const handleSave = () => {
    onSave(localSettings);
    onClose();
  };

  const handleCancel = () => {
    setLocalSettings(settings);
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div className="modal-overlay">
      <div className="settings-modal">
        <div className="modal-header">
          <h3>{t('patients.quickPreviewSettings')}</h3>
          <button className="close-button" onClick={handleCancel}>
            <svg viewBox="0 0 24 24" width="20" height="20">
              <path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/>
            </svg>
          </button>
        </div>
        
        <div className="modal-content">
          <p className="settings-description">
            {t('patients.quickPreviewSettingsDescription')}
          </p>
          
          <div className="settings-grid">
            <div className="setting-item">
              <label className="setting-label">
                <input
                  type="checkbox"
                  checked={localSettings.showCovidVaccination}
                  onChange={() => handleChange('showCovidVaccination')}
                />
                <span className="checkmark"></span>
                {t('patients.covidVaccination')}
              </label>
            </div>
            
            <div className="setting-item">
              <label className="setting-label">
                <input
                  type="checkbox"
                  checked={localSettings.showFluVaccination}
                  onChange={() => handleChange('showFluVaccination')}
                />
                <span className="checkmark"></span>
                {t('patients.fluVaccination')}
              </label>
            </div>
            
            <div className="setting-item">
              <label className="setting-label">
                <input
                  type="checkbox"
                  checked={localSettings.showDiabetes}
                  onChange={() => handleChange('showDiabetes')}
                />
                <span className="checkmark"></span>
                {t('patients.diabetes')}
              </label>
            </div>
            
            <div className="setting-item">
              <label className="setting-label">
                <input
                  type="checkbox"
                  checked={localSettings.showHypertension}
                  onChange={() => handleChange('showHypertension')}
                />
                <span className="checkmark"></span>
                {t('patients.hypertension')}
              </label>
            </div>
            
            <div className="setting-item">
              <label className="setting-label">
                <input
                  type="checkbox"
                  checked={localSettings.showHeartDisease}
                  onChange={() => handleChange('showHeartDisease')}
                />
                <span className="checkmark"></span>
                {t('patients.heartDisease')}
              </label>
            </div>
            
            <div className="setting-item">
              <label className="setting-label">
                <input
                  type="checkbox"
                  checked={localSettings.showAllergies}
                  onChange={() => handleChange('showAllergies')}
                />
                <span className="checkmark"></span>
                {t('patients.allergies')}
              </label>
            </div>
            
            <div className="setting-item">
              <label className="setting-label">
                <input
                  type="checkbox"
                  checked={localSettings.showRecentEvents}
                  onChange={() => handleChange('showRecentEvents')}
                />
                <span className="checkmark"></span>
                {t('patients.recentEvents')}
              </label>
            </div>
            
            <div className="setting-item">
              <label className="setting-label">
                <input
                  type="checkbox"
                  checked={localSettings.showUpcomingAppointments}
                  onChange={() => handleChange('showUpcomingAppointments')}
                />
                <span className="checkmark"></span>
                {t('patients.upcomingAppointments')}
              </label>
            </div>
            
            <div className="setting-item">
              <label className="setting-label">
                <input
                  type="checkbox"
                  checked={localSettings.showLastVisit}
                  onChange={() => handleChange('showLastVisit')}
                />
                <span className="checkmark"></span>
                {t('patients.lastVisit')}
              </label>
            </div>
          </div>
        </div>
        
        <div className="modal-footer">
          <button className="cancel-button" onClick={handleCancel}>
            {t('common.cancel')}
          </button>
          <button className="save-button" onClick={handleSave}>
            {t('common.save')}
          </button>
        </div>
      </div>
    </div>
  );
};

export default QuickPreviewSettingsModal;
