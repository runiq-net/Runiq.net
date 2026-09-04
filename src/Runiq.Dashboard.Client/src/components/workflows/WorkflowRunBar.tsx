import { Play } from 'lucide-react';
import { type FormEvent } from 'react';

type WorkflowRunBarProps = {
  input: string;
  isRunning: boolean;
  errorMessage: string | null;
  onInputChange: (value: string) => void;
  onRun: () => void;
};

export function WorkflowRunBar({
  input,
  isRunning,
  errorMessage,
  onInputChange,
  onRun,
}: WorkflowRunBarProps) {
  const canRun = input.trim().length > 0 && !isRunning;

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (canRun) {
      onRun();
    }
  };

  return (
    <form
      onSubmit={handleSubmit}
      className="border-b border-zinc-200 bg-[var(--runiq-page-surface)] px-4 py-3 dark:border-zinc-800"
    >
      <div className="flex flex-col gap-2 md:flex-row">
        <label className="min-w-0 flex-1">
          <span className="sr-only">Workflow input</span>
          <textarea
            value={input}
            onChange={(event) => onInputChange(event.target.value)}
            placeholder="Enter workflow input..."
            rows={1}
            className="block h-10 w-full resize-none rounded-lg border border-zinc-200 bg-zinc-50 px-3 py-2 text-sm text-zinc-950 outline-none transition placeholder:text-zinc-400 focus:border-zinc-400 dark:border-zinc-800 dark:bg-zinc-900/70 dark:text-zinc-100 dark:placeholder:text-zinc-600 dark:focus:border-zinc-600"
          />
        </label>

        <button
          type="submit"
          disabled={!canRun}
          className="inline-flex h-10 shrink-0 items-center justify-center gap-2 rounded-lg bg-zinc-950 px-4 text-sm font-semibold text-white transition hover:bg-zinc-800 disabled:cursor-not-allowed disabled:bg-zinc-300 disabled:text-zinc-500 dark:bg-zinc-100 dark:text-zinc-950 dark:hover:bg-white dark:disabled:bg-zinc-800 dark:disabled:text-zinc-500"
        >
          <Play className="size-4" />
          {isRunning ? 'Running...' : 'Run workflow'}
        </button>
      </div>

      {errorMessage ? (
        <div className="mt-3 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-900/70 dark:bg-red-950/20 dark:text-red-300">
          {errorMessage}
        </div>
      ) : null}
    </form>
  );
}
