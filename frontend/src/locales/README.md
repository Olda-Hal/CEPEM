# Internationalization (i18n) System

This project includes a complete internationalization system powered by **react-i18next** with automated string extraction capabilities.

## Supported Languages

- **Czech (cs)** - Default language  
- **English (en)**

## Automated String Extraction

The system supports **automatic extraction** of translatable strings from your code.

### Quick Start

1. **Write code with t() function calls:**
```tsx
import { useTranslation } from 'react-i18next';

const MyComponent = () => {
  const { t } = useTranslation();
  
  return (
    <div>
      <h1>{t('dashboard.welcome', 'Welcome to our system')}</h1>
      <p>{t('common.loading', 'Loading...')}</p>
    </div>
  );
};
```

2. **Run automated extraction:**
```bash
npm run i18n:extract
```

3. **Check for hardcoded strings:**
```bash
npm run i18n:analyze
```

## Available Scripts

- `npm run i18n:extract` - Extract translation keys from code to JSON files
- `npm run i18n:extract:watch` - Watch mode for continuous extraction during development  
- `npm run i18n:analyze` - Analyze code for hardcoded strings that need extraction
- `npm run i18n:migrate` - Run analysis + extraction in one command
- `npm run i18n:missing` - Check for missing translations and fail build if found

## Usage

### Basic Translation Hook

```tsx
import { useTranslation } from 'react-i18next';

const MyComponent = () => {
  const { t, i18n } = useTranslation();
  
  return (
    <div>
      <h1>{t('login.title', 'Default Title')}</h1>
      <p>{t('dashboard.welcome', 'Welcome, {{firstName}}', { firstName: 'John' })}</p>
      <button onClick={() => i18n.changeLanguage('en')}>English</button>
    </div>
  );
};
```

### ExtractableText Component

For automatic extraction with React components:

```tsx
import { ExtractableText } from '../components/ExtractableText';

const MyComponent = () => (
  <div>
    <ExtractableText 
      i18nKey="dashboard.title" 
      defaultText="Center for Preventive Medicine"
      as="h1"
      className="main-title"
    />
    <ExtractableText 
      i18nKey="dashboard.welcome" 
      defaultText="Welcome, {{name}}"
      values={{ name: "John" }}
    />
  </div>
);
```

## Development Workflow

1. **During development:** Use `npm run i18n:extract:watch` to automatically extract new keys
2. **Before commit:** Run `npm run i18n:migrate` to ensure all strings are extracted
3. **In CI/CD:** Use `npm run i18n:missing` to prevent builds with missing translations

## Usage

1. **Using translations:**
```tsx
import { useTranslation } from 'react-i18next';

function MyComponent() {
  const { t } = useTranslation();
  
  return <div>{t('welcome')}</div>;
}
```

2. **Language switching:**
```tsx
// Language switching
const { i18n } = useTranslation();
i18n.changeLanguage('en');
```

## Adding New Languages

1. Create new JSON file: `src/locales/de.json`
2. Copy structure from existing files
3. Update `src/i18n/index.ts` to import new translations
4. Update `i18next-scanner.config.js` to include new language

## File Structure

```
src/
├── i18n/index.ts              # i18next configuration
├── locales/
│   ├── cs.json               # Czech translations
│   ├── en.json               # English translations
│   └── README.md             # This file
├── components/
│   ├── ExtractableText.tsx   # Component for automatic extraction
│   └── LanguageSwitcher.tsx  # Language switcher
├── hooks/
│   └── useTranslation.tsx    # Legacy hook (for compatibility)
└── scripts/
    └── i18n-migrator.js      # Migration analyzer
```

## Best Practices

1. Always provide default values for new translation keys
2. Use descriptive key names that indicate context
3. Group related keys using nested objects
4. Run extraction regularly during development
5. Check for missing translations before production builds
