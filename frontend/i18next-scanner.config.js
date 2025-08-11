const fs = require('fs');
const path = require('path');

module.exports = {
  input: [
    'src/**/*.{js,jsx,ts,tsx}',
    '!src/**/*.test.{js,jsx,ts,tsx}',
    '!src/i18n/**',
    '!src/reportWebVitals.ts',
    '!src/setupTests.ts',
    '!src/react-app-env.d.ts',
    '!src/types/**',
    '!src/utils/api.ts',
    '!**/node_modules/**',
  ],
  output: './',
  options: {
    debug: false,
    func: {
      // Function names to look for
      list: ['t', 'i18next.t', 'useTranslation().t', 'translation.t'],
      extensions: ['.js', '.jsx', '.ts', '.tsx'],
    },
    trans: {
      component: 'Trans',
      i18nKey: 'i18nKey',
      defaultsKey: 'defaults',
      extensions: ['.js', '.jsx', '.ts', '.tsx'],
      fallbackKey: function(ns, value) {
        return value;
      },
      acorn: {
        ecmaVersion: 2022,
        sourceType: 'module',
        allowHashBang: true,
        allowReturnOutsideFunction: true,
        allowImportExportEverywhere: true,
        allowAwaitOutsideFunction: true,
        plugins: {
          jsx: true,
          typescript: true,
        },
      },
    },
    lngs: ['en', 'cs'],
    ns: ['translation'],
    defaultLng: 'cs',
    defaultNs: 'translation',
    defaultValue: function(lng, ns, key) {
      // Return the key as default value for missing translations
      if (lng === 'cs') {
        return key;
      }
      return '__STRING_NOT_TRANSLATED__';
    },
    resource: {
      loadPath: 'src/locales/{{lng}}.json',
      savePath: 'src/locales/{{lng}}.json',
      jsonIndent: 2,
      lineEnding: '\n',
    },
    nsSeparator: ':',
    keySeparator: '.',
    interpolation: {
      prefix: '{{',
      suffix: '}}',
    },
  },
  transform: function customTransform(file, enc, done) {
    "use strict";
    const parser = this.parser;
    const content = fs.readFileSync(file.path, enc);
    let count = 0;

    // Skip non-source files that might cause parsing issues
    if (file.path.includes('reportWebVitals') || 
        file.path.includes('setupTests') || 
        file.path.includes('react-app-env.d.ts') ||
        file.path.includes('.test.') ||
        file.path.includes('node_modules')) {
      done();
      return;
    }

    try {
      parser.parseFuncFromString(content, { list: ['t'] }, (key, options) => {
        parser.set(key, Object.assign({}, options, {
          nsSeparator: false,
          keySeparator: false
        }));
        ++count;
      });

      if (count > 0) {
        console.log(`Extracted ${count} keys from ${file.relative}`);
      }
    } catch (error) {
      // Silently skip files that can't be parsed - they likely don't contain translation keys anyway
      console.log(`Skipped ${file.relative} (parsing error)`);
    }

    done();
  },
};
