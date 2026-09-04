import assert from 'node:assert/strict';
import test from 'node:test';
import React from 'react';
import TestRenderer, { act } from 'react-test-renderer';

import type { AgentChatRagRerankingMetadata } from '../../../types/agentChat.ts';
import { RerankingDetails } from './RerankingDetails.tsx';

// Verifies successful reranking exposes execution, timing, rank movement, relevance, and answerability.
test('reranking details presents successful candidate decisions', async () => {
  let renderer: TestRenderer.ReactTestRenderer;
  await act(async () => {
    renderer = TestRenderer.create(React.createElement(RerankingDetails, { reranking: succeededReranking() }));
  });

  const content = renderedText(renderer!);
  assert.match(content, /Succeeded/);
  assert.match(content, /Ran Yes/);
  assert.match(content, /Duration 15 ms/);
  assert.match(content, /Candidates 2/);
  assert.match(content, /Aggregate answerability Answerable/);
  assert.match(content, /2 → 1/);
  assert.match(content, /82\.0%/);
  assert.match(content, /Not answerable/);
});

// Verifies failed reranking is presented as blocked with timeout and safe failure classification.
test('reranking details presents blocked timeout metadata', async () => {
  const reranking: AgentChatRagRerankingMetadata = {
    ...succeededReranking(),
    ran: true,
    outcome: 'Failed',
    answerability: 'Unknown',
    candidates: [],
    timedOut: true,
    failureCode: 'Timeout',
  };
  let renderer: TestRenderer.ReactTestRenderer;
  await act(async () => {
    renderer = TestRenderer.create(React.createElement(RerankingDetails, { reranking }));
  });

  const content = renderedText(renderer!);
  assert.match(content, /Blocked/);
  assert.match(content, /Timed out Yes/);
  assert.match(content, /Failure classification Timeout/);
  assert.match(content, /No candidate reranking decisions/);
});

// Verifies fallback execution remains distinguishable from both success and blocked outcomes.
test('reranking details presents fallback outcome and failure classification', async () => {
  const reranking: AgentChatRagRerankingMetadata = {
    ...succeededReranking(),
    outcome: 'Fallback',
    answerability: 'Unknown',
    failureCode: 'Unavailable',
  };
  let renderer: TestRenderer.ReactTestRenderer;
  await act(async () => {
    renderer = TestRenderer.create(React.createElement(RerankingDetails, { reranking }));
  });

  const content = renderedText(renderer!);
  assert.match(content, /Fallback/);
  assert.match(content, /Aggregate answerability Unknown/);
  assert.match(content, /Failure classification Unavailable/);
});

function succeededReranking(): AgentChatRagRerankingMetadata {
  return {
    requested: true,
    ran: true,
    candidateCount: 2,
    duration: '00:00:00.0150000',
    outcome: 'Succeeded',
    failurePolicy: 'UseOriginalOrder',
    answerability: 'Answerable',
    candidates: [{
      documentId: 'document-1',
      chunkId: 'chunk-1',
      originalRank: 2,
      rerankRank: 1,
      rerankRelevance: 0.82,
      answerability: 'NotAnswerable',
    }],
    timedOut: false,
  };
}

function renderedText(renderer: TestRenderer.ReactTestRenderer): string {
  return renderer.root.findAllByType('section')[0]?.findAllByType('dt')
    .map((detail) => `${nodeText(detail)} ${nodeText(detail.parent?.findByType('dd'))}`)
    .concat(renderer.root.findAllByType('section').map(nodeText))
    .join(' ') ?? '';
}

function nodeText(node: TestRenderer.ReactTestInstance | undefined): string {
  if (!node) return '';
  return node.children.map((child) => typeof child === 'string' ? child : nodeText(child)).join('');
}
