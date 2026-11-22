import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, expect, test, vi } from 'vitest';
import RecipeGroceryActions from '../../../src/features/recipes/components/RecipeGroceryActions';
import {
  addRecipeToGroceryList,
  getRecipeGroceryPreview,
  GroceryListApiError,
  removeRecipeGroceryRequirements,
  type GroceryRecipePreview,
} from '../../../src/features/grocery-list/services/groceryListApi';

vi.mock('../../../src/features/grocery-list/services/groceryListApi', async (importOriginal) => ({
  ...await importOriginal<typeof import('../../../src/features/grocery-list/services/groceryListApi')>(),
  addRecipeToGroceryList: vi.fn(),
  getRecipeGroceryPreview: vi.fn(),
  removeRecipeGroceryRequirements: vi.fn(),
}));

const preview: GroceryRecipePreview = {
  recipeId: '019a0000-0000-7000-8000-000000000001',
  recipeName: 'tomato soup',
  recipeVersion: 4,
  ingredientFingerprint: 'A'.repeat(64),
  groceryListId: null,
  listVersion: 0,
  hasOutstandingAddition: false,
  included: [{
    recipeStepId: '019a0000-0000-7000-8000-000000000002',
    stepPosition: 0,
    recipeStepItemId: '019a0000-0000-7000-8000-000000000003',
    itemPosition: 0,
    ingredientId: '019a0000-0000-7000-8000-000000000004',
    ingredientName: 'tomato',
    ingredientIsArchived: true,
    originalNameText: 'sliced tomato',
    originalAmountText: '1-2',
    originalUnitText: 'tsp',
    originalNotesText: 'finely sliced',
    interpretationStatus: 'unresolved',
    interpretedAmount: null,
    dimension: null,
    canonicalUnit: null,
    countLabel: null,
    unresolvedReason: 'The quantity is not a complete supported number.',
  }],
  excluded: [{
    stepPosition: 0,
    itemPosition: 1,
    text: 'For serving',
    reason: 'Headings and notes are not grocery items.',
  }],
};

const addMock = vi.mocked(addRecipeToGroceryList);
const previewMock = vi.mocked(getRecipeGroceryPreview);
const removeMock = vi.mocked(removeRecipeGroceryRequirements);

beforeEach(() => {
  vi.clearAllMocks();
  previewMock.mockResolvedValue(preview);
  addMock.mockResolvedValue({
    commandId: '019a0000-0000-7000-8000-000000000005',
    resultListVersion: 1,
    resultIds: ['019a0000-0000-7000-8000-000000000006'],
  });
  removeMock.mockResolvedValue({
    commandId: '019a0000-0000-7000-8000-000000000005',
    resultListVersion: 1,
    resultIds: [],
  });
});

function renderActions(hasOutstandingAddition = false) {
  return render(
    <MemoryRouter>
      <RecipeGroceryActions recipeId={preview.recipeId} hasOutstandingAddition={hasOutstandingAddition} />
    </MemoryRouter>,
  );
}

test('reviews eligible, archived, excluded and unresolved items before adding with reviewed versions', async () => {
  renderActions();
  fireEvent.click(screen.getByRole('button', { name: 'Add to Grocery List' }));

  expect(await screen.findByText('Tomato')).toBeInTheDocument();
  expect(screen.queryByText(/Sliced tomato/)).not.toBeInTheDocument();
  expect(screen.queryByText(/finely sliced/)).not.toBeInTheDocument();
  expect(screen.getByText(/archived Ingredient reference/)).toBeInTheDocument();
  expect(screen.getByText(/Needs review: The quantity is not a complete supported number/)).toBeInTheDocument();
  expect(screen.getByText(/For serving — Headings and notes are not grocery items/)).toBeInTheDocument();
  expect(addMock).not.toHaveBeenCalled();

  fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Confirm' }));
  await waitFor(() => expect(addMock).toHaveBeenCalledOnce());
  expect(addMock).toHaveBeenCalledWith(preview.recipeId, {
    commandId: expect.any(String),
    expectedListVersion: 0,
    expectedRecipeVersion: 4,
    expectedIngredientFingerprint: preview.ingredientFingerprint,
    confirmRepeat: false,
  });
  expect(await screen.findByRole('link', { name: 'View Grocery List' }))
    .toHaveAttribute('href', '/grocery-list');
});

