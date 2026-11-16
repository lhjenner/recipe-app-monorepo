import { useRef, useState } from 'react';
import { cancelGroceryItem, GroceryListApiError, type GroceryItem } from '../services/groceryListApi';
import type { GroceryListState } from './useGroceryList';

interface RemovalCommand {
  commandId: string;
  expectedListVersion: number;
}

export function useGroceryItemRemoval(grocery: GroceryListState) {
  const [selection, setSelection] = useState<{ item: GroceryItem; version: number } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isRemoving, setIsRemoving] = useState(false);
  const commands = useRef(new Map<string, RemovalCommand>());
  const inFlight = useRef(false);

  function request(item: GroceryItem) {
    if (grocery.data && !inFlight.current) {
      setSelection({ item, version: grocery.data.version });
    }
  }

  async function confirm() {
    if (!selection || inFlight.current) {
      return;
    }
    inFlight.current = true;
    setIsRemoving(true);
    setSelection(null);
    setError(null);
    const { item, version } = selection;
    const command = commands.current.get(item.id)
      ?? { commandId: crypto.randomUUID(), expectedListVersion: version };
    commands.current.set(item.id, command);
    try {
      await cancelGroceryItem(item.id, command.commandId, command.expectedListVersion);
      commands.current.delete(item.id);
      grocery.reload();
    } catch (requestError: unknown) {
      if (requestError instanceof GroceryListApiError
        && requestError.status >= 400 && requestError.status < 500) {
        commands.current.delete(item.id);
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
  };
}
