import { useEffect, useRef, useState } from 'react';
import {
  getGroceryList,
  type GroceryItem,
  type GroceryListQuery,
  type GroceryList,
} from '../services/groceryListApi';

const POLL_INTERVAL_MS = 15_000;

export interface LocalPurchase {
  version: number;
  itemId: string;
}

export interface LocalUndo {
  kind: 'undo';
  version: number;
  expectedVersion: number;
  item: GroceryItem;
}

export type LocalChange = LocalPurchase | LocalUndo;

function itemSnapshot(items: GroceryList['items']): string {
  return JSON.stringify(items.map((item) => ({
    ...item,
    storeMappings: [...item.storeMappings].sort((a, b) => a.storeId.localeCompare(b.storeId)),
  })));
}

export function isPurchaseOnlyChange(
  previous: GroceryList,
  next: GroceryList,
  purchase: LocalPurchase,
): boolean {
  const purchased = previous.items.find((item) => item.id === purchase.itemId);
  const completed = next.items.find((item) => item.id === purchase.itemId);
  return next.version === purchase.version
    && previous.id === next.id
    && previous.completedItemCount + 1 === next.completedItemCount
    && purchased?.state === 'outstanding'
    && (!completed || completed.state === 'completed')
    && itemSnapshot(previous.items.filter((item) => item.id !== purchase.itemId))
      === itemSnapshot(next.items.filter((item) => item.id !== purchase.itemId));
}

export function isLocalChangeOnly(previous: GroceryList, next: GroceryList, change: LocalChange): boolean {
  if (!('kind' in change)) return isPurchaseOnlyChange(previous, next, change);
  if (previous.id !== next.id || previous.version !== change.expectedVersion || next.version !== change.version) {
    return false;
  }
  const { item } = change;
  const previousItem = previous.items.find((candidate) => candidate.id === item.id);
  const restored = next.items.find((candidate) => candidate.id === item.id);
  if ((!previousItem || previousItem.state === 'completed')
    && restored?.state === 'outstanding'
    && previous.completedItemCount - 1 === next.completedItemCount) {
    return itemSnapshot(previous.items.filter((candidate) => candidate.id !== item.id))
      === itemSnapshot(next.items.filter((candidate) => candidate.id !== item.id));
  }

  if (previous.completedItemCount !== next.completedItemCount) return false;
  return previous.items.some((candidate) => {
    if (candidate.id === item.id || candidate.state !== 'outstanding'
      || candidate.ingredientId !== item.ingredientId
      || (item.ingredientId === null && candidate.name.toLowerCase() !== item.name.toLowerCase())
      || candidate.dimension !== item.dimension || candidate.countLabel !== item.countLabel
      || candidate.purchaseUnit !== item.purchaseUnit) return false;
    const updated = next.items.find((nextItem) => nextItem.id === candidate.id);
    return updated?.state === 'outstanding'
      && updated.outstandingPurchaseQuantity > candidate.outstandingPurchaseQuantity
      && itemSnapshot(previous.items.filter((value) => value.id !== candidate.id))
        === itemSnapshot(next.items.filter((value) => value.id !== candidate.id));
  });
}

export interface GroceryListState {
  data: GroceryList | null;
  isLoading: boolean;
  error: string | null;
  isStale: boolean;
  lastSuccessfulAt: Date | null;
  updateCount: number;
  lastChange: { previous: GroceryList; next: GroceryList } | null;
  reload: (localChange?: LocalChange) => void;
}

export function useGroceryList(query: GroceryListQuery = {}): GroceryListState {
  const includeCompleted = query.includeCompleted ?? false;
  const from = query.from;
  const to = query.to;
  const [data, setData] = useState<GroceryList | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isStale, setIsStale] = useState(false);
  const [lastSuccessfulAt, setLastSuccessfulAt] = useState<Date | null>(null);
  const [updateCount, setUpdateCount] = useState(0);
  const [lastChange, setLastChange] = useState<{ previous: GroceryList; next: GroceryList } | null>(null);
  const refreshRef = useRef<((localChange?: LocalChange) => void) | null>(null);
  const hasDataRef = useRef(false);

  useEffect(() => {
    let active = true;
    let intervalId: number | null = null;
    let controller: AbortController | null = null;
    let requestInFlight = false;
    let previousList: GroceryList | null = null;
    let localChange: LocalChange | undefined;

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
        const next = await getGroceryList(requestController.signal, { includeCompleted, from, to });
        if (!active || requestController.signal.aborted) {
          return;
        }

        const snapshot = (list: GroceryList) => JSON.stringify({
          id: list.id, completedItemCount: list.completedItemCount, items: itemSnapshot(list.items),
        });
        if (previousList && snapshot(previousList) !== snapshot(next)) {
          if (!localChange || !isLocalChangeOnly(previousList, next, localChange)) {
            setLastChange({ previous: previousList, next });
            setUpdateCount((count) => count + 1);
          }
        }
        localChange = undefined;
        previousList = next;
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

    refreshRef.current = (change) => {
      if (change) {
        localChange = change;
        retireRequest();
      }
      void refresh();
    };
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
      document.removeEventListener('visibilitychange', handleVisibilityChange);
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('pageshow', handlePageShow);
    };
  }, [includeCompleted, from, to]);

  return {
    data,
    isLoading,
    error,
    isStale,
    lastSuccessfulAt,
    updateCount,
    lastChange,
    reload: (change) => refreshRef.current?.(change),
  };
}
