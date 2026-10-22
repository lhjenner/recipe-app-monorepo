import { expect, test } from '@playwright/test';
import { mockAuthApi } from '../support/mockAuthApi.ts';

const newWorldStoreId = '67af7315-c4e5-4f9e-89b3-7c3a71ba11da';
const pakNSaveStoreId = 'b4df52fd-667a-41d8-9a9b-9d6e3bc8c87c';
const woolworthsStoreId = '3e50666e-4aac-458f-9ae8-2eacba31ec6b';
const newWorldChainId = '19f8486e-7b2c-4e10-949f-ecf0d2039b36';

const supermarkets = [{
  id: newWorldChainId,
  code: 'new-world',
  name: 'New World',
  stores: [{
    id: newWorldStoreId,
    code: 'new-world-greenmeadows',
    name: 'New World Greenmeadows',
    displayOrder: 1,
  }],
}, {
  id: 'a2d39069-a4d6-4837-8b0b-7f0a858d552a',
  code: 'pak-n-save',
  name: "Pak'n Save",
  stores: [{
    id: pakNSaveStoreId,
    code: 'pak-n-save-tamatea',
    name: "Pak'n Save Tamatea",
    displayOrder: 2,
  }],
}, {
  id: '360fab45-a7c0-4be5-88d8-bb5a272fc2d6',
  code: 'woolworths',
  name: 'Woolworths',
  stores: [{
    id: woolworthsStoreId,
    code: 'woolworths-carlyle',
    name: 'Woolworths Carlyle',
    displayOrder: 3,
  }],
}];

function detail(name: string) {
  return {
    id: '00000000-0000-7000-8000-000000000001',
    name,
    categoryId: null,
    categoryName: null,
    excludeFromGroceryList: false,
    nutrition: null,
    nutritionSource: null,
    nutritionUpdatedAtUtc: null,
    createdAtUtc: '2026-10-22T00:00:00Z',
    updatedAtUtc: '2026-10-22T00:00:00Z',
    archivedAtUtc: null,
    version: 8,
    storeMappings: [
      { storeId: newWorldStoreId, storeCode: 'new-world-greenmeadows', storeName: 'New World Greenmeadows', state: 'unmapped', departmentId: null, departmentName: null },
      { storeId: pakNSaveStoreId, storeCode: 'pak-n-save-tamatea', storeName: "Pak'n Save Tamatea", state: 'unmapped', departmentId: null, departmentName: null },
      { storeId: woolworthsStoreId, storeCode: 'woolworths-carlyle', storeName: 'Woolworths Carlyle', state: 'unmapped', departmentId: null, departmentName: null },
    ],
    productLinks: [],
  };
}

async function mockEditorLookups(page: Parameters<typeof mockAuthApi>[0]) {
  await page.route('**/api/supermarkets', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', json: supermarkets }));
  await page.route('**/api/categories', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    json: route.request().method() === 'POST'
      ? { id: '00000000-0000-7000-8000-000000000010', name: route.request().postDataJSON().name }
      : [],
  }));
  await page.route('**/api/departments', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    json: route.request().method() === 'POST'
      ? { id: '00000000-0000-7000-8000-000000000011', name: route.request().postDataJSON().name }
      : [],
  }));
}

