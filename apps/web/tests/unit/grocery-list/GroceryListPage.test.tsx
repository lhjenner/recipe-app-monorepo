import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, expect, test, vi } from 'vitest';
import GroceryListPage from '../../../src/features/grocery-list/pages/GroceryListPage';

const mocks = vi.hoisted(() => ({
  cancelGroceryItem: vi.fn(),
  useGroceryList: vi.fn(),
  useSupermarkets: vi.fn(),
}));

vi.mock('../../../src/features/grocery-list/hooks/useGroceryList', () => ({
  useGroceryList: mocks.useGroceryList,
}));

vi.mock('../../../src/features/ingredients/hooks/useSupermarkets', () => ({
  useSupermarkets: mocks.useSupermarkets,
}));

vi.mock('../../../src/features/grocery-list/services/groceryListApi', () => ({
  cancelGroceryItem: mocks.cancelGroceryItem,
  GroceryListApiError: class GroceryListApiError extends Error {
    readonly status: number;

    constructor(message: string, status: number) {
      super(message);
      this.status = status;
    }
  },
}));

vi.mock('../../../src/features/grocery-list/components/GroceryItemEditDialog', () => ({
  default: () => null,
}));

vi.mock('../../../src/features/grocery-list/components/GroceryManualEntryModal', () => ({
  default: () => null,
}));

beforeEach(() => {
  vi.clearAllMocks();
  mocks.useGroceryList.mockReturnValue({
    data: {
      id: 'list-id',
      version: 12,
      completedItemCount: 0,
      items: [{
        id: 'item-id',
        ingredientId: null,
        name: 'onions',
        state: 'outstanding',
        note: null,
        dimension: 'count',
        countLabel: 'onion',
        requirementAmount: null,
        requirementUnit: null,
        outstandingPurchaseQuantity: 2,
        purchaseUnit: 'onion',
        ingredientVersion: null,
        adHocStoreId: null,
        adHocDepartmentId: null,
        adHocDepartmentName: null,
        storeMappings: [],
        contributions: [],
      }],
    },
    isLoading: false,
    error: null,
    isStale: false,
    lastSuccessfulAt: null,
    updated: false,
    reload: vi.fn(),
  });
  mocks.useSupermarkets.mockReturnValue({
    data: [],
    isLoading: false,
    error: null,
    reload: vi.fn(),
  });
});

test('retries a removal with the same command after an ambiguous network failure', async () => {
  mocks.cancelGroceryItem
    .mockRejectedValueOnce(new Error('Network response was lost.'))
    .mockResolvedValueOnce(undefined);
  render(
    <MemoryRouter>
      <GroceryListPage />
    </MemoryRouter>,
  );
  const removeButtons = screen.getAllByRole('button', { name: 'Remove onions from Grocery List' });

  fireEvent.click(removeButtons[0]);
  await waitFor(() => expect(mocks.cancelGroceryItem).toHaveBeenCalledTimes(1));
  fireEvent.click(removeButtons[0]);

  await waitFor(() => expect(mocks.cancelGroceryItem).toHaveBeenCalledTimes(2));
  expect(mocks.cancelGroceryItem.mock.calls[1][0]).toBe('item-id');
  expect(mocks.cancelGroceryItem.mock.calls[1][1]).toBe(mocks.cancelGroceryItem.mock.calls[0][1]);
  expect(mocks.cancelGroceryItem.mock.calls[1][2]).toBe(12);
});
