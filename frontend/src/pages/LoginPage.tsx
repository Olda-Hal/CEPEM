import React, { useState } from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { useTranslation } from 'react-i18next';
import { LanguageSwitcher } from '../components/LanguageSwitcher';
import { getLandingPath } from '../utils/landing';
import { apiClient } from '../utils/api';
import './LoginPage.css';

export const LoginPage: React.FC = () => {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [showForgot, setShowForgot] = useState(false);
  const [forgotMessage, setForgotMessage] = useState('');
  const [forgotLoading, setForgotLoading] = useState(false);

  const { login, isAuthenticated, user } = useAuth();
  const { t } = useTranslation();

  if (isAuthenticated) {
    // Landing page may be configured per role; falls back to the app default.
    return <Navigate to={getLandingPath(user)} replace />;
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    const result = await login(email, password);

    if (!result.success) {
      setError(result.error || t('login.invalidCredentials'));
    }

    setLoading(false);
  };

  const handleForgotSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setForgotLoading(true);
    setForgotMessage('');
    setError('');
    try {
      await apiClient.post('/api/auth/forgot-password', { email });
      setForgotMessage(t('login.forgotSent'));
    } catch (err) {
      console.error('Forgot-password request failed', err);
      setError(t('login.forgotFailed'));
    } finally {
      setForgotLoading(false);
    }
  };

  return (
    <div className="login-container">
      <div className="login-language-switcher">
        <LanguageSwitcher />
      </div>
      <div className="login-card">
        <div className="login-header">
          <h1>{t('login.title')}</h1>
          <h2>{t('login.subtitle')}</h2>
          <p>{t('login.description')}</p>
        </div>

        {showForgot ? (
          <form onSubmit={handleForgotSubmit} className="login-form">
            <div className="form-group">
              <label htmlFor="email">{t('login.email')}</label>
              <input
                type="email"
                id="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                disabled={forgotLoading}
                placeholder={t('login.emailPlaceholder')}
              />
            </div>

            {forgotMessage && <div className="alert alert-success">{forgotMessage}</div>}
            {error && <div className="error-message">{error}</div>}

            <button type="submit" className="login-button" disabled={forgotLoading}>
              {forgotLoading ? t('login.forgotSending') : t('login.forgotSubmit')}
            </button>
            <button
              type="button"
              className="login-link-button"
              onClick={() => {
                setShowForgot(false);
                setForgotMessage('');
                setError('');
              }}
            >
              {t('login.backToLogin')}
            </button>
          </form>
        ) : (
          <form onSubmit={handleSubmit} className="login-form">
            <div className="form-group">
              <label htmlFor="email">{t('login.email')}</label>
              <input
                type="email"
                id="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                disabled={loading}
                placeholder={t('login.emailPlaceholder')}
              />
            </div>

            <div className="form-group">
              <label htmlFor="password">{t('login.password')}</label>
              <input
                type="password"
                id="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                disabled={loading}
                placeholder={t('login.passwordPlaceholder')}
              />
            </div>

            {error && <div className="error-message">{error}</div>}

            <button type="submit" className="login-button" disabled={loading}>
              {loading ? t('login.loggingIn') : t('login.loginButton')}
            </button>
            <button type="button" className="login-link-button" onClick={() => setShowForgot(true)}>
              {t('login.forgotPassword')}
            </button>
          </form>
        )}
      </div>
    </div>
  );
};