test.describe('Ingredient create and edit', () => {
  test('creates a validated ingredient with lookup, store, product, and nutrition details', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await mockEditorLookups(page);
    let submitted: Record<string, unknown> | null = null;
    let created = false;
    await page.route('**/api/ingredients**', async (route) => {
      if (route.request().method() === 'POST') {
        submitted = route.request().postDataJSON() as Record<string, unknown>;
        created = true;
        await route.fulfill({ status: 201, contentType: 'application/json', json: detail('Green Apple') });
        return;
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        json: {
          items: created ? [{
            id: '00000000-0000-7000-8000-000000000001',
            name: 'Green Apple',
            categoryName: 'Produce',
            excludeFromGroceryList: true,
            archived: false,
            stores: [{
              storeCode: 'new-world-greenmeadows',
              state: 'mapped',
              departmentName: 'Fruit',
            }],
          }] : [],
          totalCount: created ? 1 : 0,
          page: 1,
          pageSize: 25,
        },
      });
    });

    await page.goto('/ingredient-maintenance/new');
    await page.getByRole('textbox', { name: 'Name' }).fill('  Green   Apple ');
    await page.getByRole('combobox', { name: 'Category' }).fill('Produce');
    await page.getByRole('button', { name: "Create 'Produce'" }).click();
    await page.getByRole('combobox', { name: 'New World Greenmeadows department' }).fill('Fruit');
    await page.getByRole('button', { name: "Create 'Fruit'" }).click();
    await page.getByRole('checkbox', { name: 'Not stocked' }).nth(1).check();
    await page.getByRole('button', { name: 'Add product link' }).click();
    await page.getByRole('textbox', { name: 'Product link 1 URL' }).fill('https://example.com/green-apples');
    await page.getByRole('textbox', { name: 'Variant label (optional)' }).fill('Granny Smith');
    await page.getByRole('spinbutton', { name: 'Energy (kcal)' }).fill('52');
    await page.getByRole('checkbox', { name: 'Exclude from grocery list' }).check();
    await page.getByRole('button', { name: 'Save ingredient' }).click();

    await expect(page.getByRole('heading', { name: 'Ingredient Maintenance' })).toBeVisible();
    await expect(page.getByRole('table', { name: 'Ingredients' }).getByText('Green Apple')).toBeVisible();
    expect(submitted).toMatchObject({
      name: '  Green   Apple ',
      excludeFromGroceryList: true,
      nutrition: { energyKcal: 52 },
      storeMappings: [
        { storeId: newWorldStoreId, departmentId: '00000000-0000-7000-8000-000000000011' },
        { storeId: pakNSaveStoreId, departmentId: null, isNotStocked: true },
        { storeId: woolworthsStoreId, departmentId: null, isNotStocked: false },
      ],
      productLinks: [{
        chainId: newWorldChainId,
        url: 'https://example.com/green-apples',
        variantLabel: 'Granny Smith',
      }],
    });
  });

  test('preserves edited values and offers reload after a version conflict', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await mockEditorLookups(page);
    await page.route('**/api/ingredients/00000000-0000-7000-8000-000000000001', async (route) => {
      if (route.request().method() === 'PUT') {
        await route.fulfill({
          status: 409,
          contentType: 'application/problem+json',
          json: { status: 409, title: 'Conflict', detail: 'This ingredient was changed elsewhere.' },
        });
        return;
      }
      await route.fulfill({ status: 200, contentType: 'application/json', json: detail('Green Apple') });
    });

    await page.goto('/ingredient-maintenance/00000000-0000-7000-8000-000000000001');
    const name = page.getByRole('textbox', { name: 'Name' });
    await expect(name).toHaveValue('Green Apple');
    await name.fill('Edited Green Apple');
    await page.getByRole('button', { name: 'Save ingredient' }).click();

    await expect(page.getByRole('alert')).toContainText('This ingredient was changed elsewhere');
    await expect(name).toHaveValue('Edited Green Apple');
    await expect(page.getByRole('button', { name: 'Reload latest' })).toBeVisible();
  });

  test('loads stored names in title case and title-cases typed values on leaving a field', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await mockEditorLookups(page);
    await page.route('**/api/ingredients/00000000-0000-7000-8000-000000000001', (route) => route.fulfill({
      status: 200,
      contentType: 'application/json',
      json: {
        ...detail('agar agar'),
        categoryId: '00000000-0000-7000-8000-000000000020',
        categoryName: 'baking aisle',
      },
    }));
    await page.route('**/api/ingredients?**', (route) => route.fulfill({
      status: 200,
      contentType: 'application/json',
      json: { items: [], totalCount: 0, page: 1, pageSize: 25 },
    }));

    await page.goto('/ingredient-maintenance/00000000-0000-7000-8000-000000000001');
    const name = page.getByRole('textbox', { name: 'Name' });
    await expect(name).toHaveValue('Agar Agar');
    await expect(page.getByRole('combobox', { name: 'Category' })).toHaveValue('Baking Aisle');

    let prompted = false;
    page.on('dialog', (dialog) => {
      prompted = true;
      void dialog.dismiss();
    });
    await name.focus();
    await name.blur();
    await page.getByRole('link', { name: 'Home', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Home' })).toBeVisible();
    expect(prompted).toBe(false);

    await page.goto('/ingredient-maintenance/new');
    const department = page.getByRole('combobox', { name: 'New World Greenmeadows department' });
    await page.getByRole('textbox', { name: 'Name' }).fill('salt and pepper');
    await department.fill('fruit & veg');
    await expect(page.getByRole('textbox', { name: 'Name' })).toHaveValue('Salt and Pepper');
    await page.getByRole('textbox', { name: 'Name' }).focus();
    await expect(department).toHaveValue('Fruit & Veg');
  });

  test('confirms when leaving with unsaved values', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await mockEditorLookups(page);
    await page.route('**/api/ingredients**', (route) => route.fulfill({
      status: 200,
      contentType: 'application/json',
      json: { items: [], totalCount: 0, page: 1, pageSize: 25 },
    }));
    await page.goto('/ingredient-maintenance');
    await page.getByRole('link', { name: 'Add ingredient' }).click();
    const name = page.getByRole('textbox', { name: 'Name' });
    await name.fill('Unsaved Ingredient');

    const backPrompt = page.waitForEvent('dialog');
    const backNavigation = page.goBack().catch(() => null);
    await (await backPrompt).dismiss();
    await backNavigation;
    await expect(name).toBeVisible();

    const linkPrompt = page.waitForEvent('dialog');
    await page.getByRole('link', { name: 'Home', exact: true }).click();
    await (await linkPrompt).accept();
    await expect(page.getByRole('heading', { name: 'Home' })).toBeVisible();
  });
});
