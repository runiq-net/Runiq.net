import { type ReactNode } from 'react';
import { AlertTriangle, Ban, CheckCircle2, CircleDot, Database, FileStack, LoaderCircle, Play, RefreshCw, ShieldAlert, Square, XCircle } from 'lucide-react';
import { type RagIndexDetail, type RagIndexListItem, type RagOperation, type RagOperationState, type RagReadiness, type RagRuntimeStatus } from '../api/ragApi';
import { formatDuration, operationReasonLabels, operationStateLabels, progressValue, readinessLabels, summarizeIndexes } from './ragManagement';
import { useRagManagement } from './useRagManagement';

/** Composes RAG management presentation with index selection, commands, and a single polling lifecycle. */
export function RagPage() {
  const { indexes, selectedName, detail, status, loading, detailLoading, refreshing, pageError, commandError, command, selectIndex, runCommand } = useRagManagement();

  if (loading) return <LoadingState />;
  if (pageError) return <PageError message={pageError} onRetry={() => window.location.reload()} />;
  if (indexes.length === 0) return <EmptyState />;

  const summary = summarizeIndexes(indexes);
  const activeIndexes = indexes.filter((index) => index.activeOperation);

  return (
    <div className="min-w-0 space-y-6">
      <SummaryCards summary={summary} />
      <ActiveIngestions indexes={activeIndexes} onSelect={selectIndex} />
      <div className="grid min-w-0 gap-6 xl:grid-cols-[minmax(0,1.15fr)_minmax(360px,0.85fr)]">
        <IndexList indexes={indexes} selectedName={selectedName} onSelect={selectIndex} />
        <IndexDetailPanel detail={detail} status={status} loading={detailLoading} refreshing={refreshing} command={command} error={commandError} onStart={() => void runCommand('start')} onCancel={() => void runCommand('cancel')} />
      </div>
    </div>
  );
}

function SummaryCards({ summary }: { summary: ReturnType<typeof summarizeIndexes> }) {
  const cards = [
    ['Total indexes', summary.total, Database], ['Ready indexes', summary.ready, CheckCircle2], ['Initializing', summary.initializing, LoaderCircle],
    ['Degraded', summary.degraded, AlertTriangle], ['Failed', summary.failed, XCircle], ['Active ingestions', summary.active, RefreshCw],
  ] as const;
  return <section aria-label="RAG summary" className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-6">
    {cards.map(([label, value, Icon]) => <div key={label} className="min-w-0 rounded-xl border border-zinc-200 bg-white p-4 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/50 dark:shadow-none"><div className="flex items-center justify-between gap-3"><span className="text-xs font-medium text-zinc-500">{label}</span><Icon className="size-4 text-zinc-400" aria-hidden="true" /></div><div className="mt-3 text-2xl font-semibold tabular-nums">{value}</div></div>)}
  </section>;
}

function ActiveIngestions({ indexes, onSelect }: { indexes: RagIndexListItem[]; onSelect: (name: string) => void }) {
  return <section className="rounded-xl border border-zinc-200 bg-white p-5 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/50 dark:shadow-none">
    <SectionTitle title="Active ingestion" description="Managed ingestion operations currently running or cancelling." count={indexes.length} />
    {indexes.length === 0 ? <p className="mt-4 text-sm text-zinc-500">No ingestion operations are active.</p> : <div className="mt-4 grid gap-4 2xl:grid-cols-2">{indexes.map((index) => <ActiveOperationCard key={index.name} index={index} onSelect={onSelect} />)}</div>}
  </section>;
}

