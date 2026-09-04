import { useMemo, useState } from 'react';
import type { ToolJsonSchema, ToolRunResponse } from '../../api/agentMetadataApi';
import { buildInputObject, createDefaultInputValues, getSchemaProperties, validateInputValues, type ToolInputValue } from './toolInput';

type ToolDefinition = { hasInput: boolean; inputSchema: ToolJsonSchema };

export function useToolRunController(
  tool: ToolDefinition,
  execute: (input: Record<string, unknown>) => Promise<ToolRunResponse>,
  options: { validate: boolean; failureMessage: string },
) {
  const inputProperties = useMemo(() => getSchemaProperties(tool.inputSchema), [tool.inputSchema]);
  const [inputValues, setInputValues] = useState(() => createDefaultInputValues(tool.inputSchema));
  const [result, setResult] = useState<ToolRunResponse | null>(null);
  const [isRunning, setRunning] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});
  const inputPayload = useMemo(
    () => (!tool.hasInput || inputProperties.length === 0 ? {} : buildInputObject(tool.inputSchema, inputValues)),
    [inputProperties.length, inputValues, tool.hasInput, tool.inputSchema],
  );

  function setInput(name: string, value: ToolInputValue) {
    setInputValues((current) => ({ ...current, [name]: value }));
    setValidationErrors((current) => {
      if (!(name in current)) return current;
      const next = { ...current };
      delete next[name];
      return next;
    });
  }

  async function run() {
    const errors = options.validate ? validateInputValues(tool.inputSchema, inputValues) : {};
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
      setResult(await execute(inputPayload));
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : options.failureMessage);
    } finally {
      setRunning(false);
    }
  }

  return { inputProperties, inputValues, result, isRunning, errorMessage, validationErrors, setInput, run };
}
