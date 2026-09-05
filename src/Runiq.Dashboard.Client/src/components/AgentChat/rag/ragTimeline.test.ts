import assert from 'node:assert/strict';
import test from 'node:test';

import { parseStreamEventPayload } from '../../../api/agentChatApi.ts';
import type { AgentChatStreamEvent } from '../../../types/agentChat.ts';
import {
  applyRagStreamEvent,
  formatRagDuration,
  getDistinctEffectiveQuery,
  getFailureClassificationLabel,
  getNoContextLabel,
  getRejectionReasonLabel,
} from './ragTimeline.ts';

const basePayload = {
  agentId: 'agent-1',
  conversationId: 'conversation-1',
  correlationId: 'retrieval-1',
  indexName: 'documents',
  originalQuery: 'original question',
  requestedCandidateCount: 10,
};

// Verifies a started event creates one running lifecycle with its safe query metadata.
test('started event creates a running lifecycle', () => {
  const event: AgentChatStreamEvent = {
    type: 'rag_search_started',
    ragSearch: { ...basePayload, effectiveQuery: 'effective question' },
  };

  const result = applyRagStreamEvent([], event);

  assert.equal(result.length, 1);
  assert.equal(result[0]?.status, 'running');
  assert.equal(result[0]?.payload.correlationId, 'retrieval-1');
  assert.equal(result[0]?.payload.effectiveQuery, 'effective question');
});

// Verifies a completed event replaces the matching started lifecycle without changing its timeline position.
test('completed event updates the matching lifecycle', () => {
  const started = applyRagStreamEvent([], startedEvent('retrieval-1'));
  const result = applyRagStreamEvent(started, completedEvent('retrieval-1'));

  assert.equal(result.length, 1);
  assert.equal(result[0]?.status, 'completed');
  if (result[0]?.status === 'completed') {
    assert.equal(result[0].payload.actualCandidateCount, 3);
    assert.equal(result[0].payload.acceptedCount, 2);
    assert.equal(result[0].payload.rejectedCount, 1);
    assert.deepEqual(result[0].payload.selectedResults, [
      { documentId: 'document-1', chunkId: 'chunk-1', contextOrder: 0 },
      { documentId: 'document-2', chunkId: 'chunk-2', contextOrder: 1 },
    ]);
    assert.equal(result[0].payload.rejectedResults[0]?.reason, 'DuplicateContent');
  }
});

// Verifies the runtime's disabled-reranking payload remains a valid completed lifecycle when its failure code is null.
test('completed event accepts a null reranking failure code', () => {
  const started = applyRagStreamEvent([], startedEvent('retrieval-1'));
  const completed = completedEvent('retrieval-1');
  const parsed = parseStreamEventPayload(JSON.stringify({
    ...completed,
    ragSearch: {
      ...completed.ragSearch,
      reranking: {
        requested: false,
        ran: false,
        candidateCount: 0,
        duration: '00:00:00',
        outcome: 'Disabled',
        failurePolicy: 'UseOriginalOrder',
        answerability: 'Unknown',
        candidates: [],
        timedOut: false,
        failureCode: null,
      },
    },
  }));

  assert.ok(parsed);
  const result = applyRagStreamEvent(started, parsed);
  assert.equal(result.length, 1);
  assert.equal(result[0]?.status, 'completed');
});

// Verifies a failed event replaces the matching started lifecycle with classification but no diagnostic fields.
test('failed event updates the matching lifecycle', () => {
  const started = applyRagStreamEvent([], startedEvent('retrieval-1'));
  const result = applyRagStreamEvent(started, failedEvent('retrieval-1'));

  assert.equal(result.length, 1);
  assert.equal(result[0]?.status, 'failed');
  if (result[0]?.status === 'failed') {
    assert.equal(result[0].payload.failureClassification, 'EmbeddingFailed');
    assert.deepEqual(Object.keys(result[0].payload).sort(), [
      'agentId', 'conversationId', 'correlationId', 'duration',
      'failureClassification', 'indexName', 'originalQuery',
      'requestedCandidateCount',
    ]);
  }
});

