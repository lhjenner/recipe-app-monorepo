import { useRef, useState } from 'react';
import { GroceryListApiError } from '../services/groceryListApi';

export function useGroceryCommand<TCommand>(
  send: (command: TCommand) => Promise<unknown>,
  onSaved: () => void,
  onRejected?: (error: GroceryListApiError) => void,
) {
  const pending = useRef<TCommand | null>(null);
  const inFlight = useRef(false);
  const [isSaving, setIsSaving] = useState(false);
  const [hasPending, setHasPending] = useState(false);
  const [error, setError] = useState<GroceryListApiError | null>(null);

  async function execute(command: TCommand) {
    if (inFlight.current) {
      return;
    }
    inFlight.current = true;
    setIsSaving(true);
    setError(null);
    try {
      await send(command);
      pending.current = null;
      setHasPending(false);
      onSaved();
    } catch (requestError: unknown) {
      const failure = requestError instanceof GroceryListApiError
        ? requestError
        : new GroceryListApiError(
          requestError instanceof Error ? requestError.message : 'Unable to save grocery changes.',
          0,
        );
      if (failure.status >= 400 && failure.status < 500) {
        pending.current = null;
        setHasPending(false);
        onRejected?.(failure);
      }
      setError(failure);
    } finally {
      inFlight.current = false;
      setIsSaving(false);
    }
  }

  function submit(command: TCommand) {
    if (pending.current !== null) {
      return false;
    }
    pending.current = command;
    setHasPending(true);
    void execute(command);
    return true;
  }

  return {
    submit,
    retry: () => {
      if (pending.current !== null) {
        void execute(pending.current);
      }
    },
    isSaving,
    locked: isSaving || hasPending,
    canRetry: hasPending && !isSaving,
    error,
    clearError: () => setError(null),
  };
}
