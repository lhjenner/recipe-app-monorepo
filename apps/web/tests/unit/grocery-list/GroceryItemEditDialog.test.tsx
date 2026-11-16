import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, expect, test, vi } from 'vitest';
import GroceryItemEditDialog from '../../../src/features/grocery-list/components/GroceryItemEditDialog';
import { GroceryListApiError, type GroceryItem } from '../../../src/features/grocery-list/services/groceryListApi';

const mocks = vi.hoisted(() => ({ update: vi.fn(), departments: vi.fn() }));
vi.mock('../../../src/features/grocery-list/services/groceryListApi', async (importOriginal) => ({
  ...await importOriginal<typeof import('../../../src/features/grocery-list/services/groceryListApi')>(),
  updateGroceryItem: mocks.update,
}));
vi.mock('../../../src/features/ingredients/services/ingredientsApi', () => ({
  getDepartments: mocks.departments,
}));

const item: GroceryItem = {
  id: 'item', ingredientId: null, ingredientVersion: null, name: 'Oats',
  state: 'outstanding', note: null, dimension: 'count', countLabel: 'item',
  requirementAmount: null, requirementUnit: null, outstandingPurchaseQuantity: 1,
  purchaseUnit: 'item', adHocStoreId: 'store-a', adHocDepartmentId: 'old-department',
  adHocDepartmentName: 'Pantry', storeMappings: [], contributions: [],
};
const stores = [
  { id: 'store-a', code: 'a', name: 'Store A', displayOrder: 1 },
  { id: 'store-b', code: 'b', name: 'Store B', displayOrder: 2 },
];
function props(linked = false) {
  return {
    item: linked ? {
      ...item, ingredientId: 'ingredient', ingredientVersion: 5,
      storeMappings: [{
        storeId: 'store-a', departmentId: 'old-department', departmentName: 'Pantry', state: 'mapped' as const,
      }],
    } : item,
    listVersion: 3,
    selectedStoreId: 'store-a' as string | null,
    stores,
    onClose: vi.fn(),
    onSaved: vi.fn(),
  };
}
beforeEach(() => {
  vi.clearAllMocks();
  mocks.departments.mockResolvedValue([
    { id: 'old-department', name: 'Pantry' }, { id: 'new-department', name: 'Produce' },
  ]);
  mocks.update.mockResolvedValue({});
});

