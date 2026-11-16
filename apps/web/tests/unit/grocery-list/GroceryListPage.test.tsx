import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, expect, test, vi } from 'vitest';
import GroceryListPage from '../../../src/features/grocery-list/pages/GroceryListPage';
import { GroceryListApiError } from '../../../src/features/grocery-list/services/groceryListApi';

const mocks = vi.hoisted(() => ({
  cancelGroceryItem: vi.fn(),
  updateGroceryItem: vi.fn(),
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
  updateGroceryItem: mocks.updateGroceryItem,
  GroceryListApiError: class GroceryListApiError extends Error {
    readonly status: number;
    readonly fieldErrors: Record<string, string[]>;

    constructor(message: string, status: number, fieldErrors: Record<string, string[]> = {}) {
      super(message);
      this.status = status;
      this.fieldErrors = fieldErrors;
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
  mocks.updateGroceryItem.mockResolvedValue({ resultListVersion: 13 });
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

test('saves an inline quantity change and waits for the refreshed version before enabling controls', async () => {
  const { rerender } = render(<MemoryRouter><GroceryListPage /></MemoryRouter>);
  const state = mocks.useGroceryList.mock.results[0].value;
  const increase = screen.getAllByRole('button', { name: 'Increase quantity for Onions' })[0];
  fireEvent.click(increase);
  fireEvent.click(increase);
  await waitFor(() => expect(mocks.updateGroceryItem).toHaveBeenCalledTimes(1));
  expect(mocks.updateGroceryItem.mock.calls[0]).toEqual(['item-id', {
    commandId: expect.any(String),
    expectedListVersion: 12,
    outstandingPurchaseQuantity: 3,
    purchaseUnit: 'onion',
    note: null,
    departmentUpdate: null,
    adHocStoreId: null,
    adHocDepartmentId: null,
  }]);
  await waitFor(() => expect(state.reload).toHaveBeenCalled());
  expect(increase).toBeDisabled();
  mocks.useGroceryList.mockReturnValue({
    ...state,
    data: { ...state.data, version: 13, items: [{ ...state.data.items[0], outstandingPurchaseQuantity: 3 }] },
  });
  rerender(<MemoryRouter><GroceryListPage /></MemoryRouter>);
  expect(screen.getAllByRole('spinbutton', { name: 'Quantity to buy for Onions' })[0]).toHaveValue(3);
  expect(increase).toBeEnabled();
  fireEvent.click(screen.getAllByRole('button', { name: 'Decrease quantity for Onions' })[0]);
  await waitFor(() => expect(mocks.updateGroceryItem).toHaveBeenCalledTimes(2));
  expect(mocks.updateGroceryItem.mock.calls[1][1]).toMatchObject({
    expectedListVersion: 13, outstandingPurchaseQuantity: 2,
  });
});

test('retries an ambiguous quantity update with the exact same command', async () => {
  mocks.updateGroceryItem.mockRejectedValueOnce(new Error('Network response was lost.'));
  render(<MemoryRouter><GroceryListPage /></MemoryRouter>);
  const increase = screen.getAllByRole('button', { name: 'Increase quantity for Onions' })[0];
  fireEvent.click(increase);
  await screen.findByText('Network response was lost.');
  expect(increase).toBeDisabled();
  fireEvent.click(screen.getByRole('button', { name: 'Retry quantity update' }));
  await waitFor(() => expect(mocks.updateGroceryItem).toHaveBeenCalledTimes(2));
  expect(mocks.updateGroceryItem.mock.calls[1]).toEqual(mocks.updateGroceryItem.mock.calls[0]);
});

test.each([400, 409])('surfaces a %s quantity rejection without changing the displayed quantity', async (status) => {
  mocks.updateGroceryItem.mockRejectedValueOnce(new GroceryListApiError('Quantity update rejected.', status, {
    outstandingPurchaseQuantity: ['Purchase quantity cannot be lower than the recipe requirement.'],
  }));
  render(<MemoryRouter><GroceryListPage /></MemoryRouter>);
  const state = mocks.useGroceryList.mock.results[0].value;
  fireEvent.click(screen.getAllByRole('button', { name: 'Decrease quantity for Onions' })[0]);
  expect(await screen.findByRole('alert')).toHaveTextContent(
    'Purchase quantity cannot be lower than the recipe requirement.',
  );
  expect(screen.getAllByRole('spinbutton', { name: 'Quantity to buy for Onions' })[0]).toHaveValue(2);
  expect(screen.queryByRole('button', { name: 'Retry quantity update' })).not.toBeInTheDocument();
  expect(state.reload).toHaveBeenCalledTimes(status === 409 ? 1 : 0);
});
