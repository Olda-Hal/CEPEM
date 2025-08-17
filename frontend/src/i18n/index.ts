import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import Backend from 'i18next-http-backend';

// Import JSON translations for better i18next-scanner support
import enTranslations from '../locales/en.json';
import csTranslations from '../locales/cs.json';

const resources = {
  en: {
    translation: enTranslations
  },
  cs: {
    translation: csTranslations
  }
};

i18n
  // Load translations using http backend
  .use(Backend)
  // Detect user language
  .use(LanguageDetector)
  // Pass the i18n instance to react-i18next
  .use(initReactI18next)
  // Initialize i18next
  .init({
    resources,
    fallbackLng: 'cs',
    defaultNS: 'translation',
    
    // Language detection options
    detection: {
      order: ['localStorage', 'navigator', 'htmlTag'],
      lookupLocalStorage: 'preferred-language',
      caches: ['localStorage'],
    },

    interpolation: {
      escapeValue: false, // React already escapes by default
    },

    // Pluralization options
    pluralSeparator: '_',
    contextSeparator: '_',
    
    // Development options
    debug: process.env.NODE_ENV === 'development',
    
    // Namespace and key separator
    keySeparator: '.',
    nsSeparator: ':',
    
    // Enable key extraction in development
    saveMissing: process.env.NODE_ENV === 'development',
    saveMissingTo: 'current',
    missingKeyHandler: (lng, ns, key, fallbackValue) => {
      if (process.env.NODE_ENV === 'development') {
        console.warn(`Missing translation key: ${key} for language: ${lng}`);
      }
    },
  });

export default i18n;
