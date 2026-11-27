import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { useGroceryList } from '../../../src/features/grocery-list/hooks/useGroceryList';
import { getGroceryList, type GroceryList } from '../../../src/features/grocery-list/services/groceryListApi';
import { groceryList } from '../../e2e/support/mockGroceryApi';

vi.mock('../../../src/features/grocery-list/services/groceryListApi', () => ({
  getGroceryList: vi.fn(),
}));

const getGroceryListMock = vi.mocked(getGroceryList);
const emptyList: GroceryList = { id: null, version: 0, completedItemCount: 0, items: [] };

beforeEach(() => {
  vi.useFakeTimers();
  getGroceryListMock.mockReset();
  Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'visible' });
});

afterEach(() => {
  vi.useRealTimers();
});

test('pauses while hidden and refreshes immediately when visible again', async () => {
  getGroceryListMock.mockResolvedValue(emptyList);
  const { unmount } = renderHook(() => useGroceryList());
  await act(async () => {});

  act(() => {
    Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'hidden' });
    document.dispatchEvent(new Event('visibilitychange'));
    vi.advanceTimersByTime(30_000);
  });
  expect(getGroceryListMock).toHaveBeenCalledTimes(1);

  await act(async () => {
    Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'visible' });
    document.dispatchEvent(new Event('visibilitychange'));
  });
  expect(getGroceryListMock).toHaveBeenCalledTimes(2);
  unmount();
});

test('refreshes immediately after reconnecting', async () => {
  getGroceryListMock.mockResolvedValue(emptyList);
  const { unmount } = renderHook(() => useGroceryList());
  await act(async () => {});
  expect(getGroceryListMock).toHaveBeenCalledTimes(1);

  await act(async () => {
    window.dispatchEvent(new Event('online'));
  });
  expect(getGroceryListMock).toHaveBeenCalledTimes(2);
  unmount();
});

test('retires a pending read on reconnect and ignores its late response', async () => {
  let resolveDisconnected: ((value: GroceryList) => void) | undefined;
  let resolveReconnected: ((value: GroceryList) => void) | undefined;
  getGroceryListMock
    .mockResolvedValueOnce(emptyList)
    .mockImplementationOnce(() => new Promise<GroceryList>((resolve) => { resolveDisconnected = resolve; }))
    .mockImplementationOnce(() => new Promise<GroceryList>((resolve) => { resolveReconnected = resolve; }));
  const { result } = renderHook(() => useGroceryList());
  await act(async () => {});

  await act(async () => {
    await vi.advanceTimersByTimeAsync(15_000);
  });
  const disconnectedSignal = getGroceryListMock.mock.calls[1][0];
  await act(async () => {
    window.dispatchEvent(new Event('online'));
  });
  expect(disconnectedSignal?.aborted).toBe(true);
  expect(getGroceryListMock).toHaveBeenCalledTimes(3);

  await act(async () => {
    resolveDisconnected?.({ ...emptyList, version: 99 });
    await vi.advanceTimersByTimeAsync(15_000);
  });
  expect(result.current.data).toEqual(emptyList);
  expect(getGroceryListMock).toHaveBeenCalledTimes(3);

  const reconnectedList = { ...emptyList, id: 'list', version: 1, completedItemCount: 1 };
  await act(async () => {
    resolveReconnected?.(reconnectedList);
  });
  expect(result.current.data).toEqual(reconnectedList);
  expect(result.current.isStale).toBe(false);
});

test.each([
  { change: 'list creation', next: { ...emptyList, id: 'list', version: 1 }, updated: true },
  { change: 'completed count', next: { ...emptyList, completedItemCount: 1 }, updated: true },
  { change: 'version only', next: { ...emptyList, version: 1 }, updated: false },
])('announces $change only when displayed data changes', async ({ next, updated }) => {
  getGroceryListMock.mockResolvedValueOnce(emptyList).mockResolvedValue(next);
  const { result } = renderHook(() => useGroceryList());
  await act(async () => {});
  expect(result.current.updateCount).toBe(0);

  await act(async () => {
    await vi.advanceTimersByTimeAsync(15_000);
  });
  expect(result.current.data).toEqual(next);
  expect(result.current.updateCount).toBe(updated ? 1 : 0);
});

test('signals each changed read while ignoring unchanged reads', async () => {
  const first = { ...emptyList, id: 'list', version: 1 };
  const second = { ...first, version: 2, completedItemCount: 1 };
  getGroceryListMock.mockResolvedValueOnce(emptyList)
    .mockResolvedValueOnce(first)
    .mockResolvedValueOnce(second)
    .mockResolvedValue(second);
  const { result } = renderHook(() => useGroceryList());
  await act(async () => {});
  await act(async () => result.current.reload());
  expect(result.current.updateCount).toBe(1);
  await act(async () => result.current.reload());
  expect(result.current.updateCount).toBe(2);
  await act(async () => result.current.reload());
  expect(result.current.updateCount).toBe(2);
});

test.each([false, true])('suppresses only the local purchase update (other change: %s)', async (otherChange) => {
  const previous = structuredClone(groceryList);
  const next = structuredClone(previous);
  next.version = 2;
  next.completedItemCount = 1;
  next.items[0].state = 'completed';
  next.items[0].outstandingPurchaseQuantity = 0;
  if (otherChange) next.items[1].name = 'updated elsewhere';
  getGroceryListMock.mockResolvedValueOnce(previous).mockResolvedValue(next);
  const { result } = renderHook(() => useGroceryList());
  await act(async () => {});
  await act(async () => result.current.reload({ version: 2, itemId: previous.items[0].id }));
  expect(result.current.updateCount).toBe(otherChange ? 1 : 0);
});

