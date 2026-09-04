export type AgentToolMetadata = {
  name: string;
  displayName?: string;
  description?: string;
  inputType?: string;
  outputType?: string;
};

export type AgentMetadata = {
  id: string;
  name: string;
  instructions?: string;
  model?: string;
  reasoningEffort?: string;
  verbosity?: string;
  rag: {
    enabled: boolean;
    indexName?: string | null;
    executionMode?: string | null;
    reranking: {
      enabled: boolean;
      maximumCandidates: number;
      timeout: string;
      failurePolicy: 'Fail' | 'UseOriginalOrder';
    };
  };
  tools?: AgentToolMetadata[];
};

export type ToolAttachedAgentMetadata = {
  id: string;
  name: string;
};

export type ToolMetadata = {
  name: string;
  displayName: string;
  description?: string;
  typeName: string;
  inputType: string;
  outputType: string;
  hasInput: boolean;
  inputSchema: ToolJsonSchema;
  outputSchema: ToolJsonSchema;
  attachedAgents: ToolAttachedAgentMetadata[];
};

export type ToolJsonSchema = {
  type?: string;
  title?: string;
  properties?: Record<string, ToolJsonSchema>;
  required?: string[];
  enum?: string[];
  items?: ToolJsonSchema;
};

export type ToolRunResponse = {
  isSuccess: boolean;
  outputJson?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
};

export type WorkflowStepMetadata = {
  id: string;
  agentType: string;
  agentName: string;
  successStepId?: string | null;
  failureBehavior: string;
  failureStepId?: string | null;
};

export type WorkflowMetadata = {
  id: string;
  name: string;
  startStepId?: string | null;
  stepCount: number;
  steps: WorkflowStepMetadata[];
};

export type WorkflowStepRunResult = {
  stepId: string;
  agentName: string;
  agentType: string;
  status: string;
  input?: string | null;
  output?: string | null;
  errorMessage?: string | null;
  toolCalls: WorkflowToolCallRunResult[];
};

export type WorkflowToolCallRunResult = {
  toolCallId?: string | null;
  toolName?: string | null;
  status: string;
  argumentsJson?: string | null;
  outputJson?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
  startedAt?: string | null;
  completedAt?: string | null;
  durationMs?: number | null;
};

export type WorkflowRunResponse = {
  workflowId: string;
  status: string;
  finalOutput?: string | null;
  errorMessage?: string | null;
  steps: WorkflowStepRunResult[];
};

export async function runTool(
  basePath: string,
  toolName: string,
  input: Record<string, unknown>,
): Promise<ToolRunResponse> {
  const response = await fetch(
    `${basePath}/api/tools/${encodeURIComponent(toolName)}/run`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        input,
      }),
    },
  );

  if (!response.ok) {
    throw new Error(`Tool run failed with status ${response.status}.`);
  }

  return response.json() as Promise<ToolRunResponse>;
}

export async function getAgents(basePath: string): Promise<AgentMetadata[]> {
  const response = await fetch(`${basePath}/metadata/agents`);

  if (!response.ok) {
    throw new Error('Agents metadata could not be loaded.');
  }

  return response.json() as Promise<AgentMetadata[]>;
}

export async function getTools(basePath: string): Promise<ToolMetadata[]> {
  const response = await fetch(`${basePath}/metadata/tools`);

  if (!response.ok) {
    throw new Error('Tools metadata could not be loaded.');
  }

  return response.json() as Promise<ToolMetadata[]>;
}

export async function getWorkflows(
  basePath: string,
): Promise<WorkflowMetadata[]> {
  const response = await fetch(`${basePath}/api/workflows`);

  if (!response.ok) {
    throw new Error('Workflows metadata could not be loaded.');
  }

  return response.json() as Promise<WorkflowMetadata[]>;
}

export async function getWorkflow(
  basePath: string,
  workflowId: string,
): Promise<WorkflowMetadata> {
  const response = await fetch(
    `${basePath}/api/workflows/${encodeURIComponent(workflowId)}`,
  );

  if (!response.ok) {
    throw new Error('Workflow metadata could not be loaded.');
  }

  return response.json() as Promise<WorkflowMetadata>;
}

export async function runWorkflow(
  basePath: string,
  workflowId: string,
  input: string,
): Promise<WorkflowRunResponse> {
  const response = await fetch(
    `${basePath}/api/workflows/${encodeURIComponent(workflowId)}/run`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ input }),
    },
  );

  if (!response.ok) {
    throw new Error(`Workflow run failed with status ${response.status}.`);
  }

  return response.json() as Promise<WorkflowRunResponse>;
}
