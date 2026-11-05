import { useEffect, useState } from 'react';
import { getCurrentTheme, subscribeToTheme, Theme, toggleTheme } from './theme';

/** Reads the active light/dark theme and re-renders after a toggle. */
export const useTheme = () => {
  const [theme, setTheme] = useState<Theme>(getCurrentTheme);

  useEffect(() => subscribeToTheme(setTheme), []);

  return { theme, toggleTheme };
};
