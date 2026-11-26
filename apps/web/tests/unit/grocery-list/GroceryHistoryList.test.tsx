import { fireEvent, render, screen } from '@testing-library/react';
import { expect, test, vi } from 'vitest';
import GroceryHistoryList from '../../../src/features/grocery-list/components/GroceryHistoryList';
import type { GroceryHistoryEvent } from '../../../src/features/grocery-list/services/groceryListApi';

const purchase: GroceryHistoryEvent = {
  id: 'purchase-event',
  groceryItemId: 'item',
  kind: 'purchase',
  quantity: 3,
  purchaseUnit: 'tin',
  occurredAtUtc: '2026-04-05T00:00:00Z',
  itemNameSnapshot: 'tomatoes',
  noteSnapshot: null,
  storeId: null,
  storeName: null,
  origin: 'manual',
  reversesEventId: null,
  isReversed: false,
};

test('offers Undo only for an unreversed purchase event', () => {
  const onUndo = vi.fn();
  render(
    <GroceryHistoryList
      events={[
        purchase,
        { ...purchase, id: 'reversed-event', isReversed: true },
        { ...purchase, id: 'reversal-event', kind: 'reversal', reversesEventId: purchase.id },
      ]}
      onUndo={onUndo}
    />,
  );

  const undoButtons = screen.getAllByRole('button', { name: 'Undo purchase' });
  fireEvent.click(undoButtons[0]);
  expect(onUndo).toHaveBeenCalledWith(purchase);
  expect(screen.getAllByText('Purchase reversed')).toHaveLength(2);
  expect(screen.getAllByText('Undo recorded (manual)')).toHaveLength(2);
});
