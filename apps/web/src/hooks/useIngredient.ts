import { useEffect, useState } from 'react';
import { getIngredient, type IngredientDetail } from '../services/ingredientsApi';

interface IngredientState {
  data: IngredientDetail | null;
  isLoading: boolean;
  error: string | null;
}

export function useIngredient(id: string | null): IngredientState {
  const [result, setResult] = useState<{
    id: string;
    data: IngredientDetail | null;
    error: string | null;
  } | null>(null);

  useEffect(() => {
    if (!id) {
      return;
    }

    const controller = new AbortController();
    void getIngredient(id, controller.signal)
      .then((data) => setResult({ id, data, error: null }))
      .catch((requestError: unknown) => {
        if (!controller.signal.aborted) {
          setResult({
            id,
            data: null,
            error: requestError instanceof Error
              ? requestError.message
              : 'Unable to load ingredient details.',
          });
        }
      });

    return () => controller.abort();
  }, [id]);

  const isCurrentResult = id !== null && result?.id === id;
  return {
    data: isCurrentResult ? result.data : null,
    isLoading: id !== null && !isCurrentResult,
    error: isCurrentResult ? result.error : null,
  };
}
