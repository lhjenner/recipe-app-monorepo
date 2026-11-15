import { useEffect, useRef, useState } from 'react';
import {
  getGroceryList,
  type GroceryList,
} from '../services/groceryListApi';

const POLL_INTERVAL_MS = 15_000;
const CHANGE_NOTICE_MS = 4_000;

export interface GroceryListState {
  data: GroceryList | null;
  isLoading: boolean;
  error: string | null;
  isStale: boolean;
  lastSuccessfulAt: Date | null;
  updated: boolean;
  reload: () => void;
}

export function useGroceryList(): GroceryListState {
  const [data, setData] = useState<GroceryList | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isStale, setIsStale] = useState(false);
  const [lastSuccessfulAt, setLastSuccessfulAt] = useState<Date | null>(null);
  const [updated, setUpdated] = useState(false);
  const refreshRef = useRef<(() => void) | null>(null);
  const hasDataRef = useRef(false);

  useEffect(() => {
    let active = true;
    let intervalId: number | null = null;
    let noticeTimeoutId: number | null = null;
    let controller: AbortController | null = null;
    let requestInFlight = false;
    let previousSnapshot = '';

    function retireRequest() {
      controller?.abort();
      controller = null;
      requestInFlight = false;
    }

    function clearIntervalTimer() {
      if (intervalId !== null) {
        window.clearInterval(intervalId);
        intervalId = null;
      }
    }

    function startInterval() {
      if (intervalId === null && document.visibilityState === 'visible') {
        intervalId = window.setInterval(() => void refresh(), POLL_INTERVAL_MS);
      }
    }

    async function refresh() {
      if (!active || document.visibilityState !== 'visible' || requestInFlight) {
        return;
      }

      requestInFlight = true;
      const requestController = new AbortController();
      controller = requestController;
      setIsLoading(!hasDataRef.current);

      try {
        const next = await getGroceryList(requestController.signal);
        if (!active || requestController.signal.aborted) {
          return;
        }

        const nextSnapshot = JSON.stringify({
          id: next.id,
          completedItemCount: next.completedItemCount,
          items: next.items,
        });
        if (previousSnapshot && previousSnapshot !== nextSnapshot) {
          setUpdated(true);
          if (noticeTimeoutId !== null) {
            window.clearTimeout(noticeTimeoutId);
          }
          noticeTimeoutId = window.setTimeout(() => setUpdated(false), CHANGE_NOTICE_MS);
        }
        previousSnapshot = nextSnapshot;
        hasDataRef.current = true;
        setData(next);
        setError(null);
        setIsStale(false);
        setLastSuccessfulAt(new Date());
      } catch (requestError: unknown) {
        if (active && !requestController.signal.aborted) {
          setError(requestError instanceof Error
            ? requestError.message
            : 'Unable to load the grocery list. Please try again.');
          setIsStale(true);
        }
      } finally {
        if (controller === requestController) {
          controller = null;
          requestInFlight = false;
          if (active) {
            setIsLoading(false);
          }
        }
      }
    }

    function handleVisibilityChange() {
      if (document.visibilityState === 'hidden') {
        clearIntervalTimer();
        retireRequest();
        return;
      }

      void refresh();
      startInterval();
    }

    function handleOnline() {
      retireRequest();
      void refresh();
      startInterval();
    }

    function handlePageShow(event: PageTransitionEvent) {
      if (event.persisted && document.visibilityState === 'visible') {
        void refresh();
        startInterval();
      }
    }

    refreshRef.current = () => void refresh();
    document.addEventListener('visibilitychange', handleVisibilityChange);
    window.addEventListener('online', handleOnline);
    window.addEventListener('pageshow', handlePageShow);
    if (document.visibilityState === 'visible') {
      void refresh();
      startInterval();
    }

    return () => {
      active = false;
      refreshRef.current = null;
      clearIntervalTimer();
      retireRequest();
      if (noticeTimeoutId !== null) {
        window.clearTimeout(noticeTimeoutId);
      }
      document.removeEventListener('visibilitychange', handleVisibilityChange);
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('pageshow', handlePageShow);
    };
  }, []);

  return {
    data,
    isLoading,
    error,
    isStale,
    lastSuccessfulAt,
    updated,
    reload: () => refreshRef.current?.(),
  };
}
