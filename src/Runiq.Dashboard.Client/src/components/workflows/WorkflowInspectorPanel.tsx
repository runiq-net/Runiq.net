import { AlertTriangle, ChevronRight } from 'lucide-react';
import type { ReactNode } from 'react';

import type {
  WorkflowMetadata,
  WorkflowRunResponse,
  WorkflowStepRunResult,
  WorkflowToolCallRunResult,
} from '../../api/agentMetadataApi';
import { getStepRunResult } from './workflowTypes';
import { workflowEndNodeId, workflowStartNodeId } from './workflowTypes';

type WorkflowInspectorPanelProps = {
  workflow: WorkflowMetadata;
  runResult: WorkflowRunResponse | null;
  selectedNodeId: string | null;
  currentInput: string;
  isCollapsed: boolean;
  onCollapseChange: (isCollapsed: boolean) => void;
};

export function WorkflowInspectorPanel({
  workflow,
  runResult,
  selectedNodeId,
  currentInput,
  isCollapsed,
  onCollapseChange,
}: WorkflowInspectorPanelProps) {
  if (isCollapsed) {
    return (
      <button
        type="button"
        onClick={() => onCollapseChange(false)}
        className="hidden h-full w-11 shrink-0 items-center justify-center border-l border-zinc-200 bg-[var(--runiq-page-surface)] text-zinc-500 transition hover:bg-zinc-100 hover:text-zinc-950 dark:border-zinc-800 dark:text-zinc-500 dark:hover:bg-zinc-900 dark:hover:text-zinc-100 lg:flex"
        aria-label="Open inspector"
      >
        <span className="-rotate-90 text-xs font-semibold uppercase tracking-[0.14em]">
          Inspector
        </span>
      </button>
    );
  }

  const selectedStep = workflow.steps.find(
    (step) => step.id.toLowerCase() === selectedNodeId?.toLowerCase(),
  );
  const selectedStepResult = selectedStep
    ? getStepRunResult(runResult, selectedStep.id)
    : undefined;
  const runInput = runResult?.steps[0]?.input ?? currentInput;

  return (
    <aside className="h-full w-full shrink-0 overflow-hidden border-l border-zinc-200 bg-[var(--runiq-page-surface)] dark:border-zinc-800 lg:w-[352px]">
      <div className="flex h-11 items-center justify-between border-b border-zinc-200 px-4 dark:border-zinc-800">
        <div className="text-xs font-semibold uppercase tracking-[0.14em] text-zinc-500 dark:text-zinc-500">
          Inspector
        </div>
        <button
          type="button"
          onClick={() => onCollapseChange(true)}
          className="inline-flex size-8 items-center justify-center rounded-lg border border-zinc-200 bg-white text-zinc-600 transition hover:bg-zinc-100 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-300 dark:hover:bg-zinc-900"
          aria-label="Collapse inspector"
        >
          <ChevronRight className="size-4" />
        </button>
      </div>

      <div className="h-[calc(100%-2.75rem)] overflow-y-auto p-4">
        {selectedNodeId === workflowStartNodeId ? (
          <StartInspector runInput={runInput} />
        ) : selectedNodeId === workflowEndNodeId ? (
          <EndInspector runResult={runResult} />
        ) : selectedStep ? (
          <StepInspector
            step={selectedStep}
            result={selectedStepResult}
            finalOutput={runResult?.finalOutput ?? null}
          />
        ) : (
          <WorkflowSummary workflow={workflow} />
        )}
      </div>
    </aside>
  );
}

function StartInspector({ runInput }: { runInput: string }) {
  return (
    <>
      <Section title="Workflow Input">
        {runInput.trim() ? (
          <TextBlock value={runInput} />
        ) : (
          <EmptyText value="No workflow input entered yet." />
        )}
      </Section>
    </>
  );
}

function EndInspector({ runResult }: { runResult: WorkflowRunResponse | null }) {
  return (
    <>
      <Section title="End">
        <KeyValue
          label="Workflow status"
          value={runResult?.status ?? 'Not run'}
        />
      </Section>

      <Section title="Final Output">
        {runResult?.finalOutput ? (
          <TextBlock value={runResult.finalOutput} />
        ) : (
          <EmptyText value="No final output yet." />
        )}
      </Section>

      {runResult?.errorMessage ? (
        <Section title="Error">
          <ErrorBlock value={runResult.errorMessage} />
        </Section>
      ) : null}
    </>
  );
}

