import { afterEach, expect, test, vi } from 'vitest';
import {
  getIngredient,
  getIngredientList,
  getSupermarkets,
  IngredientsApiError,
} from '../../src/services/ingredientsApi';

afterEach(() => {
  vi.unstubAllGlobals();
});

test('requests a paged ingredient list and validates its response', async () => {
  const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
    items: [{
      id: '00000000-0000-7000-8000-000000000001',
      name: 'Apple',
      categoryName: null,
      excludeFromGroceryList: false,
      archived: false,
      stores: [{
        id: '67af7315-c4e5-4f9e-89b3-7c3a71ba11da',
        storeCode: 'new-world-greenmeadows',
        state: 'mapped',
        departmentName: 'Produce',
      }],
    }],
    totalCount: 1,
    page: 1,
    pageSize: 25,
  }), { status: 200 }));
  vi.stubGlobal('fetch', fetchMock);

  const list = await getIngredientList({
    search: ' apple ',
    page: 1,
    pageSize: 25,
    archived: false,
  });

  expect(fetchMock).toHaveBeenCalledWith(
    '/api/ingredients?page=1&pageSize=25&archived=false&search=apple',
    expect.objectContaining({ credentials: 'same-origin' }),
  );
  expect(list.items[0]?.stores[0]?.state).toBe('mapped');
});

test('rejects an ingredient list response outside the Zod contract', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
    items: [{ id: 'not-a-guid' }],
    totalCount: 1,
    page: 1,
    pageSize: 25,
  }), { status: 200 })));

  await expect(getIngredientList({
    search: '',
    page: 1,
    pageSize: 25,
    archived: false,
  })).rejects.toBeInstanceOf(IngredientsApiError);
});

test('validates ingredient detail timestamps and nested response data', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
    id: '00000000-0000-7000-8000-000000000001',
    name: 'Apple',
    categoryId: null,
    categoryName: null,
    excludeFromGroceryList: false,
    nutrition: null,
    nutritionSource: null,
    nutritionUpdatedAtUtc: null,
    createdAtUtc: '2026-10-22T00:00:00+00:00',
    updatedAtUtc: '2026-10-22T00:00:00Z',
    archivedAtUtc: null,
    version: 1,
    storeMappings: [],
    productLinks: [],
  }), { status: 200 })));

  const detail = await getIngredient('00000000-0000-7000-8000-000000000001');

  expect(detail.name).toBe('Apple');
});

test('validates supermarkets and surfaces RFC problem details', async () => {
  const fetchMock = vi.fn()
    .mockResolvedValueOnce(new Response(JSON.stringify([{
      id: '19f8486e-7b2c-4e10-949f-ecf0d2039b36',
      code: 'new-world',
      name: 'New World',
      stores: [{
        id: '67af7315-c4e5-4f9e-89b3-7c3a71ba11da',
        code: 'new-world-greenmeadows',
        name: 'New World Greenmeadows',
        displayOrder: 1,
      }],
    }]), { status: 200 }))
    .mockResolvedValueOnce(new Response(JSON.stringify({
      status: 400,
      title: 'Invalid ingredient query',
    }), { status: 400, headers: { 'Content-Type': 'application/problem+json' } }));
  vi.stubGlobal('fetch', fetchMock);

  const supermarkets = await getSupermarkets();
  expect(supermarkets[0]?.stores[0]?.displayOrder).toBe(1);

  await expect(getIngredientList({
    search: '',
    page: 999,
    pageSize: 25,
    archived: false,
  })).rejects.toMatchObject({
    status: 400,
    message: 'Invalid ingredient query',
  });
});
