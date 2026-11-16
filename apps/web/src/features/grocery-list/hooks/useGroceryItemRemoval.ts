import { useRef, useState } from 'react';
import { cancelGroceryItem, GroceryListApiError, type GroceryItem } from '../services/groceryListApi';
import type { GroceryListState } from './useGroceryList';

interface RemovalCommand {
  itemId: string;
  itemName: string;
  commandId: string;
  expectedListVersion: number;
}

export function useGroceryItemRemoval(grocery: GroceryListState) {
  const [selection, setSelection] = useState<{ item: GroceryItem; version: number } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isRemoving, setIsRemoving] = useState(false);
  const [pending, setPending] = useState<RemovalCommand | null>(null);
  const inFlight = useRef(false);

  function request(item: GroceryItem) {
    if (grocery.data && !inFlight.current && !pending) {
      setSelection({ item, version: grocery.data.version });
    }
  }

  async function confirm() {
    if (!selection || inFlight.current) {
      return;
    }
    setSelection(null);
    const { item, version } = selection;
    await submit({
      itemId: item.id,
      itemName: item.name,
      commandId: crypto.randomUUID(),
      expectedListVersion: version,
    });
  }

  async function submit(command: RemovalCommand) {
    if (inFlight.current) {
      return;
    }
    inFlight.current = true;
    setIsRemoving(true);
    setPending(command);
    setError(null);
    try {
      await cancelGroceryItem(command.itemId, command.commandId, command.expectedListVersion);
      setPending(null);
      grocery.reload();
    } catch (requestError: unknown) {
      if (requestError instanceof GroceryListApiError
        && requestError.status >= 400 && requestError.status < 500) {
        setPending(null);
        if (requestError.status === 404 || requestError.status === 409) {
          grocery.reload();
        }
      }
      setError(requestError instanceof Error ? requestError.message : 'Unable to remove this grocery item.');
    } finally {
      inFlight.current = false;
      setIsRemoving(false);
    }
  }

  return {
    item: selection?.item ?? null,
    request,
    confirm: () => void confirm(),
    cancel: () => setSelection(null),
    error,
    isRemoving,
    disabled: isRemoving || pending !== null,
    canRetry: pending !== null && error !== null && !isRemoving,
    retryName: pending?.itemName ?? null,
    retry: () => {
      if (pending) {
        void submit(pending);
      }
    },
  };
}
