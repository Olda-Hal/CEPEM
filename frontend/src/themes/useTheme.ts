import { useState, useEffect } from 'react';
import { Theme, themeManager, availableThemes } from './ThemeManager';

export const useTheme = () => {
  const [currentTheme, setCurrentTheme] = useState<Theme>(
    themeManager.getCurrentTheme()
  );

  useEffect(() => {
    const handleThemeChange = (theme: Theme) => {
      setCurrentTheme(theme);
    };

    themeManager.addThemeListener(handleThemeChange);
    
    return () => {
      themeManager.removeThemeListener(handleThemeChange);
    };
  }, []);

  const changeTheme = (theme: Theme) => {
    themeManager.markManualThemeChange();
    themeManager.setTheme(theme);
  };

  return {
    currentTheme,
    changeTheme,
    toggleCategory: () => {
      themeManager.markManualThemeChange();
      themeManager.toggleThemeCategory();
    },
    nextTheme: () => {
      themeManager.markManualThemeChange();
      themeManager.setTheme(themeManager.getNextTheme());
    },
    availableThemes,
  };
};
