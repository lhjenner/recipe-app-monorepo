import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, expect, test, vi } from 'vitest';
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

beforeEach(() => {
  vi.clearAllMocks();
  mocks.useGroceryEntryLookups.mockReturnValue({
    categories: [],
    departments: [],
    stores: [],
    error: null,
  });
  mocks.useGroceryIngredientOptions.mockReturnValue({ items: [], error: null });
  mocks.addGroceryItems.mockResolvedValue({});
});

test('omits Purchase Unit and submits a generic internal item unit', async () => {
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

test('steps each row independently without submitting and disables decrement at one', () => {
  render(
    <GroceryManualEntryModal expectedListVersion={3} onClose={vi.fn()} onAdded={vi.fn()} />,
  );

  const firstRow = within(screen.getByRole('group', { name: 'Item 1' }));
  const decrease = firstRow.getByRole('button', { name: 'Decrease quantity' });
  const increase = firstRow.getByRole('button', { name: 'Increase quantity' });
  expect(decrease).toBeDisabled();
  fireEvent.click(decrease);
  expect(firstRow.getByRole('spinbutton', { name: 'Quantity' })).toHaveValue(1);

  fireEvent.click(screen.getByRole('button', { name: 'Add row' }));
  fireEvent.click(increase);
  expect(firstRow.getByRole('spinbutton', { name: 'Quantity' })).toHaveValue(2);
  expect(decrease).toBeEnabled();
  const secondRow = within(screen.getByRole('group', { name: 'Item 2' }));
  expect(secondRow.getByRole('spinbutton', { name: 'Quantity' })).toHaveValue(1);

  fireEvent.click(decrease);
  expect(firstRow.getByRole('spinbutton', { name: 'Quantity' })).toHaveValue(1);
  expect(decrease).toBeDisabled();
  expect(mocks.addGroceryItems).not.toHaveBeenCalled();
});

test('steps a typed quantity up to the existing maximum', () => {
  render(
    <GroceryManualEntryModal expectedListVersion={3} onClose={vi.fn()} onAdded={vi.fn()} />,
  );
  const quantity = screen.getByRole('spinbutton', { name: 'Quantity' });
  const increase = screen.getByRole('button', { name: 'Increase quantity' });
  fireEvent.change(quantity, { target: { value: '9998' } });
  fireEvent.click(increase);
  expect(quantity).toHaveValue(9999);
  expect(increase).toBeDisabled();
  fireEvent.click(screen.getByRole('button', { name: 'Decrease quantity' }));
  expect(quantity).toHaveValue(9998);
  expect(increase).toBeEnabled();
});

test.each(['', '0', '1.5', '10000'])('disables stepping for invalid quantity %j', (value) => {
  render(
    <GroceryManualEntryModal expectedListVersion={3} onClose={vi.fn()} onAdded={vi.fn()} />,
  );
  fireEvent.change(screen.getByRole('spinbutton', { name: 'Quantity' }), { target: { value } });
  expect(screen.getByRole('button', { name: 'Decrease quantity' })).toBeDisabled();
  expect(screen.getByRole('button', { name: 'Increase quantity' })).toBeDisabled();
});

test('disables quantity buttons while saving', async () => {
  let finishSaving: () => void = () => {};
  mocks.addGroceryItems.mockReturnValue(new Promise<void>((resolve) => { finishSaving = resolve; }));
  render(
    <GroceryManualEntryModal expectedListVersion={3} onClose={vi.fn()} onAdded={vi.fn()} />,
  );
  fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Oats' } });
  fireEvent.change(screen.getByRole('spinbutton', { name: 'Quantity' }), { target: { value: '2' } });
  fireEvent.click(screen.getByRole('button', { name: 'Add to Grocery List' }));
  expect(screen.getByRole('button', { name: 'Decrease quantity' })).toBeDisabled();
  expect(screen.getByRole('button', { name: 'Increase quantity' })).toBeDisabled();
  finishSaving();
  await waitFor(() => expect(screen.getByRole('button', { name: 'Increase quantity' })).toBeEnabled());
});

test('opens matching suggestions as results arrive and narrows them while typing', () => {
  const props = { expectedListVersion: 3, onClose: vi.fn(), onAdded: vi.fn() };
  const { rerender } = render(<GroceryManualEntryModal {...props} />);
  const name = screen.getByRole('combobox', { name: 'Name' });
  fireEvent.change(name, { target: { value: 'cru' } });
  expect(screen.queryByRole('listbox')).not.toBeInTheDocument();

  mocks.useGroceryIngredientOptions.mockReturnValue({
    items: [{ id: 'crumpets', name: 'crumpets' }, { id: 'chilli', name: 'crushed chilli' }],
    error: null,
  });
  rerender(<GroceryManualEntryModal {...props} />);
  expect(screen.getByRole('option', { name: 'Crumpets' })).toBeVisible();
  expect(screen.getByRole('option', { name: 'Crushed Chilli' })).toBeVisible();
  expect(name).toHaveAttribute('aria-expanded', 'true');
  expect(name).toHaveAttribute('maxlength', '200');
  expect(screen.queryByText('Link an existing Ingredient (optional)')).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: /^Create / })).not.toBeInTheDocument();

  fireEvent.change(name, { target: { value: 'crum' } });
  expect(screen.getByRole('option', { name: 'Crumpets' })).toBeVisible();
  expect(screen.queryByRole('option', { name: 'Crushed Chilli' })).not.toBeInTheDocument();
  expect(name).toHaveValue('crum');
  fireEvent.change(name, { target: { value: 'unmatched item' } });
  expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
});

