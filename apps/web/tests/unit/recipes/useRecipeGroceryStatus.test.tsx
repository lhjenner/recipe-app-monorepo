import { act, renderHook, waitFor } from '@testing-library/react';
import { beforeEach, expect, test, vi } from 'vitest';
import { useRecipeGroceryStatus } from '../../../src/features/recipes/hooks/useRecipeGroceryStatus';
import { getGroceryList, type GroceryItem } from '../../../src/features/grocery-list/services/groceryListApi';

vi.mock('../../../src/features/grocery-list/services/groceryListApi', () => ({
  getGroceryList: vi.fn(),
}));
const getList = vi.mocked(getGroceryList);

beforeEach(() => vi.resetAllMocks());

function item(state: GroceryItem['state'], recipeId: string | null, contributionState: GroceryItem['state'] = state): GroceryItem {
  return {
    id: state, ingredientId: state, name: 'Apple', state, note: null,
    dimension: 'count', countLabel: 'item', requirementAmount: 1, requirementUnit: 'item',
    outstandingPurchaseQuantity: 1, purchaseUnit: 'item', ingredientVersion: null,
    adHocStoreId: null, adHocDepartmentId: null, adHocDepartmentName: null, storeMappings: [],
    contributions: [{
      id: 'source', origin: recipeId ? 'recipe' : 'manual', state: contributionState,
      originalAmountText: '1', originalUnitText: null, originalNotesText: null,
      interpretedAmount: 1, canonicalUnit: 'item', unresolvedReason: null, recipeId,
      recipeStepId: null, recipeStepItemId: null, recipeNameSnapshot: recipeId ? 'Recipe' : null,
    }],
  };
}

test('uses outstanding ingredient identity independently of recipe attribution and refreshes after changes', async () => {
  getList.mockResolvedValueOnce({
    id: 'list', version: 1, completedItemCount: 1,
    items: [item('outstanding', 'other'), item('completed', 'recipe'), item('cancelled', 'recipe')],
  }).mockResolvedValueOnce({
    id: 'list', version: 2, completedItemCount: 1,
    items: [item('outstanding', 'recipe')],
  }).mockResolvedValueOnce({
    id: 'list', version: 3, completedItemCount: 1,
    items: [item('outstanding', 'recipe', 'cancelled')],
  });
  const { result } = renderHook(() => useRecipeGroceryStatus('recipe'));
  await waitFor(() => expect(result.current.ingredientIds.has('outstanding')).toBe(true));
  expect([...result.current.ingredientIds]).toEqual(['outstanding']);
  expect(result.current.hasOutstandingAddition).toBe(false);
  act(() => result.current.reload());
  await waitFor(() => expect(result.current.hasOutstandingAddition).toBe(true));
  act(() => result.current.reload());
  await waitFor(() => expect(getList).toHaveBeenCalledTimes(3));
  await waitFor(() => expect(result.current.ingredientIds.has('outstanding')).toBe(true));
  expect(result.current.hasOutstandingAddition).toBe(false);
});

test('surfaces a failed status check and allows retry instead of silently claiming no grocery demand', async () => {
  getList.mockRejectedValueOnce(new Error('Connection failed.')).mockResolvedValueOnce({
    id: 'list', version: 1, completedItemCount: 0, items: [item('outstanding', null)],
  });
  const { result } = renderHook(() => useRecipeGroceryStatus('recipe'));
  await waitFor(() => expect(result.current.error).toBe('Connection failed.'));
  act(() => result.current.reload());
  await waitFor(() => expect(result.current.ingredientIds.has('outstanding')).toBe(true));
  expect(result.current.error).toBeNull();
  expect(result.current.hasOutstandingAddition).toBe(false);
});
