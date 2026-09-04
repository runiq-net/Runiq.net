import type { AgentChatResult, AgentChatStreamEvent } from '../types/agentChat';

type SendAgentMessageRequest = {
  basePath: string;
  agentId: string;
  message: string;
};



function trimTrailingSlash(value: string): string {
  return value.endsWith('/') ? value.slice(0, -1) : value;
}

function buildAgentChatUrl(basePath: string, agentId: string): string {
  return `${trimTrailingSlash(basePath)}/api/agents/${encodeURIComponent(agentId)}/chat`;
}

export async function sendAgentMessage({
  basePath,
  agentId,
  message,
}: SendAgentMessageRequest): Promise<AgentChatResult> {
  const response = await fetch(buildAgentChatUrl(basePath, agentId), {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      message,
      responseMode: 'result',
    }),
  });

  const payload = (await response.json()) as AgentChatResult;

  if (!response.ok || payload.isSuccess === false) {
    throw new Error(
      payload.errorMessage ||
      payload.errorCode ||
      `Agent chat request failed. Status: ${response.status}`,
    );
  }

  if (!payload.message) {
    throw new Error('Agent response was empty.');
  }

  return payload;
}

export async function streamAgentMessage(
  { basePath, agentId, message }: SendAgentMessageRequest,
  onEvent: (event: AgentChatStreamEvent) => void,
): Promise<void> {
  const response = await fetch(buildAgentChatUrl(basePath, agentId), {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Accept: 'text/event-stream',
    },
    body: JSON.stringify({
      message,
      responseMode: 'stream',
    }),
  });

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(errorText || `Agent stream request failed. Status: ${response.status}`);
  }

  if (!response.body) {
    throw new Error('Agent stream response body was empty.');
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();

  let buffer = '';

  while (true) {
    const { value, done } = await reader.read();

    if (done) {
      break;
    }

    buffer += decoder.decode(value, { stream: true });

    const events = buffer.split(/\r?\n\r?\n/);
    buffer = events.pop() ?? '';

    for (const event of events) {
      const streamEvent = parseServerSentEvent(event);

      if (streamEvent) {
        onEvent(streamEvent);
      }
    }
  }

  buffer += decoder.decode();

  const finalEvent = parseServerSentEvent(buffer);

  if (finalEvent) {
    onEvent(finalEvent);
  }
}

function parseServerSentEvent(event: string): AgentChatStreamEvent | null {
  const dataLines = event
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line.startsWith('data:'));

  if (dataLines.length === 0) {
    return null;
  }

  for (const line of dataLines) {
    const data = line.replace(/^data:\s?/, '').trim();

    if (!data || data === '[DONE]') {
      continue;
    }

    const streamEvent = parseStreamEventPayload(data);

    if (streamEvent) {
      return streamEvent;
    }
  }

  return null;
}

export function parseStreamEventPayload(data: string): AgentChatStreamEvent | null {
  try {
    const parsed = JSON.parse(data) as Partial<AgentChatStreamEvent>;

    if (!parsed.type) {
      return null;
    }

    const isKnownRagEvent =
      parsed.type === 'rag_search_started' ||
      parsed.type === 'rag_search_completed' ||
      parsed.type === 'rag_search_failed' ||
      parsed.type === 'rag_search_blocked';

    if (parsed.type.startsWith('rag_search_') && !isKnownRagEvent) {
      return null;
    }

    if (
      isKnownRagEvent &&
      !isValidRagPayload(parsed.type, parsed.ragSearch)
    ) {
      return null;
    }

    return {
      type: parsed.type,
      content: parsed.content ?? null,
      toolCallId: parsed.toolCallId ?? null,
      toolName: parsed.toolName ?? null,
      argumentsJson: parsed.argumentsJson ?? null,
      outputJson: parsed.outputJson ?? null,
      errorCode: parsed.errorCode ?? null,
      errorMessage: parsed.errorMessage ?? null,
      ragSearch: parsed.ragSearch ?? null,
    } as AgentChatStreamEvent;


  } catch {
    return null;
  }
}

