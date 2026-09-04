import { ArrowLeft } from 'lucide-react';

import { ThemeToggle } from '../ThemeToggle/ThemeToggle';
import { getDashboardBasePath } from '../../dashboardConfig';

type WorkflowStudioTopbarProps = {
  workflowName: string;
  status: string;
};

export function WorkflowStudioTopbar({
  workflowName,
  status,
}: WorkflowStudioTopbarProps) {
  return (
    <header className="flex h-14 shrink-0 items-center justify-between gap-4 border-b border-zinc-200 bg-[var(--runiq-page-surface)] px-4 dark:border-zinc-800">
      <div className="flex min-w-0 items-center gap-4">
        <button
          type="button"
          onClick={navigateToWorkflows}
          className="inline-flex h-9 shrink-0 items-center gap-2 rounded-lg border border-zinc-200 bg-white px-3 text-sm font-medium text-zinc-700 transition hover:border-zinc-300 hover:bg-zinc-100 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-200 dark:hover:border-zinc-700 dark:hover:bg-zinc-900"
        >
          <ArrowLeft className="size-4" />
          Back
        </button>

        <div className="min-w-0 truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">
          {workflowName}
        </div>
      </div>

      <div className="flex shrink-0 items-center gap-2">
        <StatusBadge status={status} />
        <ThemeToggle />
      </div>
    </header>
  );
}

function StatusBadge({ status }: { status: string }) {
  const statusClass =
    status === 'Completed'
      ? 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900/70 dark:bg-emerald-950/30 dark:text-emerald-300'
      : status === 'Failed'
        ? 'border-red-200 bg-red-50 text-red-700 dark:border-red-900/70 dark:bg-red-950/30 dark:text-red-300'
        : status === 'Running'
          ? 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-900/70 dark:bg-blue-950/30 dark:text-blue-300'
          : 'border-zinc-200 bg-zinc-50 text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-400';

  return (
    <span
      className={[
        'inline-flex h-8 items-center rounded-full border px-3 text-xs font-semibold',
        statusClass,
      ].join(' ')}
    >
      {status}
    </span>
  );
}

function navigateToWorkflows() {
  const basePath = getDashboardBasePath().replace(/\/+$/g, '');

  window.history.pushState({}, '', `${basePath}/workflows`);
  window.dispatchEvent(new PopStateEvent('popstate'));
}
