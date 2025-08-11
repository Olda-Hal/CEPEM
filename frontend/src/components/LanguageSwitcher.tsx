import React from 'react';
import { useTranslation } from 'react-i18next';
import './LanguageSwitcher.css';

type SupportedLanguages = 'cs' | 'en';

export const LanguageSwitcher: React.FC = () => {
  const { i18n, t } = useTranslation();

  const languages: { code: SupportedLanguages; name: string; flag: string }[] = [
    { code: 'cs', name: t('common.czech'), flag: '🇨🇿' },
    { code: 'en', name: t('common.english'), flag: '🇺🇸' },
  ];

  const handleLanguageChange = (languageCode: SupportedLanguages) => {
    i18n.changeLanguage(languageCode);
  };

  return (
    <div className="language-switcher">
      <select
        value={i18n.language}
        onChange={(e) => handleLanguageChange(e.target.value as SupportedLanguages)}
        className="language-select"
      >
        {languages.map((lang) => (
          <option key={lang.code} value={lang.code}>
            {lang.flag} {lang.name}
          </option>
        ))}
      </select>
    </div>
  );
};
