import { useRef, useState } from 'react';
import {
  GroceryListApiError,
  updateGroceryItem,
  type GroceryItem,
  type GroceryItemUpdateRequest,
} from '../services/groceryListApi';
import type { GroceryListState } from './useGroceryList';

interface PendingQuantityUpdate {
  itemId: string;
  request: GroceryItemUpdateRequest;
  resultVersion: number | null;
}

export function useGroceryQuantityUpdate(grocery: GroceryListState) {
  const [pending, setPending] = useState<PendingQuantityUpdate | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inFlight = useRef(false);
  const busy = isSaving || (pending !== null && (pending.resultVersion === null
    || grocery.data === null || grocery.data.version < pending.resultVersion));

  async function submit(command: PendingQuantityUpdate) {
    if (inFlight.current) {
      return;
    }
    inFlight.current = true;
    setPending(command);
    setIsSaving(true);
    setError(null);
    try {
      const receipt = await updateGroceryItem(command.itemId, command.request);
      setPending({ ...command, resultVersion: receipt.resultListVersion });
      grocery.reload();
    } catch (requestError: unknown) {
      if (requestError instanceof GroceryListApiError
        && requestError.status >= 400 && requestError.status < 500) {
        setPending(null);
        if (requestError.status === 404 || requestError.status === 409) {
          grocery.reload();
        }
      }
      const details = requestError instanceof GroceryListApiError
        ? Object.values(requestError.fieldErrors).flat().join(' ')
        : '';
      setError(details || (requestError instanceof Error
        ? requestError.message
        : 'Unable to update this grocery quantity.'));
    } finally {
      inFlight.current = false;
      setIsSaving(false);
    }
  }

  function changeQuantity(item: GroceryItem, quantity: number) {
    if (!grocery.data || grocery.isStale || busy || inFlight.current) {
      return;
    }
    void submit({
      itemId: item.id,
      resultVersion: null,
      request: {
        commandId: crypto.randomUUID(),
        expectedListVersion: grocery.data.version,
        outstandingPurchaseQuantity: quantity,
        purchaseUnit: item.purchaseUnit,
        note: item.note,
        departmentUpdate: null,
        adHocStoreId: item.ingredientId ? null : item.adHocStoreId,
        adHocDepartmentId: item.ingredientId ? null : item.adHocDepartmentId,
      },
    });
  }

  return {
    changeQuantity,
    disabled: grocery.isStale || busy,
    error,
    canRetry: Boolean(error && pending?.resultVersion === null && !isSaving),
    retry: () => {
      if (pending && pending.resultVersion === null) {
        void submit(pending);
      }
    },
  };
}