test('allows cancelling a first-time review after a stale list conflict', async () => {
  addMock.mockRejectedValueOnce(new GroceryListApiError('List changed.', 409));
  renderActions();
  fireEvent.click(screen.getByRole('button', { name: 'Add to Grocery List' }));
  const dialog = await screen.findByRole('dialog');
  fireEvent.click(await within(dialog).findByRole('button', { name: 'Confirm' }));
  await screen.findByRole('button', { name: 'Review latest Recipe and Grocery List' });
  fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
});

test.each(['cubed', '(cubed)'])('omits cooking notes "%s" from unitless ingredient review', async (notes) => {
  previewMock.mockResolvedValue({
    ...preview,
    included: [{
      ...preview.included[0],
      ingredientName: 'apple',
      ingredientIsArchived: false,
      originalAmountText: '1',
      originalUnitText: null,
      originalNotesText: notes,
      interpretationStatus: 'known',
      interpretedAmount: 1,
      dimension: 'count',
      canonicalUnit: 'item',
      countLabel: 'item',
      unresolvedReason: null,
    }],
  });
  renderActions();
  fireEvent.click(screen.getByRole('button', { name: 'Add to Grocery List' }));

  const apple = await screen.findByText('Apple');
  expect(apple.parentElement?.textContent).toBe('Apple — 1');
  expect(screen.queryByText(/cubed/)).not.toBeInTheDocument();
});

test('requires a deliberate repeat action and removes only outstanding Recipe demand', async () => {
  previewMock.mockResolvedValue({ ...preview, hasOutstandingAddition: true, listVersion: 8 });
  renderActions(true);
  fireEvent.click(screen.getByRole('button', { name: 'Add to Grocery List' }));
  expect(await screen.findByText(/already on your Grocery List/)).toBeInTheDocument();
  expect(screen.queryByText(/Included \(/)).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Remove this Recipe’s outstanding requirements' })).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Confirm' }));

  await waitFor(() => expect(addMock).toHaveBeenCalledOnce());
  expect(addMock.mock.calls[0]?.[1]).toMatchObject({
    expectedListVersion: 8,
    confirmRepeat: true,
  });

  fireEvent.click(screen.getByRole('button', { name: 'Remove from Grocery List' }));
  await screen.findByRole('button', { name: 'Confirm' });
  const removalCancel = screen.getByRole('button', { name: 'Cancel' });
  expect(removalCancel).toHaveClass('btn-danger-secondary');
  expect(removalCancel.parentElement).toBe(screen.getByRole('button', { name: 'Confirm' }).parentElement);
  expect(screen.getByRole('alert')).toHaveTextContent('Manual additions and purchase history will remain.');
  fireEvent.click(screen.getByRole('button', { name: 'Confirm' }));
  await waitFor(() => expect(removeMock).toHaveBeenCalledOnce());
  expect(removeMock).toHaveBeenCalledWith(preview.recipeId, {
    commandId: expect.any(String),
    expectedListVersion: 8,
  });

});

