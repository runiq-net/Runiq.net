import { useMemo, useState } from 'react';

import type {
  ToolJsonSchema,
  ToolRunResponse,
} from '../../api/agentMetadataApi';
import { runMcpTool, type McpToolInfo } from '../../api/mcpApi';
import { getDashboardBasePath } from '../../dashboardConfig';

type McpToolRunPanelProps = {
  tool: McpToolInfo;
};

type ToolInputValue = string | boolean;

export function McpToolRunPanel({ tool }: McpToolRunPanelProps) {
  return <McpToolRunPanelContent key={JSON.stringify(tool)} tool={tool} />;
}

function McpToolRunPanelContent({ tool }: McpToolRunPanelProps) {
  const inputProperties = getSchemaProperties(tool.inputSchema);

  const [inputValues, setInputValues] = useState<Record<string, ToolInputValue>>(
    () => createDefaultInputValues(tool.inputSchema),
  );
  const [result, setResult] = useState<ToolRunResponse | null>(null);
  const [isRunning, setRunning] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});

  const inputPayload = useMemo(() => {
    if (!tool.hasInput || inputProperties.length === 0) {
      return {};
    }

    return buildInputObject(tool.inputSchema, inputValues);
  }, [inputProperties.length, inputValues, tool]);

  async function handleRun() {
    const errors = validateInputValues(tool.inputSchema, inputValues);

    if (Object.keys(errors).length > 0) {
      setValidationErrors(errors);
      setErrorMessage('Fill required fields before running this MCP tool.');
      return;
    }

    try {
      setRunning(true);
      setErrorMessage(null);
      setValidationErrors({});
      setResult(null);

      const response = await runMcpTool(
        getDashboardBasePath(),
        tool.name,
        inputPayload,
      );

      setResult(response);
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : 'MCP tool run failed.',
      );
    } finally {
      setRunning(false);
    }
  }

  return (
    <section className="flex min-h-0 min-w-0 flex-1 rounded-lg border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:shadow-none">
      <aside className="flex w-[320px] shrink-0 flex-col border-r border-zinc-200 dark:border-zinc-800">
        <div className="border-b border-zinc-200 px-5 py-4 dark:border-zinc-800">
          <div className="text-sm font-semibold text-zinc-950 dark:text-zinc-100">
            Input
          </div>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-5 [scrollbar-width:thin] [scrollbar-color:rgb(161_161_170)_transparent] dark:[scrollbar-color:rgb(82_82_91)_transparent] [&::-webkit-scrollbar]:w-1.5 [&::-webkit-scrollbar-track]:bg-transparent [&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-thumb]:bg-zinc-300 dark:[&::-webkit-scrollbar-thumb]:bg-zinc-700">
          {!tool.hasInput || inputProperties.length === 0 ? (
            <div className="rounded-md border border-zinc-200 bg-zinc-50 p-4 text-sm text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900/40 dark:text-zinc-400">
              No input required.
            </div>
          ) : (
            <div className="space-y-4">
              {inputProperties.map(([propertyName, propertySchema]) => (
                <McpToolInputField
                  key={propertyName}
                  name={propertyName}
                  schema={propertySchema}
                  required={isRequired(tool.inputSchema, propertyName)}
                  value={inputValues[propertyName]}
                  disabled={isRunning}
                  error={validationErrors[propertyName]}
                  onChange={(value) => {
                    setInputValues((current) => ({
                      ...current,
                      [propertyName]: value,
                    }));
                    setValidationErrors((current) => {
                      const next = { ...current };
                      delete next[propertyName];
                      return next;
                    });
                  }}
                />
              ))}
            </div>
          )}

          <button
            type="button"
            disabled={isRunning}
            onClick={handleRun}
            className="mt-6 inline-flex h-10 w-full items-center justify-center rounded-md bg-zinc-950 px-4 text-sm font-medium text-white transition hover:bg-zinc-800 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-zinc-100 dark:text-zinc-950 dark:hover:bg-zinc-200"
          >
            {isRunning ? 'Running...' : 'Run'}
          </button>
        </div>
      </aside>

      <div className="flex min-h-0 min-w-0 flex-1 flex-col">
        <div className="border-b border-zinc-200 px-5 py-4 dark:border-zinc-800">
          <div className="text-sm font-semibold text-zinc-950 dark:text-zinc-100">
            Result
          </div>
        </div>

        <div className="min-h-0 flex-1 px-5 py-5">
          <div className="h-full rounded-md border border-dashed border-zinc-200 bg-zinc-50 p-4 dark:border-zinc-800 dark:bg-zinc-900/30">
            {isRunning ? (
              <div className="flex h-full items-center justify-center text-sm text-zinc-500 dark:text-zinc-500">
                Running MCP tool...
              </div>
            ) : errorMessage ? (
              <ToolErrorView message={errorMessage} />
            ) : result ? (
              <ToolResultView result={result} />
            ) : (
              <pre className="whitespace-pre-wrap break-words text-xs leading-6 text-zinc-800 dark:text-zinc-300">
                {JSON.stringify({}, null, 2)}
              </pre>
            )}
          </div>
        </div>
      </div>
    </section>
  );
}