test.each([false, true])('handles reordered store mappings during a purchase (other change: %s)', async (otherChange) => {
  const previous = structuredClone(groceryList);
  previous.items[1].storeMappings.push({
    storeId: '00000000-0000-7000-8000-000000000999',
    state: 'unmapped',
    departmentId: null,
    departmentName: null,
  });
  const next = structuredClone(previous);
  next.version = 2;
  next.completedItemCount = 1;
  next.items = next.items.filter((item) => item.id !== previous.items[0].id);
  next.items.find((item) => item.id === previous.items[1].id)!.storeMappings.reverse();
  if (otherChange) next.items[0].storeMappings[0].state = 'notStocked';
  getGroceryListMock.mockResolvedValueOnce(previous).mockResolvedValue(next);
  const { result } = renderHook(() => useGroceryList());
  await act(async () => {});
  await act(async () => result.current.reload({ version: 2, itemId: previous.items[0].id }));
  expect(result.current.updateCount).toBe(otherChange ? 1 : 0);
});

test('does not announce a poll that only reorders store mappings', async () => {
  const previous = structuredClone(groceryList);
  previous.items[1].storeMappings.push({
    storeId: '00000000-0000-7000-8000-000000000999',
    state: 'unmapped',
    departmentId: null,
    departmentName: null,
  });
  const next = structuredClone(previous);
  next.items[1].storeMappings.reverse();
  getGroceryListMock.mockResolvedValueOnce(previous).mockResolvedValue(next);
  const { result } = renderHook(() => useGroceryList());
  await act(async () => {});
  await act(async () => result.current.reload());
  expect(result.current.updateCount).toBe(0);
});

test('retires an in-flight poll before refreshing a successful purchase', async () => {
  const previous = structuredClone(groceryList);
  const next = structuredClone(previous);
  next.version = 2;
  next.completedItemCount = 1;
  next.items[0].state = 'completed';
  next.items[0].outstandingPurchaseQuantity = 0;
  let resolvePoll: ((list: GroceryList) => void) | undefined;
  getGroceryListMock.mockResolvedValueOnce(previous)
    .mockImplementationOnce(() => new Promise<GroceryList>((resolve) => { resolvePoll = resolve; }))
    .mockResolvedValueOnce(next);
  const { result } = renderHook(() => useGroceryList());
  await act(async () => {});
  await act(async () => result.current.reload());
  const pollSignal = getGroceryListMock.mock.calls[1][0];
  await act(async () => result.current.reload({ version: 2, itemId: previous.items[0].id }));
  expect(pollSignal?.aborted).toBe(true);
  expect(result.current.data).toEqual(next);
  expect(result.current.updateCount).toBe(0);
  await act(async () => resolvePoll?.(previous));
  expect(result.current.data).toEqual(next);
});

test('keeps the last good list stale after a failed poll and clears stale only on success', async () => {
  getGroceryListMock
    .mockResolvedValueOnce(emptyList)
    .mockRejectedValueOnce(new Error('offline'))
    .mockResolvedValueOnce(emptyList);
  const { result } = renderHook(() => useGroceryList());
  await act(async () => {});
  expect(result.current.lastSuccessfulAt).not.toBeNull();

  await act(async () => {
    await vi.advanceTimersByTimeAsync(15_000);
  });
  expect(result.current.data).toEqual(emptyList);
  expect(result.current.isStale).toBe(true);

  await act(async () => {
    await vi.advanceTimersByTimeAsync(15_000);
  });
  expect(result.current.isStale).toBe(false);
});

test('does not overlap slow reads and announces only changed list data', async () => {
  const updatedList = {
    ...emptyList,
    items: [{
      id: '00000000-0000-7000-8000-000000000001',
      ingredientId: null,
      name: 'Apples',
      state: 'outstanding' as const,
      note: null,
      dimension: null,
      countLabel: null,
      requirementAmount: null,
      requirementUnit: null,
      outstandingPurchaseQuantity: 1,
      purchaseUnit: 'item',
      ingredientVersion: null,
      adHocStoreId: null,
      adHocDepartmentId: null,
      adHocDepartmentName: null,
      storeMappings: [],
      contributions: [],
    }],
  };
  let resolveInitial: ((value: GroceryList) => void) | undefined;
  getGroceryListMock
    .mockImplementationOnce(() => new Promise<GroceryList>((resolve) => { resolveInitial = resolve; }))
    .mockResolvedValueOnce(updatedList)
    .mockResolvedValue(updatedList);
  const { result } = renderHook(() => useGroceryList());

  await act(async () => {
    await vi.advanceTimersByTimeAsync(30_000);
  });
  expect(getGroceryListMock).toHaveBeenCalledTimes(1);

  await act(async () => {
    resolveInitial?.(emptyList);
  });
  expect(result.current.data).toEqual(emptyList);

  await act(async () => {
    await vi.advanceTimersByTimeAsync(15_000);
  });
  expect(getGroceryListMock).toHaveBeenCalledTimes(2);
  expect(result.current.updateCount).toBe(1);

  await act(async () => {
    await vi.advanceTimersByTimeAsync(15_000);
  });
  expect(getGroceryListMock).toHaveBeenCalledTimes(3);
  expect(result.current.updateCount).toBe(1);
});
