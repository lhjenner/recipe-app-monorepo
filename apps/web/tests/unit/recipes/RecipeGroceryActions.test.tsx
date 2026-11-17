import { fireEvent, render, screen, waitFor } from '@testing-library/react';
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

function renderActions() {
  return render(
    <MemoryRouter>
      <RecipeGroceryActions recipeId={preview.recipeId} />
    </MemoryRouter>,
  );
}

test('reviews eligible, archived, excluded and unresolved items before adding with reviewed versions', async () => {
  renderActions();
  fireEvent.click(screen.getByRole('button', { name: 'Add to grocery list' }));

  expect(await screen.findByText('Tomato')).toBeInTheDocument();
  expect(screen.queryByText(/Sliced tomato/)).not.toBeInTheDocument();
  expect(screen.getByText(/archived Ingredient reference/)).toBeInTheDocument();
  expect(screen.getByText(/Needs review: The quantity is not a complete supported number/)).toBeInTheDocument();
  expect(screen.getByText(/For serving — Headings and notes are not grocery items/)).toBeInTheDocument();
  expect(addMock).not.toHaveBeenCalled();

  fireEvent.click(screen.getByRole('button', { name: 'Add to Grocery List' }));
  await waitFor(() => expect(addMock).toHaveBeenCalledOnce());
  expect(addMock).toHaveBeenCalledWith(preview.recipeId, {
    commandId: expect.any(String),
    expectedListVersion: 0,
    expectedRecipeVersion: 4,
    confirmRepeat: false,
  });
  expect(await screen.findByRole('link', { name: 'View Grocery List' }))
    .toHaveAttribute('href', '/grocery-list');
});

test('requires a deliberate repeat action and removes only outstanding Recipe demand', async () => {
  previewMock.mockResolvedValue({ ...preview, hasOutstandingAddition: true, listVersion: 8 });
  renderActions();
  fireEvent.click(screen.getByRole('button', { name: 'Add to grocery list' }));
  fireEvent.click(await screen.findByRole('button', { name: /Confirm and add another Recipe/ }));

  await waitFor(() => expect(addMock).toHaveBeenCalledOnce());
  expect(addMock.mock.calls[0]?.[1]).toMatchObject({
    expectedListVersion: 8,
    confirmRepeat: true,
  });

  fireEvent.click(screen.getByRole('button', { name: 'Add to grocery list' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Remove this Recipe’s outstanding requirements' }));
  expect(screen.getByRole('alert')).toHaveTextContent('Manual additions and purchase history will remain.');
  fireEvent.click(screen.getByRole('button', { name: 'Confirm removal' }));
  await waitFor(() => expect(removeMock).toHaveBeenCalledOnce());
  expect(removeMock).toHaveBeenCalledWith(preview.recipeId, {
    commandId: expect.any(String),
    expectedListVersion: 8,
  });
});

test('retries an ambiguous add with the exact same command payload', async () => {
  addMock.mockRejectedValueOnce(new GroceryListApiError('Response lost.', 0));
  renderActions();
  fireEvent.click(screen.getByRole('button', { name: 'Add to grocery list' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Add to Grocery List' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Retry original command' }));

  await waitFor(() => expect(addMock).toHaveBeenCalledTimes(2));
  expect(addMock.mock.calls[1]).toEqual(addMock.mock.calls[0]);
});

test('a stale version requires a fresh preview before another command', async () => {
  addMock.mockRejectedValueOnce(new GroceryListApiError('The grocery list changed after review.', 409));
  previewMock.mockResolvedValueOnce(preview).mockResolvedValueOnce({
    ...preview,
    recipeVersion: 5,
    listVersion: 9,
  });
  renderActions();
  fireEvent.click(screen.getByRole('button', { name: 'Add to grocery list' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Add to Grocery List' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Review latest Recipe and Grocery List' }));

  await waitFor(() => expect(previewMock).toHaveBeenCalledTimes(2));
  fireEvent.click(await screen.findByRole('button', { name: 'Add to Grocery List' }));
  await waitFor(() => expect(addMock).toHaveBeenCalledTimes(2));
  expect(addMock.mock.calls[1]?.[1]).toMatchObject({
    expectedRecipeVersion: 5,
    expectedListVersion: 9,
  });
});