test('allows cancelling removal when preview fails or requires a fresh review', async () => {
  previewMock.mockRejectedValueOnce(new Error('Preview unavailable.'));
  renderActions(true);
  fireEvent.click(screen.getByRole('button', { name: 'Remove from Grocery List' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Preview unavailable.');
  fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
  expect(removeMock).not.toHaveBeenCalled();

  previewMock.mockResolvedValue({ ...preview, hasOutstandingAddition: true });
  removeMock.mockRejectedValueOnce(new GroceryListApiError('List changed.', 409));
  fireEvent.click(screen.getByRole('button', { name: 'Remove from Grocery List' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Confirm' }));
  expect(await screen.findByRole('button', { name: 'Review latest Recipe and Grocery List' })).toBeVisible();
  const cancel = screen.getByRole('button', { name: 'Cancel' });
  expect(cancel).toHaveClass('btn-danger-secondary');
  fireEvent.click(cancel);
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
});

test('does not offer removal without this recipe contribution and cancelling repeat makes no changes', async () => {
  previewMock.mockResolvedValue({ ...preview, hasOutstandingAddition: true });
  renderActions();
  expect(screen.queryByRole('button', { name: 'Remove from Grocery List' })).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Add to Grocery List' }));
  const cancel = await screen.findByRole('button', { name: 'Cancel' });
  expect(cancel).toHaveClass('btn-danger-secondary');
  expect(cancel.parentElement).toBe(screen.getByRole('button', { name: 'Confirm' }).parentElement);
  fireEvent.click(cancel);
  expect(addMock).not.toHaveBeenCalled();
  expect(removeMock).not.toHaveBeenCalled();
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
});

test('retries an ambiguous add with the exact same command payload', async () => {
  addMock.mockRejectedValueOnce(new GroceryListApiError('Response lost.', 0));
  renderActions();
  fireEvent.click(screen.getByRole('button', { name: 'Add to Grocery List' }));
  await screen.findByText('Tomato');
  fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Confirm' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Retry original command' }));

  await waitFor(() => expect(addMock).toHaveBeenCalledTimes(2));
  expect(addMock.mock.calls[1]).toEqual(addMock.mock.calls[0]);
});

test.each([
  new GroceryListApiError('Server failed after submission.', 500),
  new GroceryListApiError('Invalid receipt.', 201),
  new SyntaxError('Unreadable receipt.'),
])('retains the original command for an ambiguous response: $message', async (failure) => {
  addMock.mockRejectedValueOnce(failure);
  renderActions();
  fireEvent.click(screen.getByRole('button', { name: 'Add to Grocery List' }));
  const dialog = await screen.findByRole('dialog');
  fireEvent.click(await within(dialog).findByRole('button', { name: 'Confirm' }));
  const retry = await within(dialog).findByRole('button', { name: 'Retry original command' });
  expect(within(dialog).getByRole('button', { name: 'Cancel' })).toBeDisabled();
  fireEvent.click(retry);
  await waitFor(() => expect(addMock).toHaveBeenCalledTimes(2));
  expect(addMock.mock.calls[1]).toEqual(addMock.mock.calls[0]);
});

test('prevents concurrent retries of an uncertain Recipe command', async () => {
  let finish: (value: Awaited<ReturnType<typeof addRecipeToGroceryList>>) => void = () => {};
  addMock.mockRejectedValueOnce(new GroceryListApiError('Response lost.', 0))
    .mockReturnValueOnce(new Promise((resolve) => { finish = resolve; }));
  renderActions();
  fireEvent.click(screen.getByRole('button', { name: 'Add to Grocery List' }));
  const dialog = await screen.findByRole('dialog');
  fireEvent.click(await within(dialog).findByRole('button', { name: 'Confirm' }));
  const retry = await within(dialog).findByRole('button', { name: 'Retry original command' });
  fireEvent.click(retry);
  expect(retry).toBeDisabled();
  fireEvent.click(retry);
  expect(addMock).toHaveBeenCalledTimes(2);
  finish({ commandId: '019a0000-0000-7000-8000-000000000005', resultListVersion: 1, resultIds: [] });
  await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
});

test('retries an uncertain Recipe removal with the original payload', async () => {
  previewMock.mockResolvedValue({ ...preview, hasOutstandingAddition: true });
  removeMock.mockRejectedValueOnce(new GroceryListApiError('Server failed after submission.', 503));
  renderActions(true);
  fireEvent.click(screen.getByRole('button', { name: 'Remove from Grocery List' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Confirm' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Retry original command' }));
  await waitFor(() => expect(removeMock).toHaveBeenCalledTimes(2));
  expect(removeMock.mock.calls[1]).toEqual(removeMock.mock.calls[0]);
});

test('a stale version requires a fresh preview before another command', async () => {
  addMock.mockRejectedValueOnce(new GroceryListApiError('The grocery list changed after review.', 409));
  previewMock.mockResolvedValueOnce(preview).mockResolvedValueOnce({
    ...preview,
    recipeVersion: 5,
    listVersion: 9,
    ingredientFingerprint: 'B'.repeat(64),
  });
  renderActions();
  fireEvent.click(screen.getByRole('button', { name: 'Add to Grocery List' }));
  await screen.findByText('Tomato');
  fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Confirm' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Review latest Recipe and Grocery List' }));

  await waitFor(() => expect(previewMock).toHaveBeenCalledTimes(2));
  fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Confirm' }));
  await waitFor(() => expect(addMock).toHaveBeenCalledTimes(2));
  expect(addMock.mock.calls[1]?.[1]).toMatchObject({
    expectedRecipeVersion: 5,
    expectedListVersion: 9,
    expectedIngredientFingerprint: 'B'.repeat(64),
  });
});
