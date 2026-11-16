import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { expect, test, vi } from 'vitest';
import GroceryManualEntryModal from '../../../src/features/grocery-list/components/GroceryManualEntryModal';

const mocks = vi.hoisted(() => ({
  addGroceryItems: vi.fn(),
  useGroceryEntryLookups: vi.fn(),
  useGroceryIngredientOptions: vi.fn(),
}));

vi.mock('../../../src/features/grocery-list/hooks/useGroceryEntryLookups', () => ({
  useGroceryEntryLookups: mocks.useGroceryEntryLookups,
}));

vi.mock('../../../src/features/grocery-list/hooks/useGroceryIngredientOptions', () => ({
  useGroceryIngredientOptions: mocks.useGroceryIngredientOptions,
}));

vi.mock('../../../src/features/grocery-list/services/groceryListApi', () => ({
  addGroceryItems: mocks.addGroceryItems,
  GroceryListApiError: class GroceryListApiError extends Error {},
}));

test('omits Purchase Unit and submits a generic internal item unit', async () => {
  mocks.useGroceryEntryLookups.mockReturnValue({
    categories: [],
    departments: [],
    stores: [],
    error: null,
  });
  mocks.useGroceryIngredientOptions.mockReturnValue({ items: [], error: null });
  mocks.addGroceryItems.mockResolvedValue({});
  render(
    <GroceryManualEntryModal
      expectedListVersion={3}
      onClose={vi.fn()}
      onAdded={vi.fn()}
    />,
  );

  expect(screen.queryByText('Purchase unit')).not.toBeInTheDocument();
  fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Oats' } });
  fireEvent.click(screen.getByRole('button', { name: 'Add to Grocery List' }));

  await waitFor(() => expect(mocks.addGroceryItems).toHaveBeenCalledTimes(1));
  expect(mocks.addGroceryItems.mock.calls[0][0].items[0].purchaseUnit).toBe('item');
});
