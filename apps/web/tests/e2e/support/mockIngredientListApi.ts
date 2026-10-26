import type { Page, Route } from '@playwright/test';

export interface StoreState {
  storeCode: string;
  state: 'mapped' | 'notStocked' | 'unmapped';
  departmentName: string | null;
}

export interface IngredientSummary {
  id: string;
  name: string;
  categoryName: string | null;
  excludeFromGroceryList: boolean;
  archived: boolean;
  stores: StoreState[];
}

export const storeDefinitions = [
  { id: '67af7315-c4e5-4f9e-89b3-7c3a71ba11da', code: 'new-world-greenmeadows', name: 'New World Greenmeadows', displayOrder: 1 },
  { id: 'b4df52fd-667a-41d8-9a9b-9d6e3bc8c87c', code: 'pak-n-save-tamatea', name: "Pak'n Save Tamatea", displayOrder: 2 },
  { id: '3e50666e-4aac-458f-9ae8-2eacba31ec6b', code: 'woolworths-carlyle', name: 'Woolworths Carlyle', displayOrder: 3 },
];

export const supermarkets = [
  {
    id: '19f8486e-7b2c-4e10-949f-ecf0d2039b36',
    code: 'new-world',
    name: 'New World',
    stores: [storeDefinitions[0]],
  },
  {
    id: 'a2d39069-a4d6-4837-8b0b-7f0a858d552a',
    code: 'pak-n-save',
    name: "Pak'n Save",
    stores: [storeDefinitions[1]],
  },
  {
    id: '360fab45-a7c0-4be5-88d8-bb5a272fc2d6',
    code: 'woolworths',
    name: 'Woolworths',
    stores: [storeDefinitions[2]],
  },
];
let nextIngredientId = 0;

export function ingredient(
  name: string,
  {
    categoryName = 'Produce',
    excludeFromGroceryList = false,
    archived = false,
    stores = storeDefinitions.map((store) => ({
      storeCode: store.code,
      state: 'unmapped' as const,
      departmentName: null,
    })),
  }: Partial<Omit<IngredientSummary, 'id' | 'name'>> = {},
): IngredientSummary {
  return {
    id: `00000000-0000-7000-8000-${String(++nextIngredientId).padStart(12, '0')}`,
    name,
    categoryName,
    excludeFromGroceryList,
    archived,
    stores,
  };
}

export async function mockIngredientApi(
  page: Page,
  ingredients: IngredientSummary[],
): Promise<string[]> {
  const requests: string[] = [];
  await page.route('**/api/supermarkets', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', json: supermarkets }));
  await page.route('**/api/ingredients**', async (route: Route) => {
    const url = new URL(route.request().url());
    if (route.request().method() === 'POST') {
      const [, id, action] = url.pathname.match(/^\/api\/ingredients\/([^/]+)\/(archive|restore)$/) ?? [];
      const target = ingredients.find((item) => item.id === id);
      if (!target || !action) {
        await route.fulfill({ status: 404 });
        return;
      }
      target.archived = action === 'archive';
      requests.push(`POST ${url.pathname}`);
      await route.fulfill({ status: 204 });
      return;
    }

    requests.push(url.search);
    const archived = url.searchParams.get('archived') === 'true';
    const search = url.searchParams.get('search')?.toLocaleLowerCase() ?? '';
    const pageNumber = Number(url.searchParams.get('page') ?? 1);
    const pageSize = Number(url.searchParams.get('pageSize') ?? 25);
    const filtered = ingredients
      .filter((item) => item.archived === archived && item.name.toLocaleLowerCase().includes(search))
      .sort((first, second) => first.name.localeCompare(second.name));
    const offset = (pageNumber - 1) * pageSize;
    if (pageNumber > Math.max(1, Math.ceil(filtered.length / pageSize))) {
      await route.fulfill({
        status: 400,
        contentType: 'application/problem+json',
        json: {
          title: 'One or more validation errors occurred.',
          status: 400,
          errors: { Page: ['Page must not exceed the last page of results.'] },
        },
      });
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      json: {
        items: filtered.slice(offset, offset + pageSize),
        totalCount: filtered.length,
        page: pageNumber,
        pageSize,
      },
    });
  });
  return requests;
}
