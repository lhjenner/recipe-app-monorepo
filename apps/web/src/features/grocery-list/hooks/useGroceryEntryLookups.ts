import { useEffect, useState } from 'react';
import {
  getCategories,
  getDepartments,
  getSupermarkets,
  type LookupValue,
  type SupermarketChain,
} from '../../ingredients/services/ingredientsApi';

export interface GroceryEntryLookups {
  categories: LookupValue[];
  departments: LookupValue[];
  stores: SupermarketChain[];
  error: string | null;
}

export function useGroceryEntryLookups(): GroceryEntryLookups {
  const [lookups, setLookups] = useState<GroceryEntryLookups>({
    categories: [],
    departments: [],
    stores: [],
    error: null,
  });

  useEffect(() => {
    const controller = new AbortController();
    void Promise.all([
      getCategories(controller.signal),
      getDepartments(controller.signal),
      getSupermarkets(controller.signal),
    ])
      .then(([categories, departments, stores]) => setLookups({
        categories,
        departments,
        stores,
        error: null,
      }))
      .catch((lookupError: unknown) => {
        if (!controller.signal.aborted) {
          setLookups((current) => ({
            ...current,
            error: lookupError instanceof Error
              ? lookupError.message
              : 'Unable to load Ingredient lookup options.',
          }));
        }
      });
    return () => controller.abort();
  }, []);

  return lookups;
}
