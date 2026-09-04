import assert from 'node:assert/strict';
import test from 'node:test';
import React from 'react';
import TestRenderer, { act } from 'react-test-renderer';

import type { AgentMetadata } from '../../../api/agentMetadataApi.ts';
import { RunningBehaviorTab } from './RunningBehaviorTab.tsx';

// Verifies the running behavior inspector exposes the effective reranking configuration.
test('running behavior shows reranking configuration', async () => {
  const agent: AgentMetadata = {
    id: 'agent',
    name: 'Agent',
    rag: {
      enabled: true,
      indexName: 'documents',
      executionMode: 'Grounded',
      reranking: {
        enabled: true,
        maximumCandidates: 8,
        timeout: '00:00:03.5000000',
        failurePolicy: 'UseOriginalOrder',
      },
    },
  };
  let renderer: TestRenderer.ReactTestRenderer;
  await act(async () => {
    renderer = TestRenderer.create(React.createElement(RunningBehaviorTab, {
      agent,
      chatMethod: 'stream',
      onChatMethodChange: () => undefined,
    }));
  });

  const text = nodeText(renderer!.root);
  assert.match(text, /Reranking/);
  assert.match(text, /EnabledYes/);
  assert.match(text, /Maximum candidates8/);
  assert.match(text, /Timeout3\.5 s/);
  assert.match(text, /Failure policyUse Original Order/);
});

function nodeText(node: TestRenderer.ReactTestInstance): string {
  return node.children.map((child) => typeof child === 'string' ? child : nodeText(child)).join('');
}
