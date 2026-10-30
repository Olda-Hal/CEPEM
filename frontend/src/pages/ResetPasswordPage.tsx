import React, { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { apiClient } from '../utils/api';
import { LanguageSwitcher } from '../components/LanguageSwitcher';
import './LoginPage.css';

/**
 * Public destination of the password-reset e-mail link. The token is a
 * one-time, short-lived secret kept only in the URL (hashed server-side).
 */
export const ResetPasswordPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') ?? '';
  const { t } = useTranslation();

  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (newPassword.length < 6) {
      setError(t('resetPassword.passwordTooShort'));
      return;
    }
    if (newPassword !== confirmPassword) {
      setError(t('resetPassword.mismatch'));
      return;
    }

    setLoading(true);
    try {
      await apiClient.post('/api/auth/reset-password', { token, newPassword });
      setSuccess(true);
    } catch (err) {
      console.error('Password reset failed', err);
      setError(t('resetPassword.failed'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-container">
      <div className="login-language-switcher">
        <LanguageSwitcher />
      </div>
      <div className="login-card">
        <div className="login-header">
          <h1>{t('resetPassword.title')}</h1>
          <p>{t('resetPassword.description')}</p>
        </div>

        {!token ? (
          <div className="alert alert-error">{t('resetPassword.missingToken')}</div>
        ) : success ? (
          <>
            <div className="alert alert-success">{t('resetPassword.success')}</div>
            <p>
              <Link to="/login">{t('resetPassword.backToLogin')}</Link>
            </p>
          </>
        ) : (
          <form onSubmit={handleSubmit} className="login-form">
            <div className="form-group">
              <label htmlFor="newPassword">{t('resetPassword.newPassword')}</label>
              <input
                type="password"
                id="newPassword"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                required
                disabled={loading}
              />
            </div>
            <div className="form-group">
              <label htmlFor="confirmPassword">{t('resetPassword.confirmPassword')}</label>
              <input
                type="password"
                id="confirmPassword"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                required
                disabled={loading}
              />
            </div>

            {error && <div className="error-message">{error}</div>}

            <button type="submit" className="login-button" disabled={loading}>
              {loading ? t('resetPassword.saving') : t('resetPassword.submit')}
            </button>
          </form>
        )}
      </div>
    </div>
  );
};

export default ResetPasswordPage;