// Verifies readiness blocking replaces the running row without being classified as retrieval failure or no-context.
test('readiness blocked event creates a structured action-required lifecycle', () => {
  const started = applyRagStreamEvent([], startedEvent('retrieval-1'));
  const parsed = parseStreamEventPayload(JSON.stringify({
    type: 'rag_search_blocked',
    ragSearch: {
      ...basePayload,
      readiness: 'Initializing',
      blockingReason: 'Initializing',
      suggestedAction: 'WaitForIngestion',
      activeOperationState: 'Running',
      progress: { discoveredDocuments: 8, processedDocuments: 3, failedDocuments: 0 },
    },
  }));

  assert.ok(parsed);
  const result = applyRagStreamEvent(started, parsed);
  assert.equal(result.length, 1);
  assert.equal(result[0]?.status, 'blocked');
  if (result[0]?.status === 'blocked') {
    assert.equal(result[0].payload.suggestedAction, 'WaitForIngestion');
    assert.equal(result[0].payload.progress?.processedDocuments, 3);
    assert.equal('failureClassification' in result[0].payload, false);
    assert.equal('noContextReason' in result[0].payload, false);
  }
});

// Verifies two retrieval correlations in one conversation remain distinct and preserve arrival order.
test('separate correlations create separate lifecycle rows', () => {
  const first = applyRagStreamEvent([], startedEvent('retrieval-1'));
  const second = applyRagStreamEvent(first, startedEvent('retrieval-2'));
  const completed = applyRagStreamEvent(second, completedEvent('retrieval-1'));

  assert.deepEqual(completed.map((item) => item.payload.correlationId), ['retrieval-1', 'retrieval-2']);
  assert.deepEqual(completed.map((item) => item.status), ['completed', 'running']);
});

// Verifies terminal lifecycle events render safely even when their started event was not observed.
test('terminal events create standalone lifecycle rows', () => {
  const completed = applyRagStreamEvent([], completedEvent('retrieval-1'));
  const failed = applyRagStreamEvent(completed, failedEvent('retrieval-2'));

  assert.deepEqual(failed.map((item) => item.status), ['completed', 'failed']);
});

// Verifies duration and enum labels use centralized developer-friendly presentation mappings.
test('formatters present duration and lifecycle classifications', () => {
  assert.equal(formatRagDuration('00:00:00.1250000'), '125 ms');
  assert.equal(formatRagDuration('00:00:02.5000000'), '2.5 s');
  assert.equal(getNoContextLabel('BelowRelevanceThreshold'), 'Below relevance threshold');
  assert.equal(getRejectionReasonLabel('ResultLimitExceeded'), 'Result limit exceeded');
  assert.equal(getFailureClassificationLabel('VectorStoreQueryFailed'), 'Vector store query failed');
  assert.equal(getDistinctEffectiveQuery('same query', 'same query'), undefined);
  assert.equal(getDistinctEffectiveQuery('original', undefined), undefined);
  assert.equal(getDistinctEffectiveQuery('original', 'effective'), 'effective');
});

// Verifies non-RAG stream events leave lifecycle state unchanged for RAG-disabled conversations.
test('non-RAG events do not create retrieval lifecycle rows', () => {
  const existing = applyRagStreamEvent([], startedEvent('retrieval-1'));
  const result = applyRagStreamEvent(existing, { type: 'assistant_delta', content: 'answer' });

  assert.deepEqual(result, existing);
});

// Verifies malformed, unknown, and diagnostic-bearing RAG payloads cannot crash the parser or invent lifecycle data.
test('parser safely ignores malformed RAG events', () => {
  assert.equal(parseStreamEventPayload('{invalid'), null);
  assert.equal(parseStreamEventPayload('{"type":"rag_search_completed"}'), null);
  assert.equal(parseStreamEventPayload('{"type":"rag_search_completed","ragSearch":{}}'), null);
  assert.equal(parseStreamEventPayload('{"type":"rag_search_unknown","ragSearch":{}}'), null);
});

