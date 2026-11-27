import { useRef, useState } from 'react';
import {
  GroceryListApiError,
  purchaseGroceryItem,
  reverseGroceryPurchase,
  type GroceryItem,
} from '../services/groceryListApi';
import type { GroceryListState } from './useGroceryList';

type PurchaseCommand = {
  kind: 'purchase';
  itemId: string;
  itemName: string;
  commandId: string;
  expectedListVersion: number;
  expectedOutstandingQuantity: number;
  purchaseUnit: string;
  storeId: string | null;
};

type UndoCommand = {
  kind: 'undo';
  eventId: string;
  itemName: string;
  commandId: string;
  expectedListVersion: number;
};

type PendingCommand = PurchaseCommand | UndoCommand;

interface UndoSelection {
  eventId: string;
  itemName: string;
  expectedListVersion: number;
}

export function useGroceryPurchaseActions(
  grocery: GroceryListState,
  onHistoryChanged?: () => void,
) {
  const [undoSelection, setUndoSelection] = useState<UndoSelection | null>(null);
  const [pending, setPending] = useState<PendingCommand | null>(null);
  const [purchaseNotice, setPurchaseNotice] = useState<{ eventId: string; itemName: string } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [requiredVersion, setRequiredVersion] = useState<number | null>(null);
  const inFlight = useRef(false);

  function requestPurchase(item: GroceryItem, storeId: string | null) {
    if (blocked || !grocery.data) {
      return;
    }
    setError(null);
    setStatus(null);
    setPurchaseNotice(null);
    void execute({
      kind: 'purchase',
      itemId: item.id,
      itemName: item.name,
      commandId: crypto.randomUUID(),
      expectedListVersion: grocery.data.version,
      expectedOutstandingQuantity: item.outstandingPurchaseQuantity,
      purchaseUnit: item.purchaseUnit,
      storeId,
    });
  }

  function requestUndo(eventId: string, itemName: string) {
    if (blocked || !grocery.data) {
      return;
    }
    setError(null);
    setStatus(null);
    setUndoSelection({ eventId, itemName, expectedListVersion: grocery.data.version });
  }

  function confirmUndo() {
    if (!undoSelection || blocked) {
      return;
    }
    const selection = undoSelection;
    setUndoSelection(null);
    void execute({
      kind: 'undo',
      eventId: selection.eventId,
      itemName: selection.itemName,
      commandId: crypto.randomUUID(),
      expectedListVersion: selection.expectedListVersion,
    });
  }

  async function execute(command: PendingCommand) {
    if (inFlight.current) {
      return;
    }
    inFlight.current = true;
    setPending(command);
    setIsSaving(true);
    setError(null);
    try {
      const receipt = command.kind === 'purchase'
        ? await purchaseGroceryItem(command.itemId, {
          commandId: command.commandId,
          expectedListVersion: command.expectedListVersion,
          expectedOutstandingQuantity: command.expectedOutstandingQuantity,
          purchaseUnit: command.purchaseUnit,
          storeId: command.storeId,
        })
        : await reverseGroceryPurchase(command.eventId, {
          commandId: command.commandId,
          expectedListVersion: command.expectedListVersion,
        });
      const eventId = receipt.resultIds[0];
      if (!eventId) {
        throw new GroceryListApiError('The grocery service returned an invalid purchase receipt.', 500);
      }

      setPending(null);
      setRequiredVersion(receipt.resultListVersion);
      if (command.kind === 'purchase') {
        setPurchaseNotice({ eventId, itemName: command.itemName });
        setStatus(null);
      } else {
        setPurchaseNotice(null);
        setStatus('Purchase undone.');
      }
      grocery.reload();
      onHistoryChanged?.();
    } catch (requestError: unknown) {
      const failure = requestError instanceof GroceryListApiError
        ? requestError
        : new GroceryListApiError(
          requestError instanceof Error ? requestError.message : 'Unable to save this purchase.',
          0,
        );
      if (failure.status >= 400 && failure.status < 500) {
        setPending(null);
        if (failure.status === 404 || failure.status === 409) {
          grocery.reload();
          onHistoryChanged?.();
        }
      }
      setError(failure.message);
    } finally {
      inFlight.current = false;
      setIsSaving(false);
    }
  }

  const blocked = isSaving
    || pending !== null
    || !grocery.data
    || (requiredVersion !== null && (grocery.data?.version ?? 0) < requiredVersion);

  return {
    undoItemName: undoSelection?.itemName ?? null,
    undoEventId: undoSelection?.eventId ?? null,
    purchaseNotice,
    requestPurchase,
    requestUndo,
    confirmUndo,
    cancelUndo: () => setUndoSelection(null),
    error,
    status,
    disabled: blocked,
    canRetry: pending !== null && !isSaving,
    retryLabel: pending?.kind === 'undo' ? 'Retry Undo' : 'Retry purchase',
    retry: () => {
      if (pending) {
        void execute(pending);
      }
    },
  };
}
