import React, { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { apiClient, ApiError } from '../utils/api';
import './PublicGdprPage.css';

interface PublicGdprDocument {
  available: boolean;
  version?: string | null;
  language?: string | null;
  title?: string | null;
  text?: string | null;
  updatedAtUtc?: string | null;
  availableLanguages: string[];
}

const FALLBACK_LANGUAGES = ['cs', 'en', 'nl'];

/** Public, unauthenticated view of the currently published GDPR wording. */
const PublicGdprPage: React.FC = () => {
  const { t, i18n } = useTranslation();
  const [searchParams, setSearchParams] = useSearchParams();

  const requestedLanguage = (
    (searchParams.get('lang') || i18n.language || 'cs').split('-')[0] ?? 'cs'
  ).toLowerCase();
  const [language, setLanguage] = useState(
    FALLBACK_LANGUAGES.includes(requestedLanguage) ? requestedLanguage : 'cs'
  );
  const [gdprDocument, setGdprDocument] = useState<PublicGdprDocument | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadDocument = useCallback(
    async (lang: string) => {
      setLoading(true);
      setError(null);
      try {
        setGdprDocument(
          await apiClient.get<PublicGdprDocument>(
            `/api/gdpr/public/current?language=${encodeURIComponent(lang)}`
          )
        );
      } catch (err) {
        setError(err instanceof ApiError ? err.message : t('publicGdpr.loadFailed'));
        setGdprDocument(null);
      } finally {
        setLoading(false);
      }
    },
    [t]
  );

  useEffect(() => {
    void loadDocument(language);
  }, [language, loadDocument]);

  const languages = gdprDocument?.availableLanguages?.length
    ? gdprDocument.availableLanguages
    : FALLBACK_LANGUAGES;

  const changeLanguage = (lang: string) => {
    setLanguage(lang);
    setSearchParams({ lang });
  };

  return (
    <div className="public-gdpr-page">
      <div className="public-gdpr-card">
        <h1>{t('publicGdpr.title')}</h1>
        <p className="public-gdpr-intro">{t('publicGdpr.intro')}</p>

        <div
          className="public-gdpr-languages"
          role="tablist"
          aria-label={t('publicGdpr.languageLabel')}
        >
          {languages.map((lang) => (
            <button
              key={lang}
              type="button"
              role="tab"
              aria-selected={lang === language}
              className={`public-gdpr-language${lang === language ? ' active' : ''}`}
              onClick={() => changeLanguage(lang)}
            >
              {t(`publicGdpr.languages.${lang}`, { defaultValue: lang.toUpperCase() })}
            </button>
          ))}
        </div>

        {loading && <p>{t('common.loading')}</p>}
        {error && <div className="error-message">{error}</div>}

        {!loading && !error && gdprDocument && !gdprDocument.available && (
          <div className="public-gdpr-empty">{t('publicGdpr.notPublished')}</div>
        )}

        {!loading && !error && gdprDocument?.available && (
          <article className="public-gdpr-document">
            <h2>{gdprDocument.title}</h2>
            {gdprDocument.version && (
              <p className="public-gdpr-version">
                {t('publicGdpr.version', { version: gdprDocument.version })}
              </p>
            )}
            <div className="public-gdpr-text">{gdprDocument.text}</div>
          </article>
        )}

        <a className="public-gdpr-back" href="/login">
          {t('publicGdpr.backToApp')}
        </a>
      </div>
    </div>
  );
};

export default PublicGdprPage;
