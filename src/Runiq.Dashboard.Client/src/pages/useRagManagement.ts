import { useEffect, useRef, useState } from 'react';
import { cancelRagIngestion, getRagIndex, getRagStatus, listRagIndexes, RagApiError, startRagIngestion, type RagIndexDetail, type RagIndexListItem, type RagRuntimeStatus } from '../api/ragApi';
import { mergeRuntime, pollingDelay, shouldApplyStatus } from './ragManagement';

/** Owns RAG management API calls, command state, selection, and the single polling lifecycle. */
export function useRagManagement() {
  const [indexes, setIndexes] = useState<RagIndexListItem[]>([]);
  const [selectedName, setSelectedName] = useState<string | null>(null);
  const [detail, setDetail] = useState<RagIndexDetail | null>(null);
  const [status, setStatus] = useState<RagRuntimeStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [pageError, setPageError] = useState<string | null>(null);
  const [commandError, setCommandError] = useState<string | null>(null);
  const [command, setCommand] = useState<'start' | 'cancel' | null>(null);
  const sequence = useRef(0);

  useEffect(() => {
    const controller = new AbortController();
    void listRagIndexes(controller.signal).then((result) => {
      const requested = new URLSearchParams(window.location?.search ?? '').get('index');
      const initial = requested && result.some((item) => item.name === requested) ? requested : result[0]?.name ?? null;
      setIndexes(result); setDetailLoading(result.length > 0); setSelectedName(initial); setPageError(null);
    }).catch((error: unknown) => { if (!controller.signal.aborted) setPageError(safeError(error, 'Registered RAG indexes could not be loaded.')); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, []);

  useEffect(() => {
    if (!selectedName) return;
    const controller = new AbortController();
    void getRagIndex(selectedName, controller.signal).then((result) => { setDetail(result); setStatus(result.runtime); })
      .catch((error: unknown) => { if (!controller.signal.aborted) setCommandError(safeError(error, 'Index details could not be loaded.')); })
      .finally(() => { if (!controller.signal.aborted) setDetailLoading(false); });
    return () => controller.abort();
  }, [selectedName]);

  useEffect(() => {
    if (!selectedName) return;
    let timer: number | undefined; let controller: AbortController | undefined; let disposed = false; let generation = 0;
    const schedule = () => {
      const delay = pollingDelay(status, document.visibilityState === 'visible');
      if (delay === null || disposed) return;
      timer = window.setTimeout(async () => {
        const currentGeneration = generation; controller = new AbortController(); const requestSequence = ++sequence.current; setRefreshing(true);
        try {
          const next = await getRagStatus(selectedName, controller.signal);
          if (!disposed && shouldApplyStatus(requestSequence, sequence.current)) { setStatus(next); setIndexes((current) => mergeRuntime(current, next)); }
        } catch (error) {
          if (!controller.signal.aborted && !disposed) setCommandError(safeError(error, 'Runtime status could not be refreshed.'));
        } finally {
          if (!disposed && currentGeneration === generation) { setRefreshing(false); schedule(); }
        }
      }, delay);
    };
    const visibilityChanged = () => { generation += 1; if (timer) window.clearTimeout(timer); controller?.abort(); controller = undefined; setRefreshing(false); schedule(); };
    document.addEventListener('visibilitychange', visibilityChanged); schedule();
    return () => { disposed = true; if (timer) window.clearTimeout(timer); controller?.abort(); document.removeEventListener('visibilitychange', visibilityChanged); };
  }, [selectedName, status]);

  const selectIndex = (name: string) => {
    if (name === selectedName) return;
    sequence.current += 1; setDetailLoading(true); setCommandError(null); setDetail(null); setStatus(null); setSelectedName(name);
  };

  const runCommand = async (kind: 'start' | 'cancel') => {
    if (!selectedName) return;
    sequence.current += 1; setCommand(kind); setCommandError(null);
    try {
      await (kind === 'start' ? startRagIngestion(selectedName) : cancelRagIngestion(selectedName));
      const next = await getRagStatus(selectedName); setStatus(next); setIndexes((current) => mergeRuntime(current, next));
    } catch (error) {
      if (error instanceof RagApiError && error.status === 409 && error.conflict?.activeOperation && status) {
        const next = { ...status, activeOperation: error.conflict.activeOperation }; setStatus(next); setIndexes((items) => mergeRuntime(items, next));
      }
      setCommandError(safeError(error, `Ingestion could not be ${kind === 'start' ? 'started' : 'cancelled'}.`));
    } finally { setCommand(null); }
  };

  return { indexes, selectedName, detail, status, loading, detailLoading, refreshing, pageError, commandError, command, selectIndex, runCommand };
}

function safeError(error: unknown, fallback: string) { return error instanceof Error ? error.message : fallback; }
