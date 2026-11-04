import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { SESSION_EXPIRED_EVENT } from '../../src/services/apiClient';
import {
  getRecipe,
  getRecipeImage,
  getRecipeList,
  RecipeApiError,
  type RecipeDetail,
} from '../../src/services/recipesApi';
import { useRecipe, useRecipeImage, useRecipeList } from '../../src/hooks/useRecipeData';

vi.mock('../../src/services/recipesApi', async (importOriginal) => ({
  ...await importOriginal<typeof import('../../src/services/recipesApi')>(),
  getRecipe: vi.fn(),
  getRecipeImage: vi.fn(),
  getRecipeList: vi.fn(),
}));

const getRecipeMock = vi.mocked(getRecipe);
const getRecipeImageMock = vi.mocked(getRecipeImage);
const getRecipeListMock = vi.mocked(getRecipeList);
const detail: RecipeDetail = {
  id: '019a0000-0000-7000-8000-000000000001',
  name: 'Tofu bowl',
  sourceText: null,
  sourceUrl: null,
  servingsText: null,
  prepTimeText: null,
  cookTimeText: null,
  dateAddedAtUtc: null,
  tags: [],
  steps: [],
  image: null,
};

beforeEach(() => {
  vi.useFakeTimers();
  getRecipeMock.mockReset();
  getRecipeImageMock.mockReset();
  getRecipeListMock.mockReset();
});

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

test('debounces nonempty list searches and aborts superseded requests', async () => {
  getRecipeListMock.mockImplementation((_query, signal) => new Promise((_resolve, reject) => {
    signal?.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')));
  }));
  const { rerender } = renderHook(
    ({ search }) => useRecipeList({ search, page: 1, pageSize: 25 }),
    { initialProps: { search: 'tofu' } },
  );

  await act(async () => { await vi.advanceTimersByTimeAsync(299); });
  expect(getRecipeListMock).not.toHaveBeenCalled();
  await act(async () => { await vi.advanceTimersByTimeAsync(1); });
  expect(getRecipeListMock).toHaveBeenCalledTimes(1);

  const firstSignal = getRecipeListMock.mock.calls[0]?.[1];
  rerender({ search: 'pasta' });
  expect(firstSignal?.aborted).toBe(true);
  await act(async () => { await vi.advanceTimersByTimeAsync(300); });
  expect(getRecipeListMock).toHaveBeenCalledTimes(2);
});

test('does not expose late list results after the query changes', async () => {
  let resolveFirst: ((value: Awaited<ReturnType<typeof getRecipeList>>) => void) | undefined;
  getRecipeListMock
    .mockImplementationOnce(() => new Promise((resolve) => { resolveFirst = resolve; }))
    .mockResolvedValueOnce({ items: [], totalCount: 0, page: 1, pageSize: 25 });
  const { result, rerender } = renderHook(
    ({ search }) => useRecipeList({ search, page: 1, pageSize: 25 }),
    { initialProps: { search: '' } },
  );

  await act(async () => { await vi.advanceTimersByTimeAsync(1); });
  expect(getRecipeListMock).toHaveBeenCalledTimes(1);
  rerender({ search: 'new query' });
  await act(async () => { await vi.advanceTimersByTimeAsync(301); });
  expect(result.current.data).toEqual({ items: [], totalCount: 0, page: 1, pageSize: 25 });

  await act(async () => {
    resolveFirst?.({ items: [], totalCount: 99, page: 1, pageSize: 25 });
  });
  expect(result.current.data?.totalCount).toBe(0);
});

test('loads Recipe details and exposes service failures for the page to render', async () => {
  getRecipeMock.mockResolvedValueOnce(detail)
    .mockRejectedValueOnce(new RecipeApiError('Recipe unavailable', 404));
  const { result, rerender } = renderHook(({ id }) => useRecipe(id), {
    initialProps: { id: detail.id },
  });

  await act(async () => { await vi.advanceTimersByTimeAsync(0); });
  expect(result.current.data).toEqual(detail);

  rerender({ id: '019a0000-0000-7000-8000-000000000002' });
  await act(async () => { await vi.advanceTimersByTimeAsync(0); });
  expect(result.current.error).toBe('Recipe unavailable');
  expect(result.current.status).toBe(404);
});

test('releases temporary image URLs on image changes, unmount, and session expiry', async () => {
  const firstObjectUrl = 'blob:first';
  const secondObjectUrl = 'blob:second';
  const createObjectURL = vi.spyOn(URL, 'createObjectURL')
    .mockReturnValueOnce(firstObjectUrl)
    .mockReturnValueOnce(secondObjectUrl);
  const revokeObjectURL = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
  getRecipeImageMock.mockResolvedValue(new Blob(['image'], { type: 'image/png' }));
  const { result, rerender, unmount } = renderHook(({ imageUrl }) => useRecipeImage(imageUrl), {
    initialProps: { imageUrl: '/api/recipes/019a0000-0000-7000-8000-000000000001/image' as string | null },
  });

  await act(async () => { await vi.advanceTimersByTimeAsync(0); });
  expect(result.current.url).toBe(firstObjectUrl);
  rerender({ imageUrl: '/api/recipes/019a0000-0000-7000-8000-000000000002/image' });
  expect(revokeObjectURL).toHaveBeenCalledWith(firstObjectUrl);
  await act(async () => { await vi.advanceTimersByTimeAsync(0); });
  expect(result.current.url).toBe(secondObjectUrl);

  act(() => window.dispatchEvent(new Event(SESSION_EXPIRED_EVENT)));
  expect(revokeObjectURL).toHaveBeenCalledWith(secondObjectUrl);
  expect(result.current.url).toBeNull();
  unmount();
  expect(createObjectURL).toHaveBeenCalledTimes(2);
});
