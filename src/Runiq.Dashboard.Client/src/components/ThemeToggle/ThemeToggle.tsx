import { Moon, Sun } from 'lucide-react';
import { useTheme } from '../../theme/themeContext';

export function ThemeToggle() {
  const { theme, toggleTheme } = useTheme();
  const isDark = theme === 'dark';

  return (
    <button
      type="button"
      onClick={toggleTheme}
      aria-label={isDark ? 'Switch to light mode' : 'Switch to dark mode'}
      title={isDark ? 'Switch to light mode' : 'Switch to dark mode'}
      className="inline-flex size-10 items-center justify-center rounded-xl border border-zinc-200 bg-white text-zinc-700 shadow-sm transition hover:bg-zinc-100 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-100 dark:hover:border-zinc-700 dark:hover:bg-zinc-900"
    >
      {isDark ? (
        <Moon size={18} strokeWidth={2} aria-hidden="true" />
      ) : (
        <Sun size={18} strokeWidth={2} aria-hidden="true" />
      )}
    </button>
  );
}
