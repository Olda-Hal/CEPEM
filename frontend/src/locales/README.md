# Internationalization (i18n) System

This project includes a complete internationalization system that supports multiple languages.

## Supported Languages

- **Czech (cs)** - Default language
- **English (en)**

## Usage

### Using translations in components

```tsx
import { useTranslation } from '../hooks/useTranslation';

const MyComponent = () => {
  const { t } = useTranslation();
  
  return (
    <div>
      <h1>{t('login.title')}</h1>
      <p>{t('dashboard.welcome', { firstName: 'John', lastName: 'Doe' })}</p>
    </div>
  );
};
```

### Language switching

The `LanguageSwitcher` component is available to switch between languages:

```tsx
import { LanguageSwitcher } from '../components/LanguageSwitcher';

// Use it anywhere in your component
<LanguageSwitcher />
```

### Adding new languages

1. Create a new language file in `src/locales/` (e.g., `de.ts` for German)
2. Follow the same structure as `en.ts`
3. Add the language to the `SupportedLanguages` type in `src/locales/index.ts`
4. Add the new language to the `translations` object
5. Update the `LanguageSwitcher` component to include the new language

### Adding new translation keys

1. Add the key to the English translation file `src/locales/en.ts`
2. Add the same key to all other language files
3. Use the key in your components with the `t()` function

### Parameter substitution

You can use parameters in translations:

```typescript
// In translation file
welcome: "Welcome, {firstName} {lastName}"

// In component
t('welcome', { firstName: 'John', lastName: 'Doe' })
// Result: "Welcome, John Doe"
```

## File Structure

```
src/
├── locales/
│   ├── index.ts          # Main exports and types
│   ├── en.ts            # English translations
│   └── cs.ts            # Czech translations
├── hooks/
│   └── useTranslation.tsx # Translation hook and context
└── components/
    └── LanguageSwitcher.tsx # Language switcher component
```

## Features

- **Persistent language selection** - Saves user's language preference in localStorage
- **Parameter substitution** - Support for dynamic values in translations
- **Type safety** - Full TypeScript support with proper typing
- **Easy to extend** - Simple to add new languages and translation keys
- **Nested keys** - Support for nested translation objects (e.g., `login.title`)
