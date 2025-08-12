# Internationalization

React i18next setup for multilingual support.

## Supported Languages

- Czech (cs) - default
- English (en)

## Usage

```tsx
import { useTranslation } from 'react-i18next';

const MyComponent = () => {
  const { t } = useTranslation();
  return <div>{t('key', 'Default text')}</div>;
};
```

## Scripts

- `npm run i18n:extract` - Extract translation keys
- `npm run i18n:analyze` - Find hardcoded strings

## Adding Languages

1. Create new JSON file in `locales/`
2. Update `src/i18n/index.ts`
3. Update `i18next-scanner.config.js`