function ActiveOperationCard({ index, onSelect }: { index: RagIndexListItem; onSelect: (name: string) => void }) {
  const operation = index.activeOperation!;
  const progress = operation.progress;
  const percentage = progressValue(progress.processedDocuments, progress.discoveredDocuments);
  return <article className="min-w-0 rounded-xl border border-zinc-200 bg-zinc-50/60 p-4 dark:border-zinc-800 dark:bg-zinc-900/40">
    <div className="flex flex-wrap items-start justify-between gap-3"><button type="button" onClick={() => onSelect(index.name)} className="min-w-0 text-left font-semibold hover:underline focus-visible:outline-2 focus-visible:outline-emerald-500"><span className="block truncate">{index.name}</span><span className="mt-1 block truncate font-mono text-xs font-normal text-zinc-500" title={operation.operationId}>{shortId(operation.operationId)}</span></button><div className="flex gap-2"><ReadinessBadge value={index.readiness} /><OperationBadge value={operation.state} /></div></div>
    <div className="mt-4"><div className="flex justify-between gap-3 text-xs text-zinc-500"><span>{operationReasonLabels[operation.reason]} · {formatDate(operation.startedAt)}</span><span>{formatElapsed(operation)}</span></div><ProgressBar value={percentage} /></div>
    <Metrics progress={progress} />
    {(progress.currentSourceIdentity || progress.currentDocumentIdentity) && <div className="mt-3 grid gap-2 text-xs sm:grid-cols-2"><Identity label="Current source" value={progress.currentSourceIdentity} /><Identity label="Current document" value={progress.currentDocumentIdentity} /></div>}
  </article>;
}

function IndexList({ indexes, selectedName, onSelect }: { indexes: RagIndexListItem[]; selectedName: string | null; onSelect: (name: string) => void }) {
  return <section className="min-w-0 rounded-xl border border-zinc-200 bg-white p-5 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/50 dark:shadow-none"><SectionTitle title="Registered indexes" description="Read-only index registrations and their latest runtime state." count={indexes.length} />
    <div className="mt-4 space-y-2">{indexes.map((index) => <button type="button" key={index.name} aria-pressed={selectedName === index.name} onClick={() => onSelect(index.name)} className={`w-full min-w-0 rounded-xl border p-4 text-left transition focus-visible:outline-2 focus-visible:outline-emerald-500 ${selectedName === index.name ? 'border-emerald-500 bg-emerald-50/60 dark:border-emerald-700 dark:bg-emerald-950/20' : 'border-zinc-200 hover:border-zinc-400 dark:border-zinc-800 dark:hover:border-zinc-600'}`}>
      <div className="flex flex-wrap items-start justify-between gap-3"><div className="min-w-0"><div className="truncate font-semibold">{index.name}</div><div className="mt-1 text-xs text-zinc-500">{index.sourceCount} source{index.sourceCount === 1 ? '' : 's'} · {formatStrategy(index.configuration.ingestionStrategy)}</div></div><div className="flex flex-wrap gap-2"><ReadinessBadge value={index.readiness} />{index.activeOperation && <OperationBadge value={index.activeOperation.state} />}</div></div>
      <div className="mt-3 grid min-w-0 gap-2 text-xs text-zinc-600 sm:grid-cols-2 dark:text-zinc-400"><span className="truncate">Store: {index.configuration.vectorStoreReference}</span><span className="truncate">Embedding: {index.configuration.embeddingReference}</span><span>Last operation: {index.lastOperation ? operationStateLabels[index.lastOperation.state] : 'None'}</span><span>{index.lastOperation ? formatOperationTime(index.lastOperation) : 'No operation recorded'}</span></div>
    </button>)}</div>
  </section>;
}