test('preserves both ad-hoc mapping IDs when editing only a note in another store', async () => {
  render(<GroceryItemEditDialog {...props()} selectedStoreId="store-b" />);
  fireEvent.change(screen.getByLabelText('Note (optional)'), { target: { value: 'For breakfast' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
  await waitFor(() => expect(mocks.update).toHaveBeenCalledOnce());
  expect(mocks.update.mock.calls[0][1]).toMatchObject({
    note: 'For breakfast', adHocStoreId: 'store-a', adHocDepartmentId: 'old-department',
  });
});

test('changes both ad-hoc mapping IDs when the department is explicitly changed', async () => {
  render(<GroceryItemEditDialog {...props()} selectedStoreId="store-b" />);
  await screen.findByRole('option', { name: 'Produce' });
  fireEvent.change(screen.getByLabelText('Store B department'), { target: { value: 'new-department' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
  await waitFor(() => expect(mocks.update).toHaveBeenCalledOnce());
  expect(mocks.update.mock.calls[0][1]).toMatchObject({
    adHocStoreId: 'store-b', adHocDepartmentId: 'new-department',
  });
});

test('acknowledges a no-store selection and discards master changes without losing shopping edits', async () => {
  const initial = props(true);
  const { rerender } = render(<GroceryItemEditDialog {...initial} />);
  await screen.findByRole('option', { name: 'Produce' });
  fireEvent.change(screen.getByLabelText('Note (optional)'), { target: { value: 'Keep me' } });
  fireEvent.change(screen.getByLabelText('Store A department'), { target: { value: 'new-department' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
  expect(screen.getByRole('button', { name: 'Update master Ingredient' })).toBeVisible();
  rerender(<GroceryItemEditDialog {...initial} selectedStoreId={null} />);
  fireEvent.click(screen.getByRole('button', { name: 'Review selected store' }));
  expect(screen.queryByRole('button', { name: 'Update master Ingredient' })).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
  await waitFor(() => expect(mocks.update).toHaveBeenCalledOnce());
  expect(mocks.update.mock.calls[0][1]).toMatchObject({ note: 'Keep me', departmentUpdate: null });
});

test.each(['store', 'list', 'ingredient'])(
  'blocks confirmation saves when the %s changes after opening the prompt',
  async (change) => {
    const initial = props(true);
    const { rerender } = render(<GroceryItemEditDialog {...initial} />);
    await screen.findByRole('option', { name: 'Produce' });
    fireEvent.change(screen.getByLabelText('Store A department'), { target: { value: 'new-department' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
    rerender(<GroceryItemEditDialog
      {...initial}
      selectedStoreId={change === 'store' ? 'store-b' : initial.selectedStoreId}
      listVersion={change === 'list' ? 4 : initial.listVersion}
      item={change === 'ingredient' ? { ...initial.item, ingredientVersion: 6 } : initial.item}
    />);
    fireEvent.click(screen.getByRole('button', { name: 'Update master Ingredient' }));
    fireEvent.click(screen.getByRole('button', { name: 'Discard department change and save shopping changes' }));
    expect(mocks.update).not.toHaveBeenCalled();
  },
);

test.each([0, 503])('replays the exact edit after an ambiguous %s failure despite newer versions', async (status) => {
  mocks.update.mockRejectedValueOnce(status === 0
    ? new Error('Response lost') : new GroceryListApiError('Response lost', status));
  const initial = props(true);
  const { rerender } = render(<GroceryItemEditDialog {...initial} />);
  await screen.findByRole('option', { name: 'Produce' });
  fireEvent.change(screen.getByLabelText('Store A department'), { target: { value: 'new-department' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
  fireEvent.click(screen.getByRole('button', { name: 'Update master Ingredient' }));
  await screen.findByText('Response lost');
  rerender(<GroceryItemEditDialog
    {...initial} listVersion={4} item={{ ...initial.item, ingredientVersion: 6 }}
  />);
  expect(screen.getByLabelText('Note (optional)')).toBeDisabled();
  expect(screen.getByRole('button', { name: 'Review latest list' })).toBeDisabled();
  expect(screen.getByRole('button', { name: 'Review latest Ingredient' })).toBeDisabled();
  fireEvent.click(screen.getByRole('button', { name: 'Retry edit' }));
  await waitFor(() => expect(mocks.update).toHaveBeenCalledTimes(2));
  expect(mocks.update.mock.calls[1]).toEqual(mocks.update.mock.calls[0]);
  expect(initial.onSaved).toHaveBeenCalledOnce();
});

test('a known rejection permits version review and correction with a new command', async () => {
  mocks.update.mockRejectedValueOnce(new GroceryListApiError('List changed', 409));
  const initial = props();
  const { rerender } = render(<GroceryItemEditDialog {...initial} />);
  fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
  await screen.findByText('List changed');
  rerender(<GroceryItemEditDialog {...initial} listVersion={4} />);
  fireEvent.click(screen.getByRole('button', { name: 'Review latest list' }));
  expect(screen.getByLabelText('Note (optional)')).toBeEnabled();
  fireEvent.change(screen.getByLabelText('Note (optional)'), { target: { value: 'Reviewed' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
  await waitFor(() => expect(mocks.update).toHaveBeenCalledTimes(2));
  expect(mocks.update.mock.calls[1][1]).toMatchObject({ expectedListVersion: 4, note: 'Reviewed' });
  expect(mocks.update.mock.calls[1][1].commandId).not.toBe(mocks.update.mock.calls[0][1].commandId);
  expect(screen.queryByRole('button', { name: 'Retry edit' })).not.toBeInTheDocument();
});

test.each(['list', 'store'])('rebases untouched quantity when reviewing a changed %s', async (review) => {
  const initial = props();
  const { rerender } = render(<GroceryItemEditDialog {...initial} />);
  fireEvent.change(screen.getByLabelText('Note (optional)'), { target: { value: 'My note' } });
  rerender(<GroceryItemEditDialog
    {...initial} listVersion={4}
    item={{ ...initial.item, outstandingPurchaseQuantity: 3 }}
    selectedStoreId={review === 'store' ? 'store-b' : 'store-a'}
  />);
  fireEvent.click(screen.getByRole('button', { name: review === 'store' ? 'Review Store B' : 'Review latest list' }));
  expect(screen.getByLabelText('Quantity')).toHaveValue(3);
  expect(screen.getByLabelText('Note (optional)')).toHaveValue('My note');
  fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
  await waitFor(() => expect(mocks.update).toHaveBeenCalledOnce());
  expect(mocks.update.mock.calls[0][1]).toMatchObject({
    expectedListVersion: 4, outstandingPurchaseQuantity: 3, note: 'My note',
  });
});

test('requires a separate choice for each shopping field changed on both devices', async () => {
  const initial = { ...props(), item: { ...item, note: 'Original' } };
  const { rerender } = render(<GroceryItemEditDialog {...initial} />);
  fireEvent.change(screen.getByLabelText('Quantity'), { target: { value: '2' } });
  fireEvent.change(screen.getByLabelText('Note (optional)'), { target: { value: 'My note' } });
  rerender(<GroceryItemEditDialog {...initial} listVersion={4}
    item={{ ...initial.item, outstandingPurchaseQuantity: 3, note: 'Other note' }} />);
  fireEvent.click(screen.getByRole('button', { name: 'Review latest list' }));
  expect(screen.getByText('My quantity: 2')).toBeVisible();
  expect(screen.getByText('Latest quantity: 3')).toBeVisible();
  expect(screen.getByText('My note: My note')).toBeVisible();
  expect(screen.getByText('Latest note: Other note')).toBeVisible();
  const apply = screen.getByRole('button', { name: 'Apply review choices' });
  expect(apply).toBeDisabled();
  fireEvent.click(screen.getByRole('radio', { name: 'Use latest quantity' }));
  expect(apply).toBeDisabled();
  fireEvent.click(screen.getByRole('radio', { name: 'Keep my note' }));
  fireEvent.click(apply);
  fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
  await waitFor(() => expect(mocks.update).toHaveBeenCalledOnce());
  expect(mocks.update.mock.calls[0][1]).toMatchObject({
    expectedListVersion: 4, outstandingPurchaseQuantity: 3, note: 'My note',
  });
});

test('requires a fresh review if the list changes while conflict choices are open', () => {
  const initial = props();
  const { rerender } = render(<GroceryItemEditDialog {...initial} />);
  fireEvent.change(screen.getByLabelText('Quantity'), { target: { value: '2' } });
  rerender(<GroceryItemEditDialog {...initial} listVersion={4}
    item={{ ...initial.item, outstandingPurchaseQuantity: 3 }} />);
  fireEvent.click(screen.getByRole('button', { name: 'Review latest list' }));
  fireEvent.click(screen.getByRole('radio', { name: 'Keep my quantity' }));
  rerender(<GroceryItemEditDialog {...initial} listVersion={5}
    item={{ ...initial.item, outstandingPurchaseQuantity: 4 }} />);
  fireEvent.click(screen.getByRole('button', { name: 'Apply review choices' }));
  expect(mocks.update).not.toHaveBeenCalled();
  expect(screen.getByRole('button', { name: 'Review latest list' })).toBeVisible();
});

test('requires a choice when a linked department changes on both devices', async () => {
  const initial = props(true);
  const { rerender } = render(<GroceryItemEditDialog {...initial} />);
  await screen.findByRole('option', { name: 'Produce' });
  fireEvent.change(screen.getByLabelText('Store A department'), { target: { value: 'new-department' } });
  rerender(<GroceryItemEditDialog {...initial} listVersion={4}
    item={{ ...initial.item, ingredientVersion: 6, storeMappings: [] }} />);
  fireEvent.click(screen.getByRole('button', { name: 'Review latest list' }));
  expect(screen.getByText('My department: Produce')).toBeVisible();
  expect(screen.getByText('Latest department: Uncategorised')).toBeVisible();
  fireEvent.click(screen.getByRole('radio', { name: 'Use latest department' }));
  fireEvent.click(screen.getByRole('button', { name: 'Apply review choices' }));
  fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
  await waitFor(() => expect(mocks.update).toHaveBeenCalledOnce());
  expect(mocks.update.mock.calls[0][1].departmentUpdate).toBeNull();
});
