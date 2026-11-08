import { useEffect, useState } from 'react';
import {
  getCategories,
  getDepartments,
  type LookupValue,
} from '../services/ingredientsApi';

interface IngredientLookupState {
  categories: LookupValue[];
  departments: LookupValue[];
  isLoading: boolean;
  error: string | null;
  addCategory: (value: LookupValue) => void;
  addDepartment: (value: LookupValue) => void;
}

export function useIngredientLookups(): IngredientLookupState {
  const [result, setResult] = useState<{
    categories: LookupValue[];
    departments: LookupValue[];
    error: string | null;
  } | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    void Promise.all([
      getCategories(controller.signal),
      getDepartments(controller.signal),
    ])
      .then(([categories, departments]) => setResult({ categories, departments, error: null }))
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setResult({
            categories: [],
            departments: [],
            error: error instanceof Error ? error.message : 'Unable to load ingredient lookups.',
          });
        }
      });
    return () => controller.abort();
  }, []);

  return {
    categories: result?.categories ?? [],
    departments: result?.departments ?? [],
    isLoading: result === null,
    error: result?.error ?? null,
    addCategory: (value) => setResult((current) => current && ({
      ...current,
      categories: [...current.categories.filter((item) => item.id !== value.id), value],
    })),
    addDepartment: (value) => setResult((current) => current && ({
      ...current,
      departments: [...current.departments.filter((item) => item.id !== value.id), value],
    })),
  };
}