type DetailProps = { detail: RagIndexDetail | null; status: RagRuntimeStatus | null; loading: boolean; refreshing: boolean; command: 'start' | 'cancel' | null; error: string | null; onStart: () => void; onCancel: () => void };
function IndexDetailPanel({ detail, status, loading, refreshing, command, error, onStart, onCancel }: DetailProps) {
  if (loading) return <aside className="min-w-0 rounded-xl border border-zinc-200 bg-white p-5 dark:border-zinc-800 dark:bg-zinc-950/50" aria-busy="true"><div className="h-5 w-44 animate-pulse rounded bg-zinc-200 dark:bg-zinc-800" /><div className="mt-5 h-64 animate-pulse rounded-xl bg-zinc-100 dark:bg-zinc-900" /></aside>;
  if (!detail || !status) return <aside className="rounded-xl border border-zinc-200 bg-white p-5 text-sm text-zinc-500 dark:border-zinc-800 dark:bg-zinc-950/50">Select an index to inspect its configuration and runtime.</aside>;
  const active = status.activeOperation;
  const runtimeOperation = active ?? status.lastOperation;
  return <aside className="min-w-0 space-y-4 rounded-xl border border-zinc-200 bg-white p-5 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/50 dark:shadow-none">
    <div className="flex flex-wrap items-start justify-between gap-3"><div className="min-w-0"><div className="flex items-center gap-2"><h2 className="truncate text-lg font-semibold">{detail.overview.name}</h2>{refreshing && <LoaderCircle className="size-4 animate-spin text-zinc-400" aria-label="Refreshing status" />}</div><p className="mt-1 text-xs text-zinc-500">Last updated {formatDate(status.lastUpdatedAt)}</p></div><div className="flex gap-2"><ReadinessBadge value={status.readiness} />{active && <OperationBadge value={active.state} />}</div></div>
    {error && <div role="alert" className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800 dark:border-amber-900 dark:bg-amber-950/30 dark:text-amber-200">{error}</div>}
    <div className="flex flex-wrap gap-2"><button type="button" onClick={onStart} disabled={Boolean(active) || command !== null} className="inline-flex items-center gap-2 rounded-lg bg-zinc-950 px-3.5 py-2 text-sm font-medium text-white disabled:cursor-not-allowed disabled:opacity-50 dark:bg-white dark:text-zinc-950"><Play className="size-4" aria-hidden="true" />{command === 'start' ? 'Starting…' : 'Start ingestion'}</button>{active && <button type="button" onClick={onCancel} disabled={command !== null || active.state === 'Cancelling'} className="inline-flex items-center gap-2 rounded-lg border border-red-300 px-3.5 py-2 text-sm font-medium text-red-700 disabled:cursor-not-allowed disabled:opacity-50 dark:border-red-900 dark:text-red-300"><Square className="size-4" aria-hidden="true" />{command === 'cancel' ? 'Cancelling…' : 'Cancel ingestion'}</button>}</div>
    <DetailSection title="Overview"><KeyValue label="Index name" value={detail.overview.name} mono /><KeyValue label="Readiness" value={readinessLabels[status.readiness]} /><KeyValue label="Active operation" value={active ? operationStateLabels[active.state] : 'None'} /><KeyValue label="Last operation" value={status.lastOperation ? operationStateLabels[status.lastOperation.state] : 'None'} /></DetailSection>
    <DetailSection title="Sources"><div className="space-y-3">{detail.sources.map((source) => <div key={source.identity} className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-800"><div className="flex justify-between gap-3 text-sm"><span className="font-medium">{source.type}</span><span className="min-w-0 truncate text-zinc-500">{source.displayValue}</span></div><Identity label="Safe identity" value={source.identity} /></div>)}</div></DetailSection>
    <DetailSection title="Configuration"><KeyValue label="Ingestion strategy" value={formatStrategy(detail.configuration.ingestionStrategy)} />{detail.configuration.ingestionStrategy === 'Scheduled' && <KeyValue label="Schedule" value={detail.configuration.scheduleExpression ?? 'Unavailable'} mono />}<KeyValue label="Vector store" value={detail.configuration.vectorStoreReference} /><KeyValue label="Embedding model" value={detail.configuration.embeddingReference} /><KeyValue label="Chunk size" value={String(detail.configuration.chunkSize)} /><KeyValue label="Chunk overlap" value={String(detail.configuration.chunkOverlap)} /></DetailSection>
    <DetailSection title="Runtime">{runtimeOperation ? <OperationDetails operation={runtimeOperation} /> : <p className="text-sm text-zinc-500">No ingestion operation has been recorded.</p>}{status.lastFailure && <div className="mt-3 rounded-lg border border-red-200 bg-red-50 p-3 text-sm dark:border-red-900 dark:bg-red-950/20"><div className="font-medium text-red-800 dark:text-red-300">{status.lastFailure.code}</div><p className="mt-1 text-red-700 dark:text-red-300/80">{status.lastFailure.message}</p></div>}</DetailSection>
  </aside>;
}

function OperationDetails({ operation }: { operation: RagOperation }) { const percentage = progressValue(operation.progress.processedDocuments, operation.progress.discoveredDocuments); return <div><div className="flex flex-wrap gap-2"><OperationBadge value={operation.state} /><span className="text-sm text-zinc-500">{operationReasonLabels[operation.reason]} · {formatDuration(operation.durationMilliseconds)}</span></div><ProgressBar value={percentage} /><Metrics progress={operation.progress} /></div>; }
function ProgressBar({ value }: { value: number | null }) { return <div className="mt-2 h-2 overflow-hidden rounded-full bg-zinc-200 dark:bg-zinc-800" role="progressbar" aria-label="Ingestion progress" aria-valuemin={0} aria-valuemax={100} {...(value === null ? {} : { 'aria-valuenow': Math.round(value) })}><div className={`h-full rounded-full bg-emerald-500 ${value === null ? 'w-1/3 animate-pulse' : ''}`} style={value === null ? undefined : { width: `${value}%` }} /></div>; }
function Metrics({ progress }: { progress: RagOperation['progress'] }) { const values = [['Discovered', progress.discoveredDocuments], ['Processed', progress.processedDocuments], ['Added', progress.addedDocuments], ['Updated', progress.updatedDocuments], ['Skipped', progress.skippedDocuments], ['Deleted', progress.deletedDocuments], ['Failed', progress.failedDocuments], ['Chunks', progress.producedChunks], ['Embeddings', progress.producedEmbeddings]]; return <div className="mt-3 grid grid-cols-3 gap-2 sm:grid-cols-5">{values.map(([label, value]) => <div key={label} className="rounded-lg bg-zinc-100 p-2 dark:bg-zinc-900"><div className="text-[10px] uppercase tracking-wide text-zinc-500">{label}</div><div className="mt-1 font-mono text-sm font-semibold tabular-nums">{value}</div></div>)}</div>; }

function ReadinessBadge({ value }: { value: RagReadiness }) { return <Badge tone={readinessTone(value)}>{readinessIcon(value)}Readiness: {readinessLabels[value]}</Badge>; }
function OperationBadge({ value }: { value: RagOperationState }) { return <Badge tone={value === 'Completed' ? 'success' : value === 'Running' || value === 'Cancelling' || value === 'Pending' ? 'info' : value === 'PartiallyCompleted' || value === 'Cancelled' ? 'warning' : 'danger'}>{operationIcon(value)}Operation: {operationStateLabels[value]}</Badge>; }
function Badge({ children, tone }: { children: ReactNode; tone: 'neutral' | 'success' | 'info' | 'warning' | 'danger' }) { const styles = { neutral: 'border-zinc-200 bg-zinc-100 text-zinc-700 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-300', success: 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950/30 dark:text-emerald-300', info: 'border-sky-200 bg-sky-50 text-sky-700 dark:border-sky-900 dark:bg-sky-950/30 dark:text-sky-300', warning: 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-950/30 dark:text-amber-300', danger: 'border-red-200 bg-red-50 text-red-700 dark:border-red-900 dark:bg-red-950/30 dark:text-red-300' }; return <span className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-[11px] font-medium ${styles[tone]}`}>{children}</span>; }
function readinessTone(value: RagReadiness): 'neutral' | 'success' | 'info' | 'warning' | 'danger' { return value === 'Ready' ? 'success' : value === 'Initializing' ? 'info' : value === 'Degraded' ? 'warning' : value === 'Failed' ? 'danger' : 'neutral'; }
function readinessIcon(value: RagReadiness) { if (value === 'Ready') return <CheckCircle2 className="size-3.5" aria-hidden="true" />; if (value === 'Initializing') return <LoaderCircle className="size-3.5 animate-spin" aria-hidden="true" />; if (value === 'Degraded') return <AlertTriangle className="size-3.5" aria-hidden="true" />; if (value === 'Failed') return <ShieldAlert className="size-3.5" aria-hidden="true" />; return <CircleDot className="size-3.5" aria-hidden="true" />; }
function operationIcon(value: RagOperationState) { if (value === 'Running' || value === 'Cancelling' || value === 'Pending') return <LoaderCircle className={`size-3.5 ${value !== 'Pending' ? 'animate-spin' : ''}`} aria-hidden="true" />; if (value === 'Completed') return <CheckCircle2 className="size-3.5" aria-hidden="true" />; if (value === 'Cancelled') return <Ban className="size-3.5" aria-hidden="true" />; if (value === 'PartiallyCompleted') return <AlertTriangle className="size-3.5" aria-hidden="true" />; return <XCircle className="size-3.5" aria-hidden="true" />; }
function DetailSection({ title, children }: { title: string; children: ReactNode }) { return <section className="rounded-xl border border-zinc-200 p-4 dark:border-zinc-800"><h3 className="text-xs font-semibold uppercase tracking-[0.14em] text-zinc-500">{title}</h3><div className="mt-3 space-y-2">{children}</div></section>; }
function KeyValue({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) { return <div className="flex min-w-0 items-start justify-between gap-4 text-sm"><span className="shrink-0 text-zinc-500">{label}</span><span className={`min-w-0 break-words text-right font-medium ${mono ? 'font-mono text-xs' : ''}`}>{value}</span></div>; }
function Identity({ label, value }: { label: string; value: string | null }) { return value ? <div className="mt-2 min-w-0"><div className="text-[10px] uppercase tracking-wide text-zinc-500">{label}</div><div className="mt-1 break-all font-mono text-[11px] text-zinc-600 dark:text-zinc-400" title={value}>{value}</div></div> : null; }
function SectionTitle({ title, description, count }: { title: string; description: string; count: number }) { return <div className="flex flex-wrap items-start justify-between gap-3"><div><h2 className="text-base font-semibold">{title}</h2><p className="mt-1 text-sm text-zinc-500">{description}</p></div><span className="rounded-full bg-zinc-100 px-3 py-1 text-xs font-medium dark:bg-zinc-900">{count}</span></div>; }
function LoadingState() { return <div className="space-y-4" aria-busy="true" aria-label="Loading registered RAG indexes"><div className="grid gap-3 sm:grid-cols-3">{[1, 2, 3].map((key) => <div key={key} className="h-24 animate-pulse rounded-xl bg-zinc-200 dark:bg-zinc-900" />)}</div><div className="h-72 animate-pulse rounded-xl bg-zinc-100 dark:bg-zinc-900/60" /></div>; }
function EmptyState() { return <div className="rounded-xl border border-dashed border-zinc-300 bg-white p-10 text-center dark:border-zinc-700 dark:bg-zinc-950/50"><FileStack className="mx-auto size-10 text-zinc-400" aria-hidden="true" /><h2 className="mt-4 font-semibold">No RAG indexes are registered</h2><p className="mx-auto mt-2 max-w-xl text-sm text-zinc-500">Indexes cannot be created from the Dashboard. Register an index in application code with the RAG builder, then restart the application.</p></div>; }
function PageError({ message, onRetry }: { message: string; onRetry: () => void }) { return <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-6 dark:border-red-900 dark:bg-red-950/20"><div className="flex gap-3"><ShieldAlert className="size-5 text-red-600" aria-hidden="true" /><div><h2 className="font-semibold text-red-800 dark:text-red-300">RAG management could not be loaded</h2><p className="mt-1 text-sm text-red-700 dark:text-red-300/80">{message}</p><button type="button" onClick={onRetry} className="mt-4 rounded-lg border border-red-300 px-3 py-2 text-sm font-medium focus-visible:outline-2 focus-visible:outline-red-500">Try again</button></div></div></div>; }
function shortId(value: string) { return value.length > 12 ? `${value.slice(0, 8)}…${value.slice(-4)}` : value; }
function formatDate(value: string) { const date = new Date(value); return Number.isNaN(date.getTime()) ? 'Unavailable' : new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(date); }
function formatOperationTime(operation: RagOperation) { return operation.completedAt ? formatDate(operation.completedAt) : formatDuration(operation.durationMilliseconds); }
function formatElapsed(operation: RagOperation) { return operation.completedAt ? formatDuration(operation.durationMilliseconds) : formatDuration(Math.max(operation.durationMilliseconds, Date.now() - new Date(operation.startedAt).getTime())); }
function formatStrategy(value: string) { return value === 'BackgroundOnStartup' ? 'Background on startup' : value === 'OnStartup' ? 'On startup' : value; }
