import React from 'react';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../themes/useTheme';
import './ThemeSelector.css';

interface ThemeSelectorProps {
  className?: string;
}

export const ThemeSelector: React.FC<ThemeSelectorProps> = ({ className = '' }) => {
  const { t } = useTranslation();
  const { currentTheme, changeTheme, availableThemes } = useTheme();

  return (
    <div className={`theme-selector ${className}`}>
      <select
        value={currentTheme}
        onChange={(e) => changeTheme(e.target.value as any)}
        className="theme-select"
        title="Change theme"
      >
        <optgroup label={t('themes.lightThemes')}>
          {availableThemes
            .filter(theme => theme.category === 'light')
            .map(theme => (
              <option key={theme.id} value={theme.id}>
                {t(theme.translationKey)}
              </option>
            ))
          }
        </optgroup>
        <optgroup label={t('themes.darkThemes')}>
          {availableThemes
            .filter(theme => theme.category === 'dark')
            .map(theme => (
              <option key={theme.id} value={theme.id}>
                {t(theme.translationKey)}
              </option>
            ))
          }
        </optgroup>
      </select>
      <div className="theme-preview" />
    </div>
  );
};

export default ThemeSelector;
