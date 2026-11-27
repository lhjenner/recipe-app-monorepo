import { fireEvent, render, screen, within } from '@testing-library/react';
import { expect, test, vi } from 'vitest';
import GroceryHistoryList from '../../../src/features/grocery-list/components/GroceryHistoryList';
import type { GroceryHistoryEvent } from '../../../src/features/grocery-list/services/groceryListApi';

const purchase: GroceryHistoryEvent = {
  id: 'purchase-event',
  groceryItemId: 'item',
  kind: 'purchase',
  quantity: 3,
  purchaseUnit: 'tin',
  occurredAtUtc: '2026-04-04T11:30:00Z',
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

  fireEvent.click(screen.getByRole('button', { name: 'Undo purchase' }));
  fireEvent.click(screen.getByRole('button', { name: 'Undo purchase of Tomatoes' }));
  expect(onUndo).toHaveBeenCalledTimes(2);
  expect(onUndo).toHaveBeenCalledWith(purchase);
  expect(screen.getAllByText('Purchase reversed')).toHaveLength(2);
  expect(screen.getAllByText('Purchase undone')).toHaveLength(2);
});

test('displays separate columns and Auckland dates without times on both layouts', () => {
  render(<GroceryHistoryList events={[purchase]} />);
  const table = screen.getByRole('table', { name: 'Purchase history' });
  expect(within(table).getAllByRole('columnheader').map((header) => header.textContent))
    .toEqual(['Date', 'Item', 'Quantity', 'Store', 'Record']);
  const row = within(table).getByRole('row', { name: /Tomatoes/ });
  expect(within(row).getByRole('rowheader')).toHaveTextContent('Tomatoes');
  expect(within(row).getByText('5 Apr 2026')).toBeInTheDocument();
  const cards = screen.getByRole('list', { name: 'Purchase history' });
  expect(within(cards).getByText('5 Apr 2026')).toBeInTheDocument();
});

test('displays numeric quantities without purchase units on both layouts', () => {
  render(<GroceryHistoryList events={[{ ...purchase, quantity: 1.5 }]} />);
  const table = screen.getByRole('table', { name: 'Purchase history' });
  expect(within(table).getByRole('cell', { name: '1.5' })).toBeInTheDocument();
  const cards = screen.getByRole('list', { name: 'Purchase history' });
  expect(within(cards).getByText('1.5', { exact: true })).toBeInTheDocument();
});
