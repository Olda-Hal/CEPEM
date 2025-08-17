import React, { useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { useTranslation } from 'react-i18next';
import './PasswordChangeModal.css';

interface PasswordChangeModalProps {
  isOpen: boolean;
  onClose?: () => void;
  isForced?: boolean;
}

export const PasswordChangeModal: React.FC<PasswordChangeModalProps> = ({ 
  isOpen, 
  onClose,
  isForced = false 
}) => {
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  
  const { changePassword } = useAuth();
  const { t } = useTranslation();

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    // Validation
    if (newPassword.length < 6) {
      setError(t('passwordChange.passwordTooShort'));
      setLoading(false);
      return;
    }

    if (newPassword !== confirmPassword) {
      setError(t('passwordChange.passwordsDoNotMatch'));
      setLoading(false);
      return;
    }

    if (currentPassword === newPassword) {
      setError(t('passwordChange.samePassword'));
      setLoading(false);
      return;
    }

    const result = await changePassword(currentPassword, newPassword, confirmPassword);
    
    if (result.success) {
      // Reset form
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      
      if (onClose && !isForced) {
        onClose();
      }
    } else {
      // Check for specific error messages
      if (result.error?.includes('stejné jako současné') || result.error?.includes('same as current')) {
        setError(t('passwordChange.samePassword'));
      } else if (result.error?.includes('nesprávné') || result.error?.includes('incorrect')) {
        setError(t('passwordChange.currentPasswordIncorrect'));
      } else {
        setError(result.error || t('passwordChange.currentPasswordIncorrect'));
      }
    }
    
    setLoading(false);
  };

  const handleClose = () => {
    if (!isForced && onClose) {
      onClose();
    }
  };

  return (
    <div className="password-modal-overlay">
      <div className="password-modal">
        <div className="password-modal-header">
          <h2>
            {isForced ? t('passwordChange.requiredTitle') : t('passwordChange.title')}
          </h2>
          {!isForced && (
            <button 
              className="password-modal-close"
              onClick={handleClose}
              type="button"
            >
              ×
            </button>
          )}
        </div>
        
        <div className="password-modal-body">
          {isForced && (
            <div className="password-modal-warning">
              <p>{t('passwordChange.expiredMessage')}</p>
            </div>
          )}
          
          <form onSubmit={handleSubmit} className="password-change-form">
            <div className="form-group">
              <label htmlFor="currentPassword">{t('passwordChange.currentPassword')}</label>
              <input
                type="password"
                id="currentPassword"
                value={currentPassword}
                onChange={(e) => setCurrentPassword(e.target.value)}
                required
                disabled={loading}
                placeholder={t('passwordChange.currentPasswordPlaceholder')}
              />
            </div>
            
            <div className="form-group">
              <label htmlFor="newPassword">{t('passwordChange.newPassword')}</label>
              <input
                type="password"
                id="newPassword"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                required
                disabled={loading}
                placeholder={t('passwordChange.newPasswordPlaceholder')}
              />
            </div>
            
            <div className="form-group">
              <label htmlFor="confirmPassword">{t('passwordChange.confirmPassword')}</label>
              <input
                type="password"
                id="confirmPassword"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                required
                disabled={loading}
                placeholder={t('passwordChange.confirmPasswordPlaceholder')}
              />
            </div>
            
            {error && <div className="error-message">{error}</div>}
            
            <div className="password-modal-actions">
              <button 
                type="submit" 
                className="password-change-button"
                disabled={loading}
              >
                {loading ? t('passwordChange.changing') : t('passwordChange.changeButton')}
              </button>
              {!isForced && (
                <button 
                  type="button" 
                  className="password-cancel-button"
                  onClick={handleClose}
                  disabled={loading}
                >
                  {t('passwordChange.cancel')}
                </button>
              )}
            </div>
          </form>
        </div>
      </div>
    </div>
  );
};
