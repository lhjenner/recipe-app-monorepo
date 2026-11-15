import { useEffect, useState } from 'react';
import {
  getGroceryHistory,
  type GroceryHistory,
  type GroceryHistoryQuery,
} from '../services/groceryListApi';

interface GroceryHistoryState {
  data: GroceryHistory | null;
  isLoading: boolean;
  error: string | null;
  reload: () => void;
}

export function useGroceryHistory(query: GroceryHistoryQuery): GroceryHistoryState {
  const { allDates, from, to, page, pageSize } = query;
  const [result, setResult] = useState<{
    key: string;
    data: GroceryHistory | null;
    error: string | null;
  } | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const requestKey = JSON.stringify([allDates, from, to, page, pageSize, reloadToken]);

  useEffect(() => {
    const controller = new AbortController();
    void getGroceryHistory({ allDates, from, to, page, pageSize }, controller.signal)
      .then((data) => setResult({ key: requestKey, data, error: null }))
      .catch((requestError: unknown) => {
        if (!controller.signal.aborted) {
          setResult({
            key: requestKey,
            data: null,
            error: requestError instanceof Error
              ? requestError.message
              : 'Unable to load purchase history. Please try again.',
          });
        }
      });

    return () => controller.abort();
  }, [allDates, from, page, pageSize, requestKey, to]);

  const currentResult = result?.key === requestKey;
  return {
    data: currentResult ? result.data : null,
    isLoading: !currentResult,
    error: currentResult ? result.error : null,
    reload: () => setReloadToken((token) => token + 1),
  };
}
