import { useEffect, useState } from 'react';
import { getIngredientList, type IngredientList } from '../services/ingredientsApi';

interface IngredientListParameters {
  search: string;
  page: number;
  pageSize: number;
  archived: boolean;
}

interface IngredientListState {
  data: IngredientList | null;
  isLoading: boolean;
  error: string | null;
  reload: () => void;
}

export function useIngredientList(parameters: IngredientListParameters): IngredientListState {
  const { search, page, pageSize, archived } = parameters;
  const [result, setResult] = useState<{
    key: string;
    data: IngredientList | null;
    error: string | null;
  } | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const requestKey = JSON.stringify([search, page, pageSize, archived, reloadToken]);

  useEffect(() => {
    const controller = new AbortController();

    const timeout = window.setTimeout(() => {
      void getIngredientList({ search, page, pageSize, archived }, controller.signal)
        .then((data) => setResult({ key: requestKey, data, error: null }))
        .catch((requestError: unknown) => {
          if (!controller.signal.aborted) {
            setResult({
              key: requestKey,
              data: null,
              error: requestError instanceof Error
                ? requestError.message
                : 'Unable to load ingredients. Please try again.',
            });
          }
        });
    }, search.trim() ? 300 : 0);

    return () => {
      window.clearTimeout(timeout);
      controller.abort();
    };
  }, [archived, page, pageSize, requestKey, search]);

  const isCurrentResult = result?.key === requestKey;

  return {
    data: isCurrentResult ? result.data : null,
    isLoading: !isCurrentResult,
    error: isCurrentResult ? result.error : null,
    reload: () => setReloadToken((token) => token + 1),
  };
}