test('selects an Ingredient suggestion without creating a master Ingredient', async () => {
  mocks.useGroceryIngredientOptions.mockReturnValue({
    items: [{ id: 'crumpets', name: 'crumpets' }], error: null,
  });
  render(
    <GroceryManualEntryModal expectedListVersion={3} onClose={vi.fn()} onAdded={vi.fn()} />,
  );
  fireEvent.click(screen.getByRole('checkbox', { name: 'Save to master Ingredient list' }));
  const name = screen.getByRole('combobox', { name: 'Name' });
  fireEvent.change(name, { target: { value: 'cru' } });
  fireEvent.click(screen.getByRole('option', { name: 'Crumpets' }));
  expect(name).toHaveValue('Crumpets');
  expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  expect(screen.getByRole('img', { name: 'Linked to existing Ingredient' }))
    .toHaveAttribute('title', 'Linked to existing Ingredient');
  expect(screen.queryByRole('button', { name: 'Use as an ad-hoc item' })).not.toBeInTheDocument();
  expect(screen.queryByText('Linked to an existing Ingredient.')).not.toBeInTheDocument();
  fireEvent.blur(name);
  fireEvent.click(screen.getByRole('button', { name: 'Add to Grocery List' }));
  await waitFor(() => expect(mocks.addGroceryItems).toHaveBeenCalledTimes(1));
  expect(mocks.addGroceryItems.mock.calls[0][0].items[0]).toMatchObject({
    name: 'Crumpets', ingredientId: 'crumpets', saveAsIngredient: false,
  });
});

test.each(['typed', 'edited'])('keeps an exact %s name ad-hoc until explicitly selected', async (mode) => {
  mocks.useGroceryIngredientOptions.mockReturnValue({
    items: [{ id: 'crumpets', name: 'crumpets' }], error: null,
  });
  render(
    <GroceryManualEntryModal expectedListVersion={3} onClose={vi.fn()} onAdded={vi.fn()} />,
  );
  const name = screen.getByRole('combobox', { name: 'Name' });
  fireEvent.change(name, { target: { value: 'crumpets' } });
  if (mode === 'edited') {
    fireEvent.click(screen.getByRole('option', { name: 'Crumpets' }));
    expect(screen.getByRole('img', { name: 'Linked to existing Ingredient' })).toBeVisible();
    fireEvent.change(name, { target: { value: 'crumpets' } });
  }
  fireEvent.blur(name);
  expect(name).toHaveValue('Crumpets');
  expect(screen.queryByRole('img', { name: 'Linked to existing Ingredient' })).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Add to Grocery List' }));
  await waitFor(() => expect(mocks.addGroceryItems).toHaveBeenCalledTimes(1));
  expect(mocks.addGroceryItems.mock.calls[0][0].items[0].ingredientId).toBeNull();
});

test('dismisses suggestions with Escape and selects them using arrows and Enter', () => {
  mocks.useGroceryIngredientOptions.mockReturnValue({
    items: [{ id: 'crumpets', name: 'crumpets' }], error: null,
  });
  render(
    <GroceryManualEntryModal expectedListVersion={3} onClose={vi.fn()} onAdded={vi.fn()} />,
  );
  const name = screen.getByRole('combobox', { name: 'Name' });
  fireEvent.change(name, { target: { value: 'cru' } });
  fireEvent.keyDown(name, { key: 'Escape' });
  expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  fireEvent.keyDown(name, { key: 'ArrowDown' });
  fireEvent.keyDown(name, { key: 'ArrowDown' });
  fireEvent.keyDown(name, { key: 'Enter' });
  expect(name).toHaveValue('Crumpets');
  expect(screen.getByRole('img', { name: 'Linked to existing Ingredient' })).toBeVisible();
  expect(mocks.addGroceryItems).not.toHaveBeenCalled();
});