function McpToolInputField({
  name,
  schema,
  required,
  value,
  disabled,
  error,
  onChange,
}: {
  name: string;
  schema: ToolJsonSchema;
  required: boolean;
  value: ToolInputValue | undefined;
  disabled: boolean;
  error?: string;
  onChange: (value: ToolInputValue) => void;
}) {
  const label = schema.title || formatDisplayName(name);
  const type = schema.type ?? 'string';

  if (type === 'boolean') {
    return (
      <label className="flex items-center gap-3 rounded-md border border-zinc-200 bg-zinc-50 px-4 py-3 dark:border-zinc-800 dark:bg-zinc-900/40">
        <input
          type="checkbox"
          checked={Boolean(value)}
          disabled={disabled}
          onChange={(event) => onChange(event.target.checked)}
          className="size-4 rounded border-zinc-300 text-zinc-950 disabled:cursor-not-allowed disabled:opacity-50 dark:border-zinc-700"
        />

        <span className="text-sm font-medium text-zinc-900 dark:text-zinc-100">
          {label}
          {required && <span className="ml-1 text-red-500">*</span>}
        </span>
      </label>
    );
  }

  const inputType =
    type === 'integer' || type === 'number'
      ? 'number'
      : 'text';

  return (
    <label className="block">
      <div className="mb-2 text-sm font-medium text-zinc-900 dark:text-zinc-100">
        {label}
        {required && <span className="ml-1 text-red-500">*</span>}
      </div>

      <input
        type={inputType}
        value={String(value ?? '')}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
        className={[
          'h-10 w-full rounded-md border bg-white px-3 text-sm text-zinc-950 outline-none transition placeholder:text-zinc-400 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-zinc-950/40 dark:text-zinc-100 dark:placeholder:text-zinc-600',
          error
            ? 'border-red-300 focus:border-red-400 dark:border-red-900/70 dark:focus:border-red-700'
            : 'border-zinc-200 focus:border-zinc-400 dark:border-zinc-800 dark:focus:border-zinc-600',
        ].join(' ')}
      />

      {error && (
        <div className="mt-1.5 text-xs font-medium text-red-600 dark:text-red-400">
          {error}
        </div>
      )}
    </label>
  );
}

function ToolResultView({ result }: { result: ToolRunResponse }) {
  if (!result.isSuccess) {
    return (
      <ToolErrorView
        message={result.errorMessage || result.errorCode || 'MCP tool execution failed.'}
      />
    );
  }

  return (
    <pre className="h-full overflow-auto whitespace-pre-wrap break-words text-xs leading-6 text-zinc-800 [scrollbar-width:thin] [scrollbar-color:rgb(161_161_170)_transparent] dark:text-zinc-300 dark:[scrollbar-color:rgb(82_82_91)_transparent] [&::-webkit-scrollbar]:w-1.5 [&::-webkit-scrollbar-track]:bg-transparent [&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-thumb]:bg-zinc-300 dark:[&::-webkit-scrollbar-thumb]:bg-zinc-700">
      {formatOutputJson(result.outputJson)}
    </pre>
  );
}

function ToolErrorView({ message }: { message: string }) {
  return (
    <div className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900/60 dark:bg-red-950/20 dark:text-red-300">
      {message}
    </div>
  );
}

function formatOutputJson(outputJson?: string | null): string {
  if (!outputJson) {
    return '{}';
  }

  try {
    return JSON.stringify(JSON.parse(outputJson), null, 2);
  } catch {
    return outputJson;
  }
}

function getSchemaProperties(
  schema: ToolJsonSchema | undefined,
): [string, ToolJsonSchema][] {
  if (!schema?.properties) {
    return [];
  }

  return Object.entries(schema.properties);
}

function isRequired(schema: ToolJsonSchema | undefined, propertyName: string): boolean {
  return Boolean(schema?.required?.includes(propertyName));
}

function createDefaultInputValues(
  schema: ToolJsonSchema | undefined,
): Record<string, ToolInputValue> {
  return Object.fromEntries(
    getSchemaProperties(schema).map(([name, propertySchema]) => [
      name,
      propertySchema.type === 'boolean' ? false : '',
    ]),
  );
}

function buildInputObject(
  schema: ToolJsonSchema,
  values: Record<string, ToolInputValue>,
): Record<string, unknown> {
  return Object.fromEntries(
    getSchemaProperties(schema).map(([name, propertySchema]) => {
      const value = values[name];

      if (propertySchema.type === 'integer') {
        return [name, value === '' ? null : Number.parseInt(String(value), 10)];
      }

      if (propertySchema.type === 'number') {
        return [name, value === '' ? null : Number.parseFloat(String(value))];
      }

      if (propertySchema.type === 'boolean') {
        return [name, Boolean(value)];
      }

      return [name, value ?? ''];
    }),
  );
}

function validateInputValues(
  schema: ToolJsonSchema,
  values: Record<string, ToolInputValue>,
): Record<string, string> {
  const errors: Record<string, string> = {};

  for (const [name, propertySchema] of getSchemaProperties(schema)) {
    if (!isRequired(schema, name)) {
      continue;
    }

    const value = values[name];

    if (propertySchema.type === 'boolean') {
      continue;
    }

    if (value === undefined || String(value).trim() === '') {
      errors[name] = 'Required field.';
      continue;
    }

    if (
      (propertySchema.type === 'integer' || propertySchema.type === 'number') &&
      Number.isNaN(Number(value))
    ) {
      errors[name] = 'Enter a valid number.';
    }
  }

  return errors;
}

function formatDisplayName(value: string): string {
  return value
    .replace(/[-_]+/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');
}