function isValidRagPayload(
  type: AgentChatStreamEvent['type'],
  payload: unknown,
): boolean {
  if (!isRecord(payload) ||
    !hasString(payload, 'agentId') ||
    !hasString(payload, 'conversationId') ||
    !hasString(payload, 'correlationId') ||
    !hasString(payload, 'indexName') ||
    !hasString(payload, 'originalQuery') ||
    !hasNumber(payload, 'requestedCandidateCount')) {
    return false;
  }

  if (type === 'rag_search_started') {
    return payload.effectiveQuery === undefined || typeof payload.effectiveQuery === 'string';
  }

  if (type === 'rag_search_failed') {
    return hasString(payload, 'duration') && hasString(payload, 'failureClassification');
  }

  if (type === 'rag_search_blocked') {
    return hasString(payload, 'blockingReason') && hasString(payload, 'suggestedAction');
  }

  if (type === 'rag_search_completed') {
    return hasNumber(payload, 'actualCandidateCount') &&
      hasNumber(payload, 'acceptedCount') &&
      hasNumber(payload, 'rejectedCount') &&
      hasNumber(payload, 'maximumAcceptedResultCount') &&
      hasString(payload, 'duration') &&
      Array.isArray(payload.selectedResults) &&
      payload.selectedResults.every(isValidSelectedResult) &&
      Array.isArray(payload.rejectedResults) &&
      payload.rejectedResults.every(isValidRejectedResult) &&
      hasOptionalEnum(payload, 'noContextReason', ragNoContextReasons) &&
      (payload.contextExcludedResults === undefined ||
        (Array.isArray(payload.contextExcludedResults) &&
          payload.contextExcludedResults.every(isValidContextExcludedResult))) &&
      (payload.reranking === undefined || isValidRerankingMetadata(payload.reranking));
  }

  return true;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function hasString(value: Record<string, unknown>, key: string): boolean {
  return typeof value[key] === 'string';
}

function hasNumber(value: Record<string, unknown>, key: string): boolean {
  return typeof value[key] === 'number' && Number.isFinite(value[key]);
}

function isValidSelectedResult(value: unknown): boolean {
  return isRecord(value) &&
    hasString(value, 'documentId') &&
    hasString(value, 'chunkId') &&
    hasNonNegativeInteger(value, 'contextOrder') &&
    hasOptionalFiniteNumber(value, 'rawScore') &&
    hasOptionalFiniteNumber(value, 'normalizedRelevance') &&
    hasOptionalString(value, 'metric') &&
    hasOptionalBoolean(value, 'higherIsBetter');
}

function isValidRejectedResult(value: unknown): boolean {
  return isRecord(value) &&
    hasString(value, 'documentId') &&
    hasString(value, 'chunkId') &&
    hasString(value, 'reason') &&
    hasOptionalFiniteNumber(value, 'rawScore') &&
    hasOptionalFiniteNumber(value, 'normalizedRelevance');
}

const ragNoContextReasons = [
  'NoResults',
  'BelowRelevanceThreshold',
  'CandidatesRejected',
  'ContextBudgetExhausted',
  'NotAnswerable',
] as const;

const contextExclusionReasons = [
  'TokenBudgetExceeded',
  'OverlappingContent',
  'SourceLimitExceeded',
  'SourceDiversityPreference',
  'NotAnswerable',
] as const;

const rerankingOutcomes = ['Disabled', 'Succeeded', 'Fallback', 'Failed'] as const;
const rerankerFailurePolicies = ['Fail', 'UseOriginalOrder'] as const;
const ragAnswerabilities = ['Unknown', 'Answerable', 'NotAnswerable'] as const;

function isValidContextExcludedResult(value: unknown): boolean {
  return isRecord(value) &&
    hasString(value, 'documentId') &&
    hasString(value, 'chunkId') &&
    hasEnum(value, 'reason', contextExclusionReasons) &&
    hasNonNegativeInteger(value, 'estimatedTokens');
}

function isValidRerankingMetadata(value: unknown): boolean {
  return isRecord(value) &&
    hasBoolean(value, 'requested') &&
    hasBoolean(value, 'ran') &&
    hasNonNegativeInteger(value, 'candidateCount') &&
    hasString(value, 'duration') &&
    hasEnum(value, 'outcome', rerankingOutcomes) &&
    hasEnum(value, 'failurePolicy', rerankerFailurePolicies) &&
    hasEnum(value, 'answerability', ragAnswerabilities) &&
    Array.isArray(value.candidates) &&
    value.candidates.every(isValidRerankedCandidate) &&
    hasBoolean(value, 'timedOut') &&
    hasOptionalString(value, 'failureCode');
}

function isValidRerankedCandidate(value: unknown): boolean {
  return isRecord(value) &&
    hasString(value, 'documentId') &&
    hasString(value, 'chunkId') &&
    hasPositiveInteger(value, 'originalRank') &&
    hasPositiveInteger(value, 'rerankRank') &&
    hasNumberInRange(value, 'rerankRelevance', 0, 1) &&
    hasEnum(value, 'answerability', ragAnswerabilities);
}

function hasOptionalFiniteNumber(value: Record<string, unknown>, key: string): boolean {
  return value[key] === undefined || hasNumber(value, key);
}

function hasOptionalString(value: Record<string, unknown>, key: string): boolean {
  return value[key] === undefined || typeof value[key] === 'string';
}

function hasOptionalBoolean(value: Record<string, unknown>, key: string): boolean {
  return value[key] === undefined || typeof value[key] === 'boolean';
}

function hasBoolean(value: Record<string, unknown>, key: string): boolean {
  return typeof value[key] === 'boolean';
}

function hasEnum<T extends string>(
  value: Record<string, unknown>,
  key: string,
  allowedValues: readonly T[],
): boolean {
  return typeof value[key] === 'string' && allowedValues.includes(value[key] as T);
}

function hasOptionalEnum<T extends string>(
  value: Record<string, unknown>,
  key: string,
  allowedValues: readonly T[],
): boolean {
  return value[key] === undefined || hasEnum(value, key, allowedValues);
}

function hasNumberInRange(
  value: Record<string, unknown>,
  key: string,
  minimum: number,
  maximum: number,
): boolean {
  return hasNumber(value, key) && value[key] as number >= minimum && value[key] as number <= maximum;
}

function hasNonNegativeInteger(value: Record<string, unknown>, key: string): boolean {
  const candidate = value[key];
  return typeof candidate === 'number' && Number.isFinite(candidate) && Number.isInteger(candidate) && candidate >= 0;
}

function hasPositiveInteger(value: Record<string, unknown>, key: string): boolean {
  return hasNonNegativeInteger(value, key) && (value[key] as number) > 0;
}
