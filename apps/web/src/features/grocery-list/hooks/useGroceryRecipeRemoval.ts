import { useRef, useState } from 'react';
import {
  GroceryListApiError,
  removeRecipeGroceryRequirements,
  type GroceryRecipeRemovalCommand,
} from '../services/groceryListApi';
import type { GroceryListState } from './useGroceryList';

export interface RecipeSource {
  id: string;
  name: string;
}

interface PendingRemoval {
  recipe: RecipeSource;
  command: GroceryRecipeRemovalCommand;
  resultVersion: number | null;
}

export function useGroceryRecipeRemoval(grocery: GroceryListState) {
  const [selection, setSelection] = useState<{ recipe: RecipeSource; version: number } | null>(null);
  const [pending, setPending] = useState<PendingRemoval | null>(null);
  const [isRemoving, setIsRemoving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inFlight = useRef(false);
  const busy = isRemoving || (pending !== null && (pending.resultVersion === null
    || grocery.data === null || grocery.data.version < pending.resultVersion));

  async function submit(removal: PendingRemoval) {
    if (inFlight.current) return;
    inFlight.current = true;
    setPending(removal);
    setIsRemoving(true);
    setError(null);
    try {
      const receipt = await removeRecipeGroceryRequirements(removal.recipe.id, removal.command);
      setPending({ ...removal, resultVersion: receipt.resultListVersion });
      grocery.reload();
    } catch (requestError: unknown) {
      if (requestError instanceof GroceryListApiError
        && requestError.status >= 400 && requestError.status < 500) {
        setPending(null);
        if (requestError.status === 404 || requestError.status === 409) grocery.reload();
      }
      setError(requestError instanceof Error ? requestError.message : 'Unable to remove this Recipe.');
    } finally {
      inFlight.current = false;
      setIsRemoving(false);
    }
  }

  function request(recipe: RecipeSource) {
    if (grocery.data && !grocery.isStale && !busy && !inFlight.current) {
      setSelection({ recipe, version: grocery.data.version });
    }
  }

  function confirm() {
    if (!selection || inFlight.current) return;
    setSelection(null);
    void submit({
      recipe: selection.recipe,
      command: { commandId: crypto.randomUUID(), expectedListVersion: selection.version },
      resultVersion: null,
    });
  }

  return {
    recipe: selection?.recipe ?? null,
    request,
    confirm,
    cancel: () => setSelection(null),
    disabled: grocery.isStale || busy,
    error,
    canRetry: Boolean(error && pending?.resultVersion === null && !isRemoving),
    retryName: pending?.recipe.name ?? null,
    retry: () => {
      if (pending && pending.resultVersion === null) void submit(pending);
    },
  };
}
