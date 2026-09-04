import { runTool, type ToolMetadata } from '../../api/agentMetadataApi';
import { getDashboardBasePath } from '../../dashboardConfig';
import { ToolRunView } from '../ToolRun/ToolRunView';
import { useToolRunController } from '../ToolRun/useToolRunController';

/** Renders the execution panel for a registered dashboard tool. */
export function ToolRunPanel({ tool }: { tool: ToolMetadata }) {
  return <ToolRunPanelContent key={JSON.stringify(tool)} tool={tool} />;
}

function ToolRunPanelContent({ tool }: { tool: ToolMetadata }) {
  const controller = useToolRunController(
    tool,
    (input) => runTool(getDashboardBasePath(), tool.name, input),
    { validate: false, failureMessage: 'Tool run failed.' },
  );

  return <ToolRunView controller={controller} schema={tool.inputSchema} hasInput={tool.hasInput} labels={{ input: 'Input Data', inputDescription: 'Provide input values for this tool.', emptyInput: 'This tool does not require input.', result: 'Response', resultDescription: 'Tool output will appear here after execution.', running: 'Running tool...', failure: 'Tool execution failed.' }} />;
}
