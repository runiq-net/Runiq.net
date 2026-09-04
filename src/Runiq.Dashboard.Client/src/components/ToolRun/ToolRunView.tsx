import type { ToolJsonSchema, ToolRunResponse } from '../../api/agentMetadataApi';
import { isRequired, type ToolInputValue } from './toolInput';
import type { useToolRunController } from './useToolRunController';

type ToolRunLabels = {
  input: string;
  inputDescription?: string;
  emptyInput: string;
  result: string;
  resultDescription?: string;
  running: string;
  failure: string;
};

type ToolRunViewProps = {
  controller: ReturnType<typeof useToolRunController>;
  schema: ToolJsonSchema;
  hasInput: boolean;
  labels: ToolRunLabels;
};

/** Renders the shared input and result surface for a runnable tool. */
export function ToolRunView({ controller, schema, hasInput, labels }: ToolRunViewProps) {
  return (
    <section className="flex min-h-0 min-w-0 flex-1 rounded-lg border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:shadow-none">
      <aside className="flex w-[320px] shrink-0 flex-col border-r border-zinc-200 dark:border-zinc-800">
        <PanelHeader title={labels.input} description={labels.inputDescription} />
        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-5 [scrollbar-width:thin]">
          {!hasInput || controller.inputProperties.length === 0 ? (
            <div className="rounded-md border border-zinc-200 bg-zinc-50 p-4 text-sm text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900/40 dark:text-zinc-400">
              {labels.emptyInput}
            </div>
          ) : (
            <div className="space-y-4">
              {controller.inputProperties.map(([name, propertySchema]) => (
                <ToolInputField
                  key={name}
                  name={name}
                  schema={propertySchema}
                  required={isRequired(schema, name)}
                  value={controller.inputValues[name]}
                  disabled={controller.isRunning}
                  error={controller.validationErrors[name]}
                  onChange={(value) => controller.setInput(name, value)}
                />
              ))}
            </div>
          )}
          <button
            type="button"
            disabled={controller.isRunning}
            onClick={controller.run}
            className="mt-6 inline-flex h-10 w-full items-center justify-center rounded-md bg-zinc-950 px-4 text-sm font-medium text-white transition hover:bg-zinc-800 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-zinc-100 dark:text-zinc-950 dark:hover:bg-zinc-200"
          >
            {controller.isRunning ? 'Running...' : 'Run'}
          </button>
        </div>
      </aside>
      <div className="flex min-h-0 min-w-0 flex-1 flex-col">
        <PanelHeader title={labels.result} description={labels.resultDescription} />
        <div className="min-h-0 flex-1 px-5 py-5">
          <div className="h-full rounded-md border border-dashed border-zinc-200 bg-zinc-50 p-4 dark:border-zinc-800 dark:bg-zinc-900/30">
            {controller.isRunning ? (
              <CenteredMessage message={labels.running} />
            ) : controller.errorMessage ? (
              <ToolErrorView message={controller.errorMessage} />
            ) : controller.result ? (
              <ToolResultView result={controller.result} failure={labels.failure} />
            ) : (
              <pre className="whitespace-pre-wrap break-words text-xs leading-6 text-zinc-800 dark:text-zinc-300">{JSON.stringify({}, null, 2)}</pre>
            )}
          </div>
        </div>
      </div>
    </section>
  );
}

function PanelHeader({ title, description }: { title: string; description?: string }) {
  return (
    <div className="border-b border-zinc-200 px-5 py-4 dark:border-zinc-800">
      <div className="text-sm font-semibold text-zinc-950 dark:text-zinc-100">{title}</div>
      {description ? <p className="mt-1 text-sm leading-6 text-zinc-600 dark:text-zinc-400">{description}</p> : null}
    </div>
  );
}

function CenteredMessage({ message }: { message: string }) {
  return <div className="flex h-full items-center justify-center text-sm text-zinc-500">{message}</div>;
}

function ToolInputField({ name, schema, required, value, disabled, error, onChange }: { name: string; schema: ToolJsonSchema; required: boolean; value?: ToolInputValue; disabled: boolean; error?: string; onChange: (value: ToolInputValue) => void }) {
  const label = schema.title || formatDisplayName(name);
  if ((schema.type ?? 'string') === 'boolean') {
    return <label className="flex items-center gap-3 rounded-md border border-zinc-200 bg-zinc-50 px-4 py-3 dark:border-zinc-800 dark:bg-zinc-900/40"><input type="checkbox" checked={Boolean(value)} disabled={disabled} onChange={(event) => onChange(event.target.checked)} className="size-4 rounded border-zinc-300 text-zinc-950 disabled:cursor-not-allowed disabled:opacity-50 dark:border-zinc-700" /><span className="text-sm font-medium text-zinc-900 dark:text-zinc-100">{label}{required ? <span className="ml-1 text-red-500">*</span> : null}</span></label>;
  }
  const inputType = schema.type === 'integer' || schema.type === 'number' ? 'number' : 'text';
  return (
    <label className="block">
      <div className="mb-2 text-sm font-medium text-zinc-900 dark:text-zinc-100">{label}{required ? <span className="ml-1 text-red-500">*</span> : null}</div>
      <input type={inputType} value={String(value ?? '')} disabled={disabled} onChange={(event) => onChange(event.target.value)} aria-invalid={Boolean(error)} className={`h-10 w-full rounded-md border bg-white px-3 text-sm text-zinc-950 outline-none transition disabled:cursor-not-allowed disabled:opacity-60 dark:bg-zinc-950/40 dark:text-zinc-100 ${error ? 'border-red-300 dark:border-red-900/70' : 'border-zinc-200 dark:border-zinc-800'}`} />
      {error ? <div className="mt-1.5 text-xs font-medium text-red-600 dark:text-red-400">{error}</div> : null}
    </label>
  );
}

function ToolResultView({ result, failure }: { result: ToolRunResponse; failure: string }) {
  if (!result.isSuccess) return <ToolErrorView message={result.errorMessage || result.errorCode || failure} />;
  return <pre className="h-full overflow-auto whitespace-pre-wrap break-words text-xs leading-6 text-zinc-800 dark:text-zinc-300">{formatOutputJson(result.outputJson)}</pre>;
}

function ToolErrorView({ message }: { message: string }) {
  return <div className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900/60 dark:bg-red-950/20 dark:text-red-300">{message}</div>;
}

function formatOutputJson(outputJson?: string | null): string {
  if (!outputJson) return '{}';
  try {
    return JSON.stringify(JSON.parse(outputJson), null, 2);
  } catch {
    return outputJson;
  }
}

function formatDisplayName(value: string): string {
  return value.replace(/[-_]+/g, ' ').replace(/([a-z])([A-Z])/g, '$1 $2').trim().split(/\s+/).filter(Boolean).map((part) => part.charAt(0).toUpperCase() + part.slice(1)).join(' ');
}
