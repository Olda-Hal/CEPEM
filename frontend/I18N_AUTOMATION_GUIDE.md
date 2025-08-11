# 🌍 Automatizovaná Internacionalizace (i18n) - Implementační Příručka

## 🎯 Co jsme implementovali

Kompletní automatizovaný systém pro extrakci a správu lokalizačních textů pomocí **react-i18next** s následujícími funkcemi:

- ✅ **Automatická extrakce stringů** z kódu do JSON souborů
- ✅ **Analýza hardcoded stringů** v existujícím kódu
- ✅ **Migrace z původního systému** s kompatibilním API
- ✅ **TypeScript podpora** s type safety
- ✅ **Watch mode** pro kontinuální vývoj
- ✅ **CI/CD integrace** pro kontrolu missing translations

## 🚀 Rychlý start

### 1. Instalace (již hotovo)
```bash
npm install react-i18next i18next i18next-browser-languagedetector i18next-http-backend --legacy-peer-deps
npm install --save-dev i18next-scanner @types/i18next --legacy-peer-deps
```

### 2. Dostupné příkazy
```bash
# Analyzuj stávající kód pro hardcoded stringy
npm run i18n:analyze

# Extrahuj translation klíče z kódu
npm run i18n:extract

# Watch mode pro kontinuální extrakci
npm run i18n:extract:watch

# Kompletní migrace (analýza + extrakce)
npm run i18n:migrate

# Kontrola missing translations (pro CI/CD)
npm run i18n:missing
```

## 📋 Způsoby použití

### Způsob 1: Standardní react-i18next
```tsx
import { useTranslation } from 'react-i18next';

const MyComponent = () => {
  const { t, i18n } = useTranslation();
  
  return (
    <div>
      {/* S fallback hodnotou */}
      <h1>{t('dashboard.title', 'Centrum Preventivní Medicíny')}</h1>
      
      {/* S parametry */}
      <p>{t('dashboard.welcome', 'Vítejte, {{name}}!', { name: 'Jan' })}</p>
      
      {/* Přepnutí jazyka */}
      <button onClick={() => i18n.changeLanguage('en')}>English</button>
    </div>
  );
};
```

### Způsob 2: ExtractableText komponenta
```tsx
import { ExtractableText } from '../components/ExtractableText';

const MyComponent = () => (
  <div>
    <ExtractableText 
      i18nKey="dashboard.title" 
      defaultText="Centrum Preventivní Medicíny"
      as="h1"
      className="title"
    />
    <ExtractableText 
      i18nKey="dashboard.welcome" 
      defaultText="Vítejte, {{name}}!"
      values={{ name: "Jan" }}
    />
  </div>
);
```

### Způsob 3: Migrace z původního systému
```tsx
import { useExtractableTranslation } from '../components/ExtractableText';

const MyComponent = () => {
  const { extract } = useExtractableTranslation();
  
  return (
    <div>
      <h1>{extract('dashboard.title', 'Centrum Preventivní Medicíny')}</h1>
      <p>{extract('dashboard.welcome', 'Vítejte, {{name}}!', { name: 'Jan' })}</p>
    </div>
  );
};
```

## 🔄 Workflow pro vývojáře

### Denní vývoj
1. **Během vývoje:** Spusť `npm run i18n:extract:watch` na pozadí
2. **Piš kód** s t() funkcemi a fallback hodnotami
3. **Klíče se automaticky přidávají** do JSON souborů
4. **Doplň překlady** v `src/locales/en.json` a `src/locales/cs.json`

### Před commitem
```bash
npm run i18n:migrate
```
To spustí analýzu + extrakci a ukáže ti všechny hardcoded stringy.

### V CI/CD
```bash
npm run i18n:missing
```
Tento příkaz selže pokud jsou missing translations, čímž zabrání nasazení neúplných překladů.

## 📁 Struktura souborů

