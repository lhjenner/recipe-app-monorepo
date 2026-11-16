import { useEffect, useState } from 'react';
import {
  getIngredientList,
  type IngredientSummary,
} from '../../ingredients/services/ingredientsApi';

interface GroceryIngredientOptions {
  query: string;
  items: IngredientSummary[];
  error: string | null;
}

export function useGroceryIngredientOptions(name: string): GroceryIngredientOptions {
  const query = name.trim();
  const [result, setResult] = useState<GroceryIngredientOptions>({
    query: '',
    items: [],
    error: null,
  });
  useEffect(() => {
    if (!query) {
      return;
    }

    const controller = new AbortController();
    const timeout = window.setTimeout(() => {
      void getIngredientList({
        search: query,
        page: 1,
        pageSize: 10,
        archived: false,
      }, controller.signal)
        .then((response) => setResult({ query, items: response.items, error: null }))
        .catch((error: unknown) => {
          if (!controller.signal.aborted) {
            setResult({
              query,
              items: [],
              error: error instanceof Error ? error.message : 'Unable to search Ingredients.',
            });
          }
        });
    }, 250);

    return () => {
      window.clearTimeout(timeout);
      controller.abort();
    };
  }, [query]);

  return result.query === query
    ? result
    : { query, items: [], error: null };
}
