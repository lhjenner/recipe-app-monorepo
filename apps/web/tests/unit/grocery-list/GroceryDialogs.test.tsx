import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, expect, test, vi } from 'vitest';
import GroceryManualEntryModal from '../../../src/features/grocery-list/components/GroceryManualEntryModal';
import GroceryItemEditDialog from '../../../src/features/grocery-list/components/GroceryItemEditDialog';
import type { GroceryItem } from '../../../src/features/grocery-list/services/groceryListApi';

const mocks = vi.hoisted(() => ({ send: vi.fn() }));
vi.mock('../../../src/features/grocery-list/services/groceryListApi', async (importOriginal) => ({
  ...await importOriginal<typeof import('../../../src/features/grocery-list/services/groceryListApi')>(),
  addGroceryItems: mocks.send,
  updateGroceryItem: mocks.send,
}));
vi.mock('../../../src/features/grocery-list/hooks/useGroceryEntryLookups', () => ({
  useGroceryEntryLookups: () => ({ categories: [], departments: [], stores: [], error: null }),
}));
vi.mock('../../../src/features/grocery-list/hooks/useGroceryIngredientOptions', () => ({
  useGroceryIngredientOptions: () => ({ items: [], error: null }),
}));
vi.mock('../../../src/features/ingredients/services/ingredientsApi', () => ({
  getDepartments: () => Promise.resolve([]),
}));

const item: GroceryItem = {
  id: 'item', ingredientId: null, ingredientVersion: null, name: 'Oats',
  state: 'outstanding', note: null, dimension: 'count', countLabel: 'item',
  requirementAmount: null, requirementUnit: null, outstandingPurchaseQuantity: 1,
  purchaseUnit: 'item', adHocStoreId: null, adHocDepartmentId: null,
  adHocDepartmentName: null, storeMappings: [], contributions: [],
};

beforeEach(() => {
  mocks.send.mockReset();
  mocks.send.mockResolvedValue({});
});

test.each(['add', 'edit'] as const)(
  '%s keeps its modal and draft locked during saving and an ambiguous result until retry succeeds',
  async (mode) => {
    let rejectRequest!: (error: Error) => void;
    mocks.send.mockReturnValueOnce(new Promise((_, reject) => { rejectRequest = reject; }));
    const onClose = vi.fn();
    const onSaved = vi.fn();
    render(mode === 'add'
      ? <GroceryManualEntryModal expectedListVersion={3} onClose={onClose} onAdded={onSaved} />
      : <GroceryItemEditDialog item={item} listVersion={3} selectedStoreId={null} stores={[]}
        onClose={onClose} onSaved={onSaved} />);
    const dialog = screen.getByRole('dialog', {
      name: mode === 'add' ? 'Add items to Grocery List' : 'Edit Oats',
    });
    const firstInput = screen.getByLabelText(mode === 'add' ? 'Name' : 'Quantity');
    expect(firstInput).toHaveFocus();
    expect(dialog).toHaveClass('grocery-entry-dialog');
    expect(dialog.firstElementChild).toHaveClass('grocery-entry-heading');
    if (mode === 'add') {
      fireEvent.change(firstInput, { target: { value: 'Oats' } });
    }

    fireEvent.click(screen.getByRole('button', {
      name: mode === 'add' ? 'Add to Grocery List' : 'Save changes',
    }));
    function expectLocked() {
      const cancel = new Event('cancel', { cancelable: true });
      fireEvent(dialog, cancel);
      fireEvent.click(dialog);
      expect(cancel.defaultPrevented).toBe(true);
      expect(dialog).toHaveAttribute('open');
      expect(firstInput).toBeDisabled();
      expect(screen.getByRole('button', { name: 'Close' })).toBeDisabled();
      if (mode === 'edit') {
        expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled();
      }
      expect(onClose).not.toHaveBeenCalled();
    }
    expectLocked();
    await act(async () => rejectRequest(new Error('Response lost')));
    await screen.findByText('Response lost');
    expectLocked();
    expect(mocks.send).toHaveBeenCalledOnce();

    fireEvent.click(screen.getByRole('button', {
      name: mode === 'add' ? 'Retry addition' : 'Retry edit',
    }));
    await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
    expect(mocks.send).toHaveBeenCalledTimes(2);
    expect(mocks.send.mock.calls[1]).toEqual(mocks.send.mock.calls[0]);
    expect(onClose).toHaveBeenCalledOnce();
  },
);
