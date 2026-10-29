export type Theme =
  | 'aurora'
  | 'nebula-blue'
  | 'emerald-dawn'
  | 'mystic-violet'
  | 'obsidian'
  | 'cyber-azure'
  | 'forest-shadow'
  | 'cosmic-purple'
  | 'crimson-night';

export interface ThemeInfo {
  id: Theme;
  category: 'light' | 'dark';
  translationKey: string;
}

export const availableThemes: ThemeInfo[] = [
  { id: 'aurora', category: 'light', translationKey: 'themes.aurora' },
  { id: 'nebula-blue', category: 'light', translationKey: 'themes.nebulaBlue' },
  { id: 'emerald-dawn', category: 'light', translationKey: 'themes.emeraldDawn' },
  { id: 'mystic-violet', category: 'light', translationKey: 'themes.mysticViolet' },
  { id: 'obsidian', category: 'dark', translationKey: 'themes.obsidian' },
  { id: 'cyber-azure', category: 'dark', translationKey: 'themes.cyberAzure' },
  { id: 'forest-shadow', category: 'dark', translationKey: 'themes.forestShadow' },
  { id: 'cosmic-purple', category: 'dark', translationKey: 'themes.cosmicPurple' },
  { id: 'crimson-night', category: 'dark', translationKey: 'themes.crimsonNight' }
];

const THEME_STORAGE_KEY = 'cepem-theme';

/**
 * The scheme picker is temporarily hidden and only "aurora" (Polární záře)
 * is supported — the remaining schemes are visually broken. Re-enable by
 * restoring the saved/system theme logic below.
 */
const PINNED_THEME: Theme = 'aurora';

export class ThemeManager {
  private static instance: ThemeManager;
  private currentTheme: Theme = 'aurora';
  private listeners: Set<(theme: Theme) => void> = new Set();

  private constructor() {
    this.loadTheme();
    this.applySystemThemePreference();
  }

  public static getInstance(): ThemeManager {
    if (!ThemeManager.instance) {
      ThemeManager.instance = new ThemeManager();
    }
    return ThemeManager.instance;
  }

  public getCurrentTheme(): Theme {
    return this.currentTheme;
  }

  public setTheme(theme: Theme): void {
    this.currentTheme = theme;
    this.applyTheme(theme);
    this.saveTheme(theme);
    this.notifyListeners(theme);
  }

  public getNextTheme(): Theme {
    const currentIndex = availableThemes.findIndex((t) => t.id === this.currentTheme);
    const nextIndex = (currentIndex + 1) % availableThemes.length;
    return availableThemes[nextIndex]?.id ?? availableThemes[0]?.id ?? this.currentTheme;
  }

  public toggleThemeCategory(): void {
    const currentThemeInfo = availableThemes.find((t) => t.id === this.currentTheme);
    if (!currentThemeInfo) return;

    const oppositeCategory = currentThemeInfo.category === 'light' ? 'dark' : 'light';
    const themesInCategory = availableThemes.filter((t) => t.category === oppositeCategory);

    if (themesInCategory.length > 0) {
      // Try to find a similar theme in the opposite category
      const variant = currentThemeInfo.id.split('-')[1] ?? currentThemeInfo.id;
      const similarTheme =
        themesInCategory.find((t) => t.id.includes(variant)) || themesInCategory[0]; // Fallback to first theme in category

      if (similarTheme) {
        this.setTheme(similarTheme.id);
      }
    }
  }

  public addThemeListener(listener: (theme: Theme) => void): void {
    this.listeners.add(listener);
  }

  public removeThemeListener(listener: (theme: Theme) => void): void {
    this.listeners.delete(listener);
  }

  private applyTheme(theme: Theme): void {
    document.documentElement.setAttribute('data-theme', theme);
  }

  private saveTheme(theme: Theme): void {
    try {
      localStorage.setItem(THEME_STORAGE_KEY, theme);
    } catch (error) {
      console.warn('Failed to save theme preference:', error);
    }
  }

  private loadTheme(): void {
    try {
      // Temporarily pinned: any previously saved alternative scheme is dropped.
      const savedTheme = localStorage.getItem(THEME_STORAGE_KEY) as Theme;
      if (savedTheme && savedTheme !== PINNED_THEME) {
        localStorage.removeItem(THEME_STORAGE_KEY);
      }
    } catch (error) {
      console.warn('Failed to load theme preference:', error);
    }
    this.currentTheme = PINNED_THEME;
    this.applyTheme(PINNED_THEME);
  }

  private applySystemThemePreference(): void {
    // Temporarily pinned to the aurora scheme regardless of OS preference.
    this.setTheme(PINNED_THEME);
  }

  private notifyListeners(theme: Theme): void {
    this.listeners.forEach((listener) => {
      try {
        listener(theme);
      } catch (error) {
        console.error('Error in theme listener:', error);
      }
    });
  }

  public markManualThemeChange(): void {
    try {
      localStorage.setItem('cepem-theme-manual-change', Date.now().toString());
    } catch (error) {
      console.warn('Failed to save manual theme change timestamp:', error);
    }
  }
}

// Export singleton instance
export const themeManager = ThemeManager.getInstance();

// Utility functions for React components (hook should be defined in a separate file with React imports)
export const getThemeUtils = () => ({
  currentTheme: themeManager.getCurrentTheme(),
  changeTheme: (theme: Theme) => {
    themeManager.markManualThemeChange();
    themeManager.setTheme(theme);
  },
  toggleCategory: () => {
    themeManager.markManualThemeChange();
    themeManager.toggleThemeCategory();
  },
  nextTheme: () => {
    themeManager.markManualThemeChange();
    themeManager.setTheme(themeManager.getNextTheme());
  },
  availableThemes
});
