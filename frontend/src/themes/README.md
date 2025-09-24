# CEPEM Theme System Documentation

This document explains how to use the CEPEM theme system.

## Quick Start

1. **Import theme CSS** - The theme variables are already imported in `App.tsx`:
   ```tsx
   import './themes/colors.css';
   ```

2. **Use CSS variables** - Replace hardcoded colors with theme variables:
   ```css
   /* Before */
   .my-component {
     background-color: #ffffff;
     color: #2c3e50;
     border: 1px solid #e1e8ed;
   }

   /* After */
   .my-component {
     background-color: var(--color-background);
     color: var(--color-text-primary);
     border: 1px solid var(--color-border);
   }
   ```

3. **Add theme selector** - Import and use the ThemeSelector component:
   ```tsx
   import { ThemeSelector } from './components/ThemeSelector';
   
   // In your component
   <ThemeSelector className="compact" />
   ```

## Available Themes

### Light Themes
- `aurora` (Polární záře) - Default theme with original CEPEM colors
- `nebula-blue` (Modrá mlhovina) - Blue accent variant
- `emerald-dawn` (Smaragdový úsvit) - Green accent variant  
- `mystic-violet` (Mystická fialová) - Purple accent variant

### Dark Themes  
- `obsidian` (Obsidián) - Default dark theme with original CEPEM colors
- `cyber-azure` (Kybernet azur) - Dark theme with blue accents
- `forest-shadow` (Lesní stín) - Dark theme with green accents
- `cosmic-purple` (Kosmická purpur) - Dark theme with purple accents
- `crimson-night` (Karmínová noc) - Dark theme with red accents

All theme names are automatically translated and available in both English and Czech.

## CSS Variables Reference

### Primary Colors
- `--color-primary` - Main brand color
- `--color-primary-hover` - Hover state for primary color
- `--color-primary-light` - Light version for backgrounds/shadows

### Secondary Colors
- `--color-secondary` - Secondary button/element color
- `--color-secondary-hover` - Hover state for secondary color

### Text Colors
- `--color-text-primary` - Main text color
- `--color-text-secondary` - Secondary/muted text
- `--color-text-light` - Light/disabled text

### Background Colors
- `--color-background` - Main background (cards, modals)
- `--color-background-secondary` - Secondary background
- `--color-background-tertiary` - Tertiary background
- `--color-background-page` - Page/body background

### Border Colors
- `--color-border` - Default border color
- `--color-border-light` - Light borders
- `--color-border-focus` - Focus/active borders

### Status Colors
- `--color-success` / `--color-success-light`
- `--color-warning` / `--color-warning-light` 
- `--color-error` / `--color-error-light`
- `--color-info` / `--color-info-light`

### Shadow Colors
- `--color-shadow-light` - Light shadows
- `--color-shadow-medium` - Medium shadows
- `--color-shadow-heavy` - Heavy shadows

## React Hook Usage

```tsx
import { useTheme } from './themes/useTheme';
import { useTranslation } from 'react-i18next';

export const MyComponent = () => {
  const { t } = useTranslation();
  const { currentTheme, changeTheme, toggleCategory, availableThemes } = useTheme();

  return (
    <div>
      <p>Current theme: {currentTheme}</p>
      <button onClick={() => changeTheme('cyber-azure')}>
        Switch to {t('themes.cyberAzure')}
      </button>
      <button onClick={toggleCategory}>
        Toggle Light/Dark
      </button>
    </div>
  );
};
```

## Programmatic Theme Management

```tsx
import { themeManager } from './themes/ThemeManager';

// Get current theme
const current = themeManager.getCurrentTheme();

// Set theme
themeManager.setTheme('cosmic-purple');

// Toggle between light/dark
themeManager.toggleThemeCategory();

// Listen for theme changes
themeManager.addThemeListener((theme) => {
  console.log('Theme changed to:', theme);
});
```

## Migration Guide

To migrate existing components:

1. **Replace color values** with CSS variables
2. **Test with multiple themes** to ensure proper contrast
3. **Add theme transitions** for smooth switching:
   ```css
   * {
     transition: background-color 0.3s ease, color 0.3s ease, border-color 0.3s ease;
   }
   ```

## Best Practices

1. **Always use theme variables** instead of hardcoded colors
2. **Test with both light and dark themes** during development
3. **Use semantic color names** (primary, secondary, error) rather than specific colors
4. **Respect system preferences** - the theme system automatically detects `prefers-color-scheme`
5. **Provide theme selection** in user settings or headers

## Examples

See updated files:
- `CreatePatientModal.css` - Complete modal theming
- `DashboardPage.css` - Page layout theming
- `App.css` - Global theming

## Theme System Features

- ✅ **9 built-in themes** (4 light + 5 dark)
- ✅ **Preserves original CEPEM design** - base themes use original colors
- ✅ **Accent color variants** - color variants only change gradients and accents
- ✅ **Global theme overrides** - ensures all components use theme colors
- ✅ **Automatic system theme detection**
- ✅ **Persistent theme preferences**
- ✅ **Smooth transitions**
- ✅ **React hooks integration**
- ✅ **TypeScript support**
- ✅ **Accessibility considerations**

## Design Philosophy

The theme system is designed to:
1. **Preserve the original CEPEM look** - `light` and `dark` themes use the original gradient colors
2. **Provide subtle variations** - color variants only change accent/gradient colors, not the entire UI
3. **Ensure consistency** - global overrides prevent components from having hardcoded colors
4. **Maintain usability** - all themes maintain good contrast and readability
