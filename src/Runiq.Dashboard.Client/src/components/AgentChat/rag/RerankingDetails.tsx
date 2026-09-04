import type {
  AgentChatRagRerankingMetadata,
  AgentChatRagRerankingOutcome,
} from '../../../types/agentChat';
import { formatRagDuration } from './ragTimeline';

type RerankingDetailsProps = {
  reranking: AgentChatRagRerankingMetadata;
};

export function RerankingDetails({ reranking }: RerankingDetailsProps) {
  const status = getRerankingStatus(reranking.outcome);

  return (
    <section aria-label="Reranking details" className="min-w-0 rounded-lg border border-violet-200 bg-violet-50/60 p-3 dark:border-violet-900/60 dark:bg-violet-950/20">
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
        {reranking.failureCode && <Detail label="Failure classification" value={reranking.failureCode} />}
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
            <th className="border-b border-violet-200 px-2 py-1.5 font-medium dark:border-violet-900/60">Candidate</th>
            <th className="border-b border-violet-200 px-2 py-1.5 font-medium dark:border-violet-900/60">Rank change</th>
            <th className="border-b border-violet-200 px-2 py-1.5 font-medium dark:border-violet-900/60">Relevance</th>
            <th className="border-b border-violet-200 px-2 py-1.5 font-medium dark:border-violet-900/60">Answerability</th>
          </tr>
        </thead>
        <tbody>
          {candidates.map((candidate) => (
            <tr key={`${candidate.documentId}:${candidate.chunkId}`}>
              <td className="border-b border-violet-100 px-2 py-2 font-mono text-[11px] dark:border-violet-950">
                <span className="block break-all">{candidate.documentId}</span>
                <span className="block break-all text-zinc-500">{candidate.chunkId}</span>
              </td>
              <td className="border-b border-violet-100 px-2 py-2 tabular-nums dark:border-violet-950">
                {candidate.originalRank} → {candidate.rerankRank}
              </td>
              <td className="border-b border-violet-100 px-2 py-2 tabular-nums dark:border-violet-950">
                {(candidate.rerankRelevance * 100).toFixed(1)}%
              </td>
              <td className="border-b border-violet-100 px-2 py-2 dark:border-violet-950">
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
      return { label: 'Succeeded', classes: 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900/60 dark:bg-emerald-950/30 dark:text-emerald-300' };
    case 'Fallback':
      return { label: 'Fallback', classes: 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900/60 dark:bg-amber-950/30 dark:text-amber-300' };
    case 'Failed':
      return { label: 'Blocked', classes: 'border-red-200 bg-red-50 text-red-700 dark:border-red-900/60 dark:bg-red-950/30 dark:text-red-300' };
    default:
      return { label: 'Disabled', classes: 'border-zinc-200 bg-zinc-50 text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300' };
  }
}

function formatAnswerability(value: string): string {
  return value === 'NotAnswerable' ? 'Not answerable' : value;
}

function formatWords(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2');
}
