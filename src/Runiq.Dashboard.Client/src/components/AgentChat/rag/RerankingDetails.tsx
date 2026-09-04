import type {
  AgentChatRagRerankingMetadata,
  AgentChatRagRerankingOutcome,
} from '../../../types/agentChat';
import { formatRagDuration } from './ragTimeline';

type RerankingDetailsProps = {
  reranking: AgentChatRagRerankingMetadata;
};

/** Presents the safe reranking outcome, aggregate decision, and per-candidate ranks. */
export function RerankingDetails({ reranking }: RerankingDetailsProps) {
  const status = getRerankingStatus(reranking.outcome);

  return (
    <section aria-label="Reranking details" className="min-w-0 rounded-lg border border-[var(--runiq-border-accent)] bg-[var(--runiq-surface-accent)] p-3">
      <div className="flex flex-wrap items-center gap-2">
        <h4 className="font-semibold text-zinc-950 dark:text-zinc-100">Reranking</h4>
        <span className={`inline-flex rounded-full border px-2 py-0.5 text-[11px] font-medium ${status.classes}`}>
          {status.label}
        </span>
      </div>

      <dl className="mt-3 grid min-w-0 grid-cols-1 gap-x-4 gap-y-2 sm:grid-cols-2 lg:grid-cols-3">
        <Detail label="Requested" value={reranking.requested ? 'Yes' : 'No'} />
        <Detail label="Ran" value={reranking.ran ? 'Yes' : 'No'} />
        <Detail label="Duration" value={formatRagDuration(reranking.duration)} />
        <Detail label="Candidates" value={String(reranking.candidateCount)} />
        <Detail label="Aggregate answerability" value={formatAnswerability(reranking.answerability)} />
        <Detail label="Failure policy" value={formatWords(reranking.failurePolicy)} />
        <Detail label="Timed out" value={reranking.timedOut ? 'Yes' : 'No'} />
        {reranking.failureCode ? <Detail label="Failure classification" value={reranking.failureCode} /> : null}
      </dl>

      <CandidateRanks candidates={reranking.candidates} />
    </section>
  );
}

function CandidateRanks({ candidates }: { candidates: AgentChatRagRerankingMetadata['candidates'] }) {
  if (candidates.length === 0) {
    return <p className="mt-3 text-zinc-500">No candidate reranking decisions.</p>;
  }

  return (
    <div className="mt-3 min-w-0 overflow-x-auto">
      <table className="w-full min-w-[32rem] border-separate border-spacing-0 text-left">
        <thead>
          <tr className="text-[11px] uppercase tracking-wide text-zinc-500">
            <th className="border-b border-[var(--runiq-border-accent)] px-2 py-1.5 font-medium">Candidate</th>
            <th className="border-b border-[var(--runiq-border-accent)] px-2 py-1.5 font-medium">Rank change</th>
            <th className="border-b border-[var(--runiq-border-accent)] px-2 py-1.5 font-medium">Relevance</th>
            <th className="border-b border-[var(--runiq-border-accent)] px-2 py-1.5 font-medium">Answerability</th>
          </tr>
        </thead>
        <tbody>
          {candidates.map((candidate) => (
            <tr key={`${candidate.documentId}:${candidate.chunkId}`}>
              <td className="border-b border-[var(--runiq-divider-accent)] px-2 py-2 font-mono text-[11px]">
                <span className="block break-all">{candidate.documentId}</span>
                <span className="block break-all text-zinc-500">{candidate.chunkId}</span>
              </td>
              <td className="border-b border-[var(--runiq-divider-accent)] px-2 py-2 tabular-nums">
                {candidate.originalRank} → {candidate.rerankRank}
              </td>
              <td className="border-b border-[var(--runiq-divider-accent)] px-2 py-2 tabular-nums">
                {(candidate.rerankRelevance * 100).toFixed(1)}%
              </td>
              <td className="border-b border-[var(--runiq-divider-accent)] px-2 py-2">
                {formatAnswerability(candidate.answerability)}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return <div className="min-w-0"><dt className="text-[11px] font-medium uppercase tracking-wide text-zinc-500">{label}</dt><dd className="mt-0.5 break-words text-zinc-900 dark:text-zinc-100">{value}</dd></div>;
}

function getRerankingStatus(outcome: AgentChatRagRerankingOutcome): { label: string; classes: string } {
  switch (outcome) {
    case 'Succeeded':
      return { label: 'Succeeded', classes: 'border-[var(--runiq-status-success-border)] bg-[var(--runiq-status-success-bg)] text-[var(--runiq-status-success-text)]' };
    case 'Fallback':
      return { label: 'Fallback', classes: 'border-[var(--runiq-status-warning-border)] bg-[var(--runiq-status-warning-bg)] text-[var(--runiq-status-warning-text)]' };
    case 'Failed':
      return { label: 'Blocked', classes: 'border-[var(--runiq-status-danger-border)] bg-[var(--runiq-status-danger-bg)] text-[var(--runiq-status-danger-text)]' };
    default:
      return { label: 'Disabled', classes: 'border-[var(--runiq-status-neutral-border)] bg-[var(--runiq-status-neutral-bg)] text-[var(--runiq-status-neutral-text)]' };
  }
}

function formatAnswerability(value: string): string {
  return value === 'NotAnswerable' ? 'Not answerable' : value;
}

function formatWords(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2');
}
