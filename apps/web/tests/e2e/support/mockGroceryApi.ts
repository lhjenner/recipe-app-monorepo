import type { Page } from '@playwright/test';
import type { GroceryHistory, GroceryList } from '../../../src/features/grocery-list/services/groceryListApi.js';
import { storeDefinitions, supermarkets } from './mockIngredientListApi.js';

export const selectedStoreId = storeDefinitions[0].id;

const ingredientId = '00000000-0000-7000-8000-000000000101';

export const groceryList: GroceryList = {
  id: '00000000-0000-7000-8000-000000000201',
  version: 1,
  completedItemCount: 0,
  items: [
    groceryItem('00000000-0000-7000-8000-000000000301', 'green apples', ingredientId, [
      { storeId: selectedStoreId, state: 'mapped', departmentName: 'produce' },
    ]),
    groceryItem('00000000-0000-7000-8000-000000000302', 'kale', ingredientId, [
      { storeId: selectedStoreId, state: 'notStocked', departmentName: null },
    ]),
    groceryItem('00000000-0000-7000-8000-000000000303', 'table salt'),
    {
      ...groceryItem('00000000-0000-7000-8000-000000000304', 'rolled oats'),
      adHocStoreId: selectedStoreId,
      adHocDepartmentName: 'pantry',
    },
  ],
};

const groceryHistory: GroceryHistory = {
  items: [{
    id: '00000000-0000-7000-8000-000000000401',
    groceryItemId: '00000000-0000-7000-8000-000000000301',
    kind: 'purchase',
    quantity: 2,
    purchaseUnit: 'item',
    occurredAtUtc: '2026-04-04T11:00:00Z',
    itemNameSnapshot: 'green apples',
    noteSnapshot: null,
    storeId: selectedStoreId,
    storeName: 'New World Greenmeadows',
    origin: 'manual',
    reversesEventId: null,
    isReversed: true,
  }],
  totalCount: 1,
  page: 1,
  pageSize: 25,
};

export async function mockGroceryApi(page: Page): Promise<string[]> {
  const requests: string[] = [];
  await page.route('**/api/supermarkets', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', json: supermarkets }));
  await page.route('**/api/grocery-list/purchase-history**', async (route) => {
    requests.push(route.request().url());
    await route.fulfill({ status: 200, contentType: 'application/json', json: groceryHistory });
  });
  await page.route('**/api/grocery-list', async (route) => {
    requests.push(route.request().url());
    await route.fulfill({ status: 200, contentType: 'application/json', json: groceryList });
  });
  return requests;
}

function groceryItem(
  id: string,
  name: string,
  linkedIngredientId: string | null = null,
  storeMappings: GroceryList['items'][number]['storeMappings'] = [],
): GroceryList['items'][number] {
  return {
    id,
    ingredientId: linkedIngredientId,
    name,
    state: 'outstanding',
    note: null,
    dimension: null,
    countLabel: null,
    requirementAmount: null,
    requirementUnit: null,
    outstandingPurchaseQuantity: 2,
    purchaseUnit: 'item',
    adHocStoreId: null,
    adHocDepartmentName: null,
    storeMappings,
    contributions: [],
  };
}
