import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, expect, test, vi } from 'vitest';
import GroceryListPage from '../../../src/features/grocery-list/pages/GroceryListPage';
import { GroceryListApiError } from '../../../src/features/grocery-list/services/groceryListApi';

const mocks = vi.hoisted(() => ({
  cancelGroceryItem: vi.fn(),
  removeRecipeGroceryRequirements: vi.fn(),
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
  removeRecipeGroceryRequirements: mocks.removeRecipeGroceryRequirements,
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
  mocks.removeRecipeGroceryRequirements.mockReset();
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

function addRecipeSources() {
  const state = mocks.useGroceryList();
  const item = state.data.items[0];
  const source = (id: string, recipeId: string | null, state: 'outstanding' | 'completed' | 'cancelled' = 'outstanding') => ({
    id, origin: 'recipe', state, recipeId, recipeNameSnapshot: recipeId === 'recipe-1' ? 'apple crumble' : 'pear tart',
    originalAmountText: '1', originalUnitText: 'item', originalNotesText: null,
    interpretedAmount: 1, canonicalUnit: 'item', unresolvedReason: null, recipeStepId: null, recipeStepItemId: null,
  });
  item.contributions = [
    source('1', 'recipe-1'), source('2', 'recipe-1'), source('3', 'recipe-2'),
    source('4', 'old-recipe', 'cancelled'), source('5', 'completed-recipe', 'completed'),
  ];
  state.data.items.push({ ...item, id: 'item-2', contributions: [source('6', 'recipe-1')] });
  state.data.items.push({ ...item, id: 'item-3', state: 'completed', contributions: [source('7', 'completed-item')] });
  return state;
}

test('lists distinct outstanding recipe sources and confirms removal with the reviewed list version', async () => {
  const state = addRecipeSources();
  mocks.removeRecipeGroceryRequirements.mockResolvedValue({ resultListVersion: 13 });
  render(<MemoryRouter><GroceryListPage /></MemoryRouter>);
  const summary = screen.getByText('Recipes on Grocery List');
  expect(summary.closest('details')).not.toHaveAttribute('open');
  fireEvent.click(summary);
  const recipes = summary.closest('details')!;
  expect(within(recipes).getAllByRole('link')).toHaveLength(2);
  expect(within(recipes).getByRole('link', { name: 'Apple Crumble' })).toHaveAttribute('href', '/recipes/recipe-1');
  expect(within(recipes).getByRole('link', { name: 'Pear Tart' })).toBeVisible();
  fireEvent.click(within(recipes).getByRole('button', { name: 'Remove Apple Crumble from Grocery List' }));
  expect(screen.getByRole('dialog')).toHaveTextContent('Manual additions, other Recipes, and purchase history will remain');
  fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
  expect(mocks.removeRecipeGroceryRequirements).not.toHaveBeenCalled();
  fireEvent.click(within(recipes).getByRole('button', { name: 'Remove Apple Crumble from Grocery List' }));
  state.data.version = 13;
  fireEvent.click(screen.getByRole('button', { name: 'Remove Recipe' }));
  await waitFor(() => expect(mocks.removeRecipeGroceryRequirements).toHaveBeenCalledWith('recipe-1', {
    commandId: expect.any(String), expectedListVersion: 12,
  }));
  await waitFor(() => expect(state.reload).toHaveBeenCalled());
});

test('retains the exact failed recipe removal for retry and blocks other changes', async () => {
  const state = addRecipeSources();
  mocks.removeRecipeGroceryRequirements
    .mockRejectedValueOnce(new GroceryListApiError('Connection lost', 0))
    .mockResolvedValueOnce({ resultListVersion: 13 });
  const { rerender } = render(<MemoryRouter><GroceryListPage /></MemoryRouter>);
  fireEvent.click(screen.getByText('Recipes on Grocery List'));
  fireEvent.click(screen.getByRole('button', { name: 'Remove Apple Crumble from Grocery List' }));
  fireEvent.click(screen.getByRole('button', { name: 'Remove Recipe' }));
  await waitFor(() => expect(mocks.removeRecipeGroceryRequirements).toHaveBeenCalledTimes(1));
  await expect(mocks.removeRecipeGroceryRequirements.mock.results[0].value).rejects.toThrow('Connection lost');
  const retry = await screen.findByRole('button', { name: 'Retry Recipe removal' });
  expect(screen.getByRole('button', { name: 'Add items' })).toBeDisabled();
  expect(screen.getAllByRole('button', { name: 'Increase quantity for Onions' })[0]).toBeDisabled();
  mocks.useGroceryList.mockReturnValue({ ...state, data: { ...state.data, version: 13, items: [] } });
  rerender(<MemoryRouter><GroceryListPage /></MemoryRouter>);
  expect(screen.getByRole('button', { name: 'Add items' })).toBeDisabled();
  fireEvent.click(retry);
  await waitFor(() => expect(mocks.removeRecipeGroceryRequirements).toHaveBeenCalledTimes(2));
  expect(mocks.removeRecipeGroceryRequirements.mock.calls[1]).toEqual(mocks.removeRecipeGroceryRequirements.mock.calls[0]);
});

test('refreshes after a conflict without offering a retry of the stale removal', async () => {
  const state = addRecipeSources();
  mocks.removeRecipeGroceryRequirements.mockRejectedValueOnce(new GroceryListApiError('Stale list', 409));
  render(<MemoryRouter><GroceryListPage /></MemoryRouter>);
  fireEvent.click(screen.getByText('Recipes on Grocery List'));
  fireEvent.click(screen.getByRole('button', { name: 'Remove Apple Crumble from Grocery List' }));
  fireEvent.click(screen.getByRole('button', { name: 'Remove Recipe' }));
  expect(await screen.findByText('Stale list')).toBeInTheDocument();
  expect(state.reload).toHaveBeenCalledOnce();
  expect(screen.queryByRole('button', { name: 'Retry Recipe removal' })).not.toBeInTheDocument();
});

test('waits for the removal receipt version before enabling further changes', async () => {
  const state = addRecipeSources();
  mocks.removeRecipeGroceryRequirements.mockResolvedValueOnce({ resultListVersion: 13 });
  const { rerender } = render(<MemoryRouter><GroceryListPage /></MemoryRouter>);
  fireEvent.click(screen.getByText('Recipes on Grocery List'));
  fireEvent.click(screen.getByRole('button', { name: 'Remove Apple Crumble from Grocery List' }));
  fireEvent.click(screen.getByRole('button', { name: 'Remove Recipe' }));
  await waitFor(() => expect(state.reload).toHaveBeenCalledOnce());
  expect(screen.getByRole('button', { name: 'Add items' })).toBeDisabled();
  mocks.useGroceryList.mockReturnValue({ ...state, data: { ...state.data, version: 13 } });
  rerender(<MemoryRouter><GroceryListPage /></MemoryRouter>);
  expect(screen.getByRole('button', { name: 'Add items' })).toBeEnabled();
});

test('hides the recipe accordion when there are no outstanding recipe sources', () => {
  render(<MemoryRouter><GroceryListPage /></MemoryRouter>);
  expect(screen.queryByText('Recipes on Grocery List')).not.toBeInTheDocument();
});

test('surfaces missing recipe attribution rather than silently omitting it', () => {
  const state = addRecipeSources();
  state.data.items[0].contributions.push({
    ...state.data.items[0].contributions[0], id: 'missing', recipeId: null,
  });
  render(<MemoryRouter><GroceryListPage /></MemoryRouter>);
  fireEvent.click(screen.getByText('Recipes on Grocery List'));
  expect(screen.getByText(/Some Recipe sources lack an identifier/)).toBeInTheDocument();
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
  fireEvent.click(screen.getByRole('button', { name: 'Remove item' }));
  await waitFor(() => expect(mocks.cancelGroceryItem).toHaveBeenCalledTimes(1));
  fireEvent.click(await screen.findByRole('button', { name: 'Retry removal' }));

  await waitFor(() => expect(mocks.cancelGroceryItem).toHaveBeenCalledTimes(2));
  expect(mocks.cancelGroceryItem.mock.calls[1][0]).toBe('item-id');
  expect(mocks.cancelGroceryItem.mock.calls[1][1]).toBe(mocks.cancelGroceryItem.mock.calls[0][1]);
  expect(mocks.cancelGroceryItem.mock.calls[1][2]).toBe(12);
});

test('retains explicit removal retry when a refresh no longer includes the item', async () => {
  mocks.cancelGroceryItem.mockRejectedValueOnce(new Error('Response lost')).mockResolvedValueOnce(undefined);
  const { rerender } = render(<MemoryRouter><GroceryListPage /></MemoryRouter>);
  const state = mocks.useGroceryList.mock.results[0].value;
  fireEvent.click(screen.getAllByRole('button', { name: 'Remove onions from Grocery List' })[0]);
  fireEvent.click(screen.getByRole('button', { name: 'Remove item' }));
  await screen.findByRole('button', { name: 'Retry removal' });
  mocks.useGroceryList.mockReturnValue({ ...state, data: { ...state.data, version: 13, items: [] } });
  rerender(<MemoryRouter><GroceryListPage /></MemoryRouter>);
  expect(screen.queryByRole('button', { name: 'Remove onions from Grocery List' })).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Retry removal' }));
  await waitFor(() => expect(mocks.cancelGroceryItem).toHaveBeenCalledTimes(2));
  expect(mocks.cancelGroceryItem.mock.calls[1]).toEqual(mocks.cancelGroceryItem.mock.calls[0]);
  await waitFor(() => expect(screen.queryByRole('button', { name: 'Retry removal' })).not.toBeInTheDocument());
});

test('a known removal rejection clears retry and refreshes the list', async () => {
  mocks.cancelGroceryItem.mockRejectedValueOnce(new GroceryListApiError('Stale list', 409));
  render(<MemoryRouter><GroceryListPage /></MemoryRouter>);
  const state = mocks.useGroceryList.mock.results[0].value;
  fireEvent.click(screen.getAllByRole('button', { name: 'Remove onions from Grocery List' })[0]);
  fireEvent.click(screen.getByRole('button', { name: 'Remove item' }));
  await screen.findByText('Stale list');
  expect(screen.queryByRole('button', { name: 'Retry removal' })).not.toBeInTheDocument();
  expect(state.reload).toHaveBeenCalledOnce();
});

test('cancelling removal confirmation leaves the grocery item unchanged', () => {
  render(<MemoryRouter><GroceryListPage /></MemoryRouter>);
  fireEvent.click(screen.getAllByRole('button', { name: 'Remove onions from Grocery List' })[0]);
  expect(mocks.cancelGroceryItem).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  expect(mocks.cancelGroceryItem).not.toHaveBeenCalled();
});

test('removal confirms the reviewed version and prevents repeated submissions', async () => {
  let finish: () => void = () => {};
  mocks.cancelGroceryItem.mockReturnValueOnce(new Promise<void>((resolve) => { finish = resolve; }));
  const { rerender } = render(<MemoryRouter><GroceryListPage /></MemoryRouter>);
  const state = mocks.useGroceryList.mock.results[0].value;
  fireEvent.click(screen.getAllByRole('button', { name: 'Remove onions from Grocery List' })[0]);
  mocks.useGroceryList.mockReturnValue({ ...state, data: { ...state.data, version: 13 } });
  rerender(<MemoryRouter><GroceryListPage /></MemoryRouter>);
  fireEvent.click(screen.getByRole('button', { name: 'Remove item' }));
  expect(mocks.cancelGroceryItem.mock.calls[0][2]).toBe(12);
  expect(screen.getAllByRole('button', { name: 'Remove onions from Grocery List' })[0]).toBeDisabled();
  finish();
  await waitFor(() => expect(state.reload).toHaveBeenCalledOnce());
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
