import { createContext, useContext } from 'react';

/** Identifies one of the dashboard's supported color themes. */
export type Theme = 'dark' | 'light';

/** Provides the active theme and operations that update it. */
export type ThemeContextValue = {
  theme: Theme;
  toggleTheme: () => void;
  setTheme: (theme: Theme) => void;
};

/** Stores theme state supplied by the dashboard theme provider. */
export const ThemeContext = createContext<ThemeContextValue | null>(null);

/** Returns the theme context for a component rendered inside the theme provider. */
export function useTheme(): ThemeContextValue {
  const context = useContext(ThemeContext);

  if (!context) {
    throw new Error('useTheme must be used inside ThemeProvider.');
  }

  return context;
}