function StepInspector({
  step,
  result,
  finalOutput,
}: {
  step: WorkflowMetadata['steps'][number];
  result?: WorkflowStepRunResult;
  finalOutput: string | null;
}) {
  return (
    <>
      <Section title="Selected Step" flush>
        <div className="min-w-0">
          <div className="truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">
            {step.agentName}
          </div>
          <div className="mt-1 truncate text-xs text-zinc-500 dark:text-zinc-500">
            {step.id} · {result?.status ?? 'NotStarted'}
          </div>
          <div className="mt-2 inline-flex max-w-full rounded-full border border-zinc-200 bg-zinc-50 px-2 py-1 text-[11px] font-medium text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900/80 dark:text-zinc-400">
            {formatFailurePolicy(step.failureBehavior, step.failureStepId)}
          </div>
        </div>
      </Section>

      <StepRunDetail title="Step Input" result={result} field="input" />
      <StepRunDetail title="Step Output" result={result} field="output" />

      <ToolCallsSection toolCalls={result?.toolCalls ?? []} />

      <Section title="Error">
        {result?.errorMessage ? (
          <ErrorBlock value={result.errorMessage} />
        ) : (
          <EmptyText value="No error captured for this step." />
        )}
      </Section>

      <Section title="Final Output">
        {finalOutput ? (
          <TextBlock value={finalOutput} compact />
        ) : (
          <EmptyText value="No final output yet." />
        )}
      </Section>
    </>
  );
}

function WorkflowSummary({ workflow }: { workflow: WorkflowMetadata }) {
  return (
    <>
      <Section title="Workflow Summary">
        <KeyValue label="Start step" value={workflow.startStepId ?? 'None'} />
        <KeyValue label="Steps" value={String(workflow.stepCount)} />
        <KeyValue
          label="Flow"
          value={workflow.steps.map((step) => step.id).join(' -> ')}
        />
      </Section>
    </>
  );
}

function Section({
  title,
  children,
  flush = false,
}: {
  title: string;
  children: ReactNode;
  flush?: boolean;
}) {
  return (
    <section
      className={[
        flush
          ? ''
          : 'mt-5 border-t border-zinc-200 pt-4 dark:border-zinc-800',
      ].join(' ')}
    >
      <h2 className="text-xs font-semibold uppercase tracking-[0.12em] text-zinc-500 dark:text-zinc-500">
        {title}
      </h2>
      <div className="mt-3 space-y-3">{children}</div>
    </section>
  );
}

function KeyValue({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="text-[11px] font-medium uppercase tracking-[0.1em] text-zinc-500 dark:text-zinc-600">
        {label}
      </div>
      <div className="mt-1 break-words text-sm text-zinc-800 dark:text-zinc-200">
        {value}
      </div>
    </div>
  );
}

function StepRunDetail({
  title,
  result,
  field,
}: {
  title: string;
  result?: WorkflowStepRunResult;
  field: 'input' | 'output';
}) {
  return (
    <Section title={title}>
      {result?.[field] ? (
        <TextBlock value={result[field] ?? ''} />
      ) : (
        <EmptyText value={`No ${field} captured for this step.`} />
      )}
    </Section>
  );
}

function ToolCallsSection({
  toolCalls,
}: {
  toolCalls: WorkflowToolCallRunResult[];
}) {
  return (
    <Section title={`Tool Calls (${toolCalls.length})`}>
      {toolCalls.length === 0 ? (
        <EmptyText value="No tool calls captured for this step." />
      ) : (
        <div className="space-y-2">
          {toolCalls.map((toolCall, index) => (
            <ToolCallItem
              key={toolCall.toolCallId ?? `${toolCall.toolName ?? 'tool'}-${index}`}
              toolCall={toolCall}
              defaultOpen={shouldOpenToolCall(toolCalls, index)}
            />
          ))}
        </div>
      )}
    </Section>
  );
}

function ToolCallItem({
  toolCall,
  defaultOpen,
}: {
  toolCall: WorkflowToolCallRunResult;
  defaultOpen: boolean;
}) {
  return (
    <details
      open={defaultOpen}
      className="group rounded-lg border border-zinc-200 bg-zinc-50 dark:border-zinc-800 dark:bg-zinc-900/60"
    >
      <summary className="flex cursor-pointer list-none items-center gap-2 px-3 py-2 text-sm marker:hidden [&::-webkit-details-marker]:hidden">
        <span className="text-zinc-400 transition group-open:rotate-90 dark:text-zinc-500">
          <ChevronRight className="size-3.5" />
        </span>
        <span className="min-w-0 flex-1 truncate font-mono font-semibold text-zinc-900 dark:text-zinc-100">
          {toolCall.toolName || toolCall.toolCallId || 'Tool call'}
        </span>
        {typeof toolCall.durationMs === 'number' ? (
          <span className="shrink-0 text-xs text-zinc-500 dark:text-zinc-500">
            {toolCall.durationMs} ms
          </span>
        ) : null}
        <ToolStatusBadge status={toolCall.status} />
      </summary>

      <div className="space-y-3 border-t border-zinc-200 px-3 py-3 dark:border-zinc-800">
        <ToolCallBlock
          title="Arguments"
          value={toolCall.argumentsJson}
          emptyText="No arguments captured."
        />
        <ToolCallBlock
          title="Result"
          value={toolCall.outputJson}
          emptyText="No result captured."
        />
        {toolCall.errorMessage || toolCall.errorCode ? (
          <ToolCallBlock
            title="Error"
            value={[toolCall.errorCode, toolCall.errorMessage]
              .filter(Boolean)
              .join('\n')}
            emptyText="No error captured."
            isError
          />
        ) : null}
      </div>
    </details>
  );
}