// Verifies the completed-event parser preserves valid reranking and not-answerable exclusion metadata.
test('parser accepts reranking metadata and not-answerable context exclusions', () => {
  const parsed = parseStreamEventPayload(JSON.stringify({
    ...completedEvent('retrieval-1'),
    ragSearch: {
      ...completedEvent('retrieval-1').ragSearch,
      acceptedCount: 0,
      selectedResults: [],
      noContextReason: 'NotAnswerable',
      contextExcludedResults: [{
        documentId: 'document-1',
        chunkId: 'chunk-1',
        reason: 'NotAnswerable',
        estimatedTokens: 42,
      }],
      reranking: {
        requested: true,
        ran: true,
        candidateCount: 1,
        duration: '00:00:00.0150000',
        outcome: 'Succeeded',
        failurePolicy: 'UseOriginalOrder',
        answerability: 'NotAnswerable',
        candidates: [{
          documentId: 'document-1',
          chunkId: 'chunk-1',
          originalRank: 1,
          rerankRank: 1,
          rerankRelevance: 0.82,
          answerability: 'NotAnswerable',
        }],
        timedOut: false,
      },
    },
  }));

  assert.ok(parsed && parsed.type === 'rag_search_completed');
  assert.equal(parsed.ragSearch.noContextReason, 'NotAnswerable');
  assert.equal(parsed.ragSearch.contextExcludedResults?.[0]?.reason, 'NotAnswerable');
  assert.equal(parsed.ragSearch.reranking?.candidates[0]?.rerankRelevance, 0.82);
});

// Verifies malformed or unknown reranking fields are rejected instead of entering typed lifecycle state.
test('parser rejects invalid reranking metadata', () => {
  const completed = completedEvent('retrieval-1');
  const invalidOutcome = {
    ...completed,
    ragSearch: {
      ...completed.ragSearch,
      reranking: {
        requested: true,
        ran: true,
        candidateCount: 1,
        duration: '00:00:00.0150000',
        outcome: 'Unexpected',
        failurePolicy: 'UseOriginalOrder',
        answerability: 'Answerable',
        candidates: [],
        timedOut: false,
      },
    },
  };
  const invalidCandidate = {
    ...completed,
    ragSearch: {
      ...completed.ragSearch,
      reranking: {
        requested: true,
        ran: true,
        candidateCount: 1,
        duration: '00:00:00.0150000',
        outcome: 'Succeeded',
        failurePolicy: 'UseOriginalOrder',
        answerability: 'Answerable',
        candidates: [{
          documentId: 'document-1',
          chunkId: 'chunk-1',
          originalRank: 1,
          rerankRank: 1,
          rerankRelevance: 2,
          answerability: 'Answerable',
        }],
        timedOut: false,
      },
    },
  };

  assert.equal(parseStreamEventPayload(JSON.stringify(invalidOutcome)), null);
  assert.equal(parseStreamEventPayload(JSON.stringify(invalidCandidate)), null);
});

function startedEvent(correlationId: string): AgentChatStreamEvent {
  return { type: 'rag_search_started', ragSearch: { ...basePayload, correlationId } };
}

function completedEvent(correlationId: string): AgentChatStreamEvent {
  return {
    type: 'rag_search_completed',
    ragSearch: {
      ...basePayload,
      correlationId,
      actualCandidateCount: 3,
      acceptedCount: 2,
      rejectedCount: 1,
      maximumAcceptedResultCount: 2,
      topRawScore: 0.95,
      topNormalizedRelevance: 0.95,
      duration: '00:00:00.1250000',
      selectedResults: [
        { documentId: 'document-1', chunkId: 'chunk-1', contextOrder: 0 },
        { documentId: 'document-2', chunkId: 'chunk-2', contextOrder: 1 },
      ],
      rejectedResults: [
        { documentId: 'document-1', chunkId: 'chunk-duplicate', reason: 'DuplicateContent' },
      ],
      noContextReason: 'CandidatesRejected',
    },
  };
}

function failedEvent(correlationId: string): AgentChatStreamEvent {
  return {
    type: 'rag_search_failed',
    ragSearch: {
      ...basePayload,
      correlationId,
      duration: '00:00:00.2500000',
      failureClassification: 'EmbeddingFailed',
    },
  };
}