```
src/
├── i18n/
│   └── index.ts              # Hlavní i18next konfigurace
├── locales/
│   ├── cs.json              # České překlady (nový formát)
│   ├── en.json              # Anglické překlady (nový formát)
│   ├── cs.ts                # Legacy soubory (zastaralé)
│   ├── en.ts                # Legacy soubory (zastaralé)
│   └── README.md            # Dokumentace
├── components/
│   ├── ExtractableText.tsx  # Komponenta pro automatickou extrakci
│   ├── LanguageSwitcher.tsx # Aktualizovaný přepínač jazyků
│   └── I18nDemo.tsx         # Demonstrace použití
├── hooks/
│   ├── useTranslation.tsx   # Legacy hook (zastaralý)
│   └── useTranslationNew.tsx # Kompatibilní hook
└── scripts/
    └── i18n-migrator.js     # Automatický analyzátor
```

## 🔧 Konfigurace

### i18next-scanner.config.js
Ovládá jak se stringy extrahují z kódu:
```javascript
module.exports = {
  input: ['src/**/*.{js,jsx,ts,tsx}'],
  options: {
    func: {
      list: ['t', 'i18next.t', 'useTranslation().t'],
    },
    defaultValue: '__STRING_NOT_TRANSLATED__',
    lngs: ['en', 'cs'],
  }
};
```

### package.json scripts
```json
{
  "scripts": {
    "i18n:extract": "i18next-scanner --config i18next-scanner.config.js",
    "i18n:extract:watch": "i18next-scanner --config i18next-scanner.config.js --watch",
    "i18n:analyze": "node scripts/i18n-migrator.js",
    "i18n:migrate": "npm run i18n:analyze && npm run i18n:extract",
    "i18n:missing": "i18next-scanner --config i18next-scanner.config.js --failOnUpdate"
  }
}
```

## 🔄 Migrace z původního systému

### Krok 1: Aktualizace importů
```tsx
// Starý
import { useTranslation } from '../hooks/useTranslation';

// Nový
import { useTranslation } from 'react-i18next';
```

### Krok 2: Aktualizace parametrů
```tsx
// Starý formát
t('welcome', { firstName: 'John', lastName: 'Doe' })

// Nový formát (stejný)
t('welcome', { firstName: 'John', lastName: 'Doe' })
// Nebo s fallback
t('welcome', 'Welcome, {{firstName}} {{lastName}}', { firstName: 'John', lastName: 'Doe' })
```

### Krok 3: Přepínání jazyků
```tsx
// Starý
const { setLanguage } = useTranslation();
setLanguage('en');

// Nový
const { i18n } = useTranslation();
i18n.changeLanguage('en');
```

## 🔍 Debug a troubleshooting

### Development debug
V `src/i18n/index.ts` je zapnutý debug mode:
```typescript
debug: process.env.NODE_ENV === 'development'
```

### Časté problémy

**Missing translations:**
- Spusť `npm run i18n:missing` pro kontrolu
- Zkontroluj že JSON soubory jsou správně naformátované

**Scanner nenalézá t() calls:**
- Zkontroluj že funkce calls odpovídají patterns v konfiguraci
- Ujisti se že soubory jsou v input paths

**TypeScript chyby:**
- Aktualizuj importy na `react-i18next`
- Zkontroluj že JSON soubory se importují správně

## 🌟 Výhody nového systému

1. **Automatizace** - Už žádné manuální přidávání klíčů
2. **Analýza** - Automatické nalezení hardcoded stringů
3. **Kontinuální integrace** - Zabráníš deployment s missing překlady
4. **Type safety** - Plná TypeScript podpora
5. **Performance** - Optimalizované pro production builds
6. **Industry standard** - Postaveno na react-i18next (standard v React ekosystému)

## 🎯 Další kroky

1. **Postupně migruj** existující komponenty pomocí `npm run i18n:analyze`
2. **Nastav CI/CD** pipeline s `npm run i18n:missing`
3. **Přidej další jazyky** podle dokumentace v README.md
4. **Smaž legacy soubory** až dokončíš migraci (cs.ts, en.ts, staré hooky)

---

**Hotovo!** Máš nyní kompletní automatizovaný i18n systém. Pro otázky se podívej na `src/components/I18nDemo.tsx` kde najdeš praktické příklady použití.
