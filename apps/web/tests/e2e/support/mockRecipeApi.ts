import type { Page, Route } from '@playwright/test';

export const recipeId = '019a0000-0000-7000-8000-000000000001';
export const secondRecipeId = '019a0000-0000-7000-8000-000000000002';

export interface RecipeSummary {
  id: string;
  name: string;
}

export interface RecipeDetail {
  id: string;
  name: string;
  sourceText: string | null;
  sourceUrl: string | null;
  servingsText: string | null;
  prepTimeText: string | null;
  cookTimeText: string | null;
  dateAddedAtUtc: string | null;
  tags: { position: number; text: string }[];
  steps: {
    position: number;
    items: {
      position: number;
      kind: 'Ingredient' | 'Heading' | 'Note';
      text: string;
      ingredientName: string | null;
      quantityText: string | null;
      unitText: string | null;
      notesText: string | null;
    }[];
    instructions: string;
  }[];
  image: { url: string; width: number; height: number; sha256: string } | null;
}

export const recipeDetail: RecipeDetail = {
  id: recipeId,
  name: 'tofu bowl',
  sourceText: 'Family recipe',
  sourceUrl: 'https://example.test/recipe',
  servingsText: '2-3',
  prepTimeText: 'about 15 minutes',
  cookTimeText: null,
  dateAddedAtUtc: '2026-10-25T00:00:00Z',
  tags: [{ position: 0, text: 'quick' }, { position: 1, text: 'weeknight' }],
  steps: [
    {
      position: 0,
      items: [
        {
          position: 0,
          kind: 'Heading',
          text: 'For the sauce',
          ingredientName: null,
          quantityText: null,
          unitText: null,
          notesText: null,
        },
        {
          position: 1,
          kind: 'Ingredient',
          text: 'firm tofu',
          ingredientName: 'firm tofu',
          quantityText: '1/2',
          unitText: 'block',
          notesText: 'pressed',
        },
        {
          position: 2,
          kind: 'Note',
          text: '<script>not executable</script>',
          ingredientName: null,
          quantityText: null,
          unitText: null,
          notesText: null,
        },
      ],
      instructions: 'Mix gently.\nDo not overwork.',
    },
    { position: 1, items: [], instructions: 'Serve warm.' },
  ],
  image: {
    url: `/api/recipes/${recipeId}/image`,
    width: 1,
    height: 1,
    sha256: 'a'.repeat(64),
  },
};

export interface RecipeApiMockOptions {
  summaries?: RecipeSummary[];
  details?: Record<string, RecipeDetail>;
  listResponse?: (url: URL) => Promise<{ status: number; body: unknown }> |
    { status: number; body: unknown };
  imageResponse?: (route: Route, url: URL) => Promise<void>;
}

export interface RecipeApiMock {
  listRequests: string[];
  imageRequests: string[];
}

export const syntheticPngBytes = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII=',
  'base64',
);

export async function mockRecipeApi(
  page: Page,
  options: RecipeApiMockOptions = {},
): Promise<RecipeApiMock> {
  const summaries = options.summaries ?? [{ id: recipeId, name: recipeDetail.name }];
  const details = options.details ?? { [recipeId]: recipeDetail };
  const mock: RecipeApiMock = { listRequests: [], imageRequests: [] };

  await page.route('**/api/recipes**', async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname.endsWith('/image')) {
      mock.imageRequests.push(url.pathname);
      if (options.imageResponse) {
        await options.imageResponse(route, url);
      } else {
        await route.fulfill({ status: 200, contentType: 'image/png', body: syntheticPngBytes });
      }
      return;
    }

    if (url.pathname !== '/api/recipes') {
      const id = url.pathname.split('/').at(-1) ?? '';
      const detail = details[id];
      await route.fulfill(detail
        ? { status: 200, contentType: 'application/json', json: detail }
        : { status: 404, contentType: 'application/problem+json', json: { status: 404, title: 'Not Found' } });
      return;
    }

    mock.listRequests.push(url.search);
    if (options.listResponse) {
      const response = await options.listResponse(url);
      await route.fulfill({
        status: response.status,
        contentType: response.status === 200 ? 'application/json' : 'application/problem+json',
        json: response.body,
      });
      return;
    }

    const search = url.searchParams.get('search')?.toLocaleLowerCase() ?? '';
    const matched = summaries.filter((item) => item.name.toLocaleLowerCase().includes(search));
    const pageNumber = Number(url.searchParams.get('page') ?? 1);
    const pageSize = Number(url.searchParams.get('pageSize') ?? 25);
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      json: {
        items: matched.slice((pageNumber - 1) * pageSize, pageNumber * pageSize),
        totalCount: matched.length,
        page: pageNumber,
        pageSize,
      },
    });
  });

  return mock;
}