function ToolCallBlock({
  title,
  value,
  emptyText,
  isError = false,
}: {
  title: string;
  value?: string | null;
  emptyText: string;
  isError?: boolean;
}) {
  const content = value?.trim() ? prettyPrintJson(value) : null;

  return (
    <div>
      <div className="mb-1.5 text-[11px] font-semibold uppercase tracking-[0.1em] text-zinc-500 dark:text-zinc-500">
        {title}
      </div>
      {content ? (
        <pre
          className={[
            'max-h-44 overflow-auto whitespace-pre-wrap rounded-md border p-2.5 text-xs leading-5 [overflow-wrap:anywhere]',
            isError
              ? 'border-red-200 bg-red-50 text-red-700 dark:border-red-900/70 dark:bg-red-950/20 dark:text-red-300'
              : 'border-zinc-200 bg-white text-zinc-800 dark:border-zinc-800 dark:bg-zinc-950/70 dark:text-zinc-200',
          ].join(' ')}
        >
          {content}
        </pre>
      ) : (
        <EmptyText value={emptyText} />
      )}
    </div>
  );
}

function ToolStatusBadge({ status }: { status: string }) {
  const normalized = normalizeStatus(status);

  return (
    <span
      className={[
        'shrink-0 rounded-full border px-2 py-0.5 text-[11px] font-semibold',
        normalized === 'failed'
          ? 'border-red-200 bg-red-50 text-red-700 dark:border-red-900/70 dark:bg-red-950/30 dark:text-red-300'
          : '',
        normalized === 'completed'
          ? 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900/70 dark:bg-emerald-950/30 dark:text-emerald-300'
          : '',
        normalized === 'running'
          ? 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-900/70 dark:bg-blue-950/30 dark:text-blue-300'
          : '',
        normalized !== 'failed' &&
        normalized !== 'completed' &&
        normalized !== 'running'
          ? 'border-zinc-200 bg-zinc-100 text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-400'
          : '',
      ].join(' ')}
    >
      {status || 'Unknown'}
    </span>
  );
}

function shouldOpenToolCall(
  toolCalls: WorkflowToolCallRunResult[],
  index: number,
): boolean {
  if (toolCalls.length === 1) {
    return true;
  }

  const firstFailedIndex = toolCalls.findIndex((toolCall) =>
    normalizeStatus(toolCall.status) === 'failed',
  );

  return firstFailedIndex >= 0 ? index === firstFailedIndex : index === 0;
}

function prettyPrintJson(value: string): string {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function normalizeStatus(status: string): string {
  return status.trim().toLowerCase();
}

function TextBlock({
  value,
  compact = false,
}: {
  value: string;
  compact?: boolean;
}) {
  return (
    <pre
      className={[
        'overflow-auto whitespace-pre-wrap rounded-lg border border-zinc-200 bg-zinc-50 p-3 text-xs leading-5 text-zinc-800 dark:border-zinc-800 dark:bg-zinc-900/80 dark:text-zinc-200',
        compact ? 'max-h-36' : 'max-h-56',
      ].join(' ')}
    >
      {value}
    </pre>
  );
}

function ErrorBlock({ value }: { value: string }) {
  return (
    <div className="flex gap-2 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-900/70 dark:bg-red-950/20 dark:text-red-300">
      <AlertTriangle className="mt-0.5 size-4 shrink-0" />
      <span className="min-w-0 break-words">{value}</span>
    </div>
  );
}

function EmptyText({ value }: { value: string }) {
  return <div className="text-sm text-zinc-500 dark:text-zinc-500">{value}</div>;
}

function formatFailurePolicy(
  failureBehavior: string,
  failureStepId?: string | null,
): string {
  if (failureBehavior === 'Stop') {
    return 'On failure: stop';
  }

  return failureStepId
    ? `On failure: ${failureBehavior.toLowerCase()} -> ${failureStepId}`
    : `On failure: ${failureBehavior.toLowerCase()}`;
}
