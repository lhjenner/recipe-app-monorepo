import { useRef, useState } from 'react';
import { cancelAllGroceryItems, GroceryListApiError } from '../services/groceryListApi';
import type { GroceryListState } from './useGroceryList';

interface ClearCommand {
  commandId: string;
  expectedListVersion: number;
}

export function useGroceryClearAll(grocery: GroceryListState) {
  const [confirmVersion, setConfirmVersion] = useState<number | null>(null);
  const [pending, setPending] = useState<ClearCommand | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [requiredVersion, setRequiredVersion] = useState<number | null>(null);
  const inFlight = useRef(false);

  const disabled = isSaving
    || pending !== null
    || (requiredVersion !== null && (grocery.data?.version ?? 0) < requiredVersion);

  function request() {
    if (!disabled && grocery.data) {
      setError(null);
      setConfirmVersion(grocery.data.version);
    }
  }

  async function submit(command: ClearCommand) {
    if (inFlight.current) {
      return;
    }
    inFlight.current = true;
    setPending(command);
    setIsSaving(true);
    setError(null);
    try {
      await cancelAllGroceryItems(command.commandId, command.expectedListVersion);
      setPending(null);
      setRequiredVersion(command.expectedListVersion + 1);
      grocery.reload();
    } catch (requestError: unknown) {
      const failure = requestError instanceof GroceryListApiError
        ? requestError
        : new GroceryListApiError(
          requestError instanceof Error ? requestError.message : 'Unable to clear the grocery list.',
          0,
        );
      if (failure.status >= 400 && failure.status < 500) {
        setPending(null);
        if (failure.status === 404 || failure.status === 409) {
          grocery.reload();
        }
      }
      setError(failure.message);
    } finally {
      inFlight.current = false;
      setIsSaving(false);
    }
  }

  return {
    isConfirming: confirmVersion !== null,
    request,
    cancel: () => setConfirmVersion(null),
    confirm: () => {
      if (confirmVersion !== null && !disabled) {
        const command = {
          commandId: crypto.randomUUID(),
          expectedListVersion: confirmVersion,
        };
        setConfirmVersion(null);
        void submit(command);
      }
    },
    error,
    disabled,
    canRetry: pending !== null && !isSaving,
    retry: () => {
      if (pending) {
        void submit(pending);
      }
    },
  };
}
