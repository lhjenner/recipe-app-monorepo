import { renderHook, waitFor } from '@testing-library/react';
import { beforeEach, expect, test, vi } from 'vitest';
import { useIngredientList } from '../../src/hooks/useIngredientList';
import { getIngredientList, IngredientsApiError } from '../../src/services/ingredientsApi';

vi.mock('../../src/services/ingredientsApi', async (importOriginal) => ({
  ...await importOriginal<typeof import('../../src/services/ingredientsApi')>(),
  getIngredientList: vi.fn(),
}));

const getIngredientListMock = vi.mocked(getIngredientList);
const firstPage = { items: [], totalCount: 26, page: 1, pageSize: 25 };

beforeEach(() => {
  getIngredientListMock.mockReset();
});

test('falls back to the first page when the API rejects a page past the end', async () => {
  getIngredientListMock
    .mockRejectedValueOnce(new IngredientsApiError('Invalid', 400, { Page: ['Too far'] }))
    .mockResolvedValueOnce(firstPage);

  const { result } = renderHook(() =>
    useIngredientList({ search: '', page: 9, pageSize: 25, archived: false }));

  await waitFor(() => expect(result.current.data).toEqual(firstPage));
  expect(result.current.error).toBeNull();
  expect(getIngredientListMock.mock.calls.map(([query]) => query.page)).toEqual([9, 1]);
});

test('surfaces other list errors without retrying', async () => {
  getIngredientListMock.mockRejectedValueOnce(new IngredientsApiError('Search is too long', 400, { Search: ['Too long'] }));

  const { result } = renderHook(() =>
    useIngredientList({ search: '', page: 2, pageSize: 25, archived: false }));

  await waitFor(() => expect(result.current.error).toBe('Search is too long'));
  expect(getIngredientListMock).toHaveBeenCalledTimes(1);
});
