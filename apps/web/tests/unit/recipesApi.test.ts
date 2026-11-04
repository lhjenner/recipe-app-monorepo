import { afterEach, expect, test, vi } from 'vitest';
import {
  getRecipe,
  getRecipeImage,
  getRecipeList,
  RecipeApiError,
} from '../../src/services/recipesApi';

afterEach(() => {
  vi.unstubAllGlobals();
});

const recipeId = '019a0000-0000-7000-8000-000000000001';
const validDetail = {
  id: recipeId,
  name: 'Tofu bowl',
  sourceText: null,
  sourceUrl: null,
  servingsText: '2-3',
  prepTimeText: null,
  cookTimeText: null,
  dateAddedAtUtc: null,
  tags: [{ position: 0, text: 'quick' }],
  steps: [{
    position: 0,
    items: [{
      position: 0,
      kind: 'ingredient',
      text: 'tofu snapshot',
      ingredientName: 'Firm tofu',
      quantityText: '1/2',
      unitText: 'block',
      notesText: null,
    }],
    instructions: 'Mix.\nServe.',
  }],
  image: {
    url: `/api/recipes/${recipeId}/image`,
    width: 640,
    height: 480,
    sha256: 'a'.repeat(64),
  },
};

test('requests and validates a paged Recipe list', async () => {
  const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
    items: [{ id: recipeId, name: 'Tofu bowl' }],
    totalCount: 1,
    page: 2,
    pageSize: 25,
  }), { status: 200 }));
  vi.stubGlobal('fetch', fetchMock);
  const controller = new AbortController();

  const list = await getRecipeList({ search: ' tofu ', page: 2, pageSize: 25 }, controller.signal);

  expect(fetchMock).toHaveBeenCalledWith(
    '/api/recipes?page=2&pageSize=25&search=tofu',
    expect.objectContaining({ credentials: 'same-origin', signal: controller.signal }),
  );
  expect(list.items[0]?.id).toBe(recipeId);
  expect(list.page).toBe(2);
});

test.each([
  [{ id: 'legacy-id', name: 'Not a GUID' }],
  [{ id: recipeId, name: 7 }],
])('rejects invalid Recipe list response data', async (item) => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
    items: [item],
    totalCount: 1,
    page: 1,
    pageSize: 25,
  }), { status: 200 })));

  await expect(getRecipeList({ search: '', page: 1, pageSize: 25 }))
    .rejects.toBeInstanceOf(RecipeApiError);
});

test('validates ordered detail data and rejects non-GUID lookup keys before fetching', async () => {
  const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(validDetail), { status: 200 }));
  vi.stubGlobal('fetch', fetchMock);

  const detail = await getRecipe(recipeId);

  expect(fetchMock).toHaveBeenCalledWith(
    `/api/recipes/${recipeId}`,
    expect.objectContaining({ credentials: 'same-origin' }),
  );
  expect(detail.steps[0]?.items[0]?.kind).toBe('ingredient');
  expect(detail.image?.url).toBe(`/api/recipes/${recipeId}/image`);

  await expect(getRecipe('legacy-recipe-id')).rejects.toBeInstanceOf(RecipeApiError);
  expect(fetchMock).toHaveBeenCalledTimes(1);
});

test.each(['ingredient', 'heading', 'note'])('accepts the API camelCase item kind %s', async (kind) => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
    ...validDetail,
    steps: [{
      ...validDetail.steps[0],
      items: [{ ...validDetail.steps[0]!.items[0], kind }],
    }],
  }), { status: 200 })));

  const detail = await getRecipe(recipeId);

  expect(detail.steps[0]?.items[0]?.kind).toBe(kind);
});

test('rejects invalid detail discriminators and non-application image URLs', async () => {
  vi.stubGlobal('fetch', vi.fn()
    .mockResolvedValueOnce(new Response(JSON.stringify({
      ...validDetail,
      steps: [{ ...validDetail.steps[0], items: [{ ...validDetail.steps[0]!.items[0], kind: 'Unknown' }] }],
    }), { status: 200 }))
    .mockResolvedValueOnce(new Response(JSON.stringify({
      ...validDetail,
      image: { ...validDetail.image, url: 'https://example.test/image.png' },
    }), { status: 200 })));

  await expect(getRecipe(recipeId)).rejects.toBeInstanceOf(RecipeApiError);
  await expect(getRecipe(recipeId)).rejects.toBeInstanceOf(RecipeApiError);
});

test('surfaces Problem Details and propagates an aborted request', async () => {
  vi.stubGlobal('fetch', vi.fn()
    .mockResolvedValueOnce(new Response(JSON.stringify({
      status: 503,
      title: 'Image storage unavailable',
    }), { status: 503, headers: { 'Content-Type': 'application/problem+json' } }))
    .mockRejectedValueOnce(new DOMException('Aborted', 'AbortError')));

  await expect(getRecipe(recipeId)).rejects.toMatchObject({
    name: 'RecipeApiError',
    status: 503,
    message: 'Image storage unavailable',
  });

  const controller = new AbortController();
  controller.abort();
  await expect(getRecipeList({ search: '', page: 1, pageSize: 25 }, controller.signal))
    .rejects.toMatchObject({ name: 'AbortError' });
});

test('preserves paging validation errors from Problem Details', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
    status: 400,
    title: 'Invalid recipe query',
    errors: { Page: ['Page must not exceed the last page of results.'] },
  }), { status: 400, headers: { 'Content-Type': 'application/problem+json' } })));

  await expect(getRecipeList({ search: '', page: 99, pageSize: 25 }))
    .rejects.toMatchObject({
      status: 400,
      fieldErrors: { Page: ['Page must not exceed the last page of results.'] },
    });
});

test('loads only a same-origin private image endpoint and returns its binary response', async () => {
  const image = new Uint8Array([137, 80, 78, 71]);
  const fetchMock = vi.fn().mockResolvedValue(new Response(image, {
    status: 200,
    headers: { 'Content-Type': 'image/png' },
  }));
  vi.stubGlobal('fetch', fetchMock);
  const controller = new AbortController();

  const result = await getRecipeImage(`/api/recipes/${recipeId}/image`, controller.signal);

  expect(fetchMock).toHaveBeenCalledWith(
    `/api/recipes/${recipeId}/image`,
    expect.objectContaining({ credentials: 'same-origin', signal: controller.signal }),
  );
  expect(result.type).toBe('image/png');
  expect(result.size).toBeGreaterThan(0);
  await expect(getRecipeImage('https://example.test/image.png')).rejects.toBeInstanceOf(RecipeApiError);
  expect(fetchMock).toHaveBeenCalledTimes(1);
});

test('rejects non-image and unavailable image responses', async () => {
  vi.stubGlobal('fetch', vi.fn()
    .mockResolvedValueOnce(new Response('not image', {
      status: 200,
      headers: { 'Content-Type': 'text/html' },
    }))
    .mockResolvedValueOnce(new Response(JSON.stringify({
      status: 503,
      title: 'Storage unavailable',
    }), { status: 503, headers: { 'Content-Type': 'application/problem+json' } })));

  await expect(getRecipeImage(`/api/recipes/${recipeId}/image`))
    .rejects.toBeInstanceOf(RecipeApiError);
  await expect(getRecipeImage(`/api/recipes/${recipeId}/image`))
    .rejects.toMatchObject({ status: 503, message: 'Storage unavailable' });
});
