import { runMcpTool, type McpToolInfo } from '../../api/mcpApi';
import { getDashboardBasePath } from '../../dashboardConfig';
import { ToolRunView } from '../ToolRun/ToolRunView';
import { useToolRunController } from '../ToolRun/useToolRunController';

export function McpToolRunPanel({ tool }: { tool: McpToolInfo }) {
  return <McpToolRunPanelContent key={JSON.stringify(tool)} tool={tool} />;
}

function McpToolRunPanelContent({ tool }: { tool: McpToolInfo }) {
  const controller = useToolRunController(
    tool,
    (input) => runMcpTool(getDashboardBasePath(), tool.name, input),
    { validate: true, failureMessage: 'MCP tool run failed.' },
  );

  return <ToolRunView controller={controller} schema={tool.inputSchema} hasInput={tool.hasInput} labels={{ input: 'Input', emptyInput: 'No input required.', result: 'Result', running: 'Running MCP tool...', failure: 'MCP tool execution failed.' }} />;
}
