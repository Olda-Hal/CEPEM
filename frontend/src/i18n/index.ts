import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';

// Import JSON translations for better i18next-scanner support
import enTranslations from '../locales/en.json';
import csTranslations from '../locales/cs.json';
import nlTranslations from '../locales/nl.json';

const resources = {
  en: {
    translation: enTranslations
  },
  cs: {
    translation: csTranslations
  },
  nl: {
    translation: nlTranslations
  }
};

i18n
  // Translations are bundled above; no runtime HTTP loading needed.
  .use(LanguageDetector)
  // Pass the i18n instance to react-i18next
  .use(initReactI18next)
  // Initialize i18next
  .init({
    resources,
    fallbackLng: 'cs',
    supportedLngs: ['cs', 'en', 'nl'],
    nonExplicitSupportedLngs: true,
    load: 'languageOnly',
    defaultNS: 'translation',

    // Language detection options
    detection: {
      order: ['localStorage', 'navigator', 'htmlTag'],
      lookupLocalStorage: 'preferred-language',
      caches: ['localStorage']
    },

    interpolation: {
      escapeValue: false // React already escapes by default
    },

    // Pluralization options
    pluralSeparator: '_',
    contextSeparator: '_',

    // Development options
    debug: import.meta.env.DEV,

    // Namespace and key separator
    keySeparator: '.',
    nsSeparator: ':',

    // Enable key extraction in development
    saveMissing: import.meta.env.DEV,
    saveMissingTo: 'current',
    missingKeyHandler: (lng, _ns, key, _fallbackValue) => {
      if (import.meta.env.DEV) {
        console.warn(`Missing translation key: ${key} for language: ${lng}`);
      }
    }
  });

// Keep the document language in sync with the selected i18n language
// (the app is bilingual cs/en; the hardcoded lang="cs" broke screen
// readers and translation tools for English users)
i18n.on('languageChanged', (lng) => {
  document.documentElement.lang = lng.split('-')[0] ?? lng;
});
document.documentElement.lang = (i18n.language || 'cs').split('-')[0] ?? 'cs';

export default i18n;
