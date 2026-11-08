import { useEffect, useState } from 'react';
import {
  getIngredientList,
  IngredientsApiError,
  type IngredientList,
} from '../services/ingredientsApi';

type ListQuery = Parameters<typeof getIngredientList>[0];

function isPageOutOfRange(error: unknown): boolean {
  return error instanceof IngredientsApiError
    && error.status === 400
    && Object.keys(error.fieldErrors).some((field) => field.toLowerCase() === 'page');
}

/**
 * The API rejects a page past the end (e.g. after archiving the last item on the final page).
 * Fall back to page 1 so the caller learns the total count and can move to the real last page.
 */
async function loadList(query: ListQuery, signal: AbortSignal): Promise<IngredientList> {
  try {
    return await getIngredientList(query, signal);
  } catch (error: unknown) {
    if (query.page > 1 && isPageOutOfRange(error)) {
      return getIngredientList({ ...query, page: 1 }, signal);
    }
    throw error;
  }
}

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
      void loadList({ search, page, pageSize, archived }, controller.signal)
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
