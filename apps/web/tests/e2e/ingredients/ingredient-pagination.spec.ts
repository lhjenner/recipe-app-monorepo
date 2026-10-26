import { expect, test, type Page } from '@playwright/test';
import { mockAuthApi } from '../support/mockAuthApi.ts';
import {
  ingredient,
  mockIngredientApi,
  storeDefinitions,
  type IngredientSummary,
} from '../support/mockIngredientListApi.ts';

function numberedIngredients(count: number): IngredientSummary[] {
  return Array.from({ length: count }, (_, index) =>
    ingredient(`Ingredient ${String(index + 1).padStart(3, '0')}`));
}

function pagination(page: Page) {
  return page.getByRole('navigation', { name: 'Ingredient pages' });
}

async function expectCurrentPage(page: Page, pageNumber: number) {
  await expect(pagination(page).getByRole('button', { name: `Page ${pageNumber}`, exact: true }))
    .toHaveAttribute('aria-current', 'page');
}

async function mockEditorDetail(page: Page, summary: IngredientSummary) {
  await page.route('**/api/categories', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', json: [] }));
  await page.route('**/api/departments', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', json: [] }));
  await page.route(`**/api/ingredients/${summary.id}`, (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    json: {
      id: summary.id,
      name: summary.name,
      categoryId: null,
      categoryName: null,
      excludeFromGroceryList: false,
      nutrition: null,
      nutritionSource: null,
      nutritionUpdatedAtUtc: null,
      createdAtUtc: '2026-10-22T00:00:00Z',
      updatedAtUtc: '2026-10-22T00:00:00Z',
      archivedAtUtc: null,
      version: 1,
      storeMappings: storeDefinitions.map((store) => ({
        storeId: store.id,
        storeCode: store.code,
        storeName: store.name,
        state: 'unmapped',
        departmentId: null,
        departmentName: null,
      })),
      productLinks: [],
    },
  }));
}

test.describe('Ingredient Maintenance pagination', () => {
  test.beforeEach(async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
  });

  test('AC-05 jumps straight to the last page from the numbered pages', async ({ page }) => {
    await mockIngredientApi(page, numberedIngredients(200));
    await page.goto('/ingredient-maintenance');

    await expectCurrentPage(page, 1);
    await expect(pagination(page).getByRole('button', { name: /^Page \d+$/ }))
      .toHaveText(['1', '2', '3', '8']);
    await pagination(page).getByRole('button', { name: 'Page 8' }).click();

    await expectCurrentPage(page, 8);
    await expect(page).toHaveURL(/\?page=8$/);
    await expect(page.getByText('Showing 176–200 of 200')).toBeVisible();
    await expect(page.getByRole('row', { name: /Ingredient 200/ })).toBeVisible();
    await expect(pagination(page).getByRole('button', { name: 'Next page' })).toBeDisabled();
  });

  test('AC-05 restores the previous page with the browser Back button', async ({ page }) => {
    await mockIngredientApi(page, numberedIngredients(200));
    await page.goto('/ingredient-maintenance');

    await pagination(page).getByRole('button', { name: 'Next page' }).click();
    await expectCurrentPage(page, 2);
    await pagination(page).getByRole('button', { name: 'Page 8' }).click();
    await expectCurrentPage(page, 8);

    await page.goBack();
    await expectCurrentPage(page, 2);
    await expect(page.getByText('Showing 26–50 of 200')).toBeVisible();
  });

  test('AC-05 restores search, archived and page from a shared URL', async ({ page }) => {
    const ingredients = numberedIngredients(60).map((item) => ({ ...item, archived: true }));
    const requests = await mockIngredientApi(page, ingredients);
    await page.goto('/ingredient-maintenance?search=ingredient&archived=true&page=2');

    await expect(page.getByRole('searchbox', { name: 'Search ingredients' })).toHaveValue('ingredient');
    await expect(page.getByRole('checkbox', { name: 'Show archived' })).toBeChecked();
    await expectCurrentPage(page, 2);
    expect(requests.some((query) =>
      query.includes('page=2') && query.includes('archived=true') && query.includes('search=ingredient'))).toBe(true);
  });

  test('AC-05 falls back to the first page for an invalid page in the URL', async ({ page }) => {
    await mockIngredientApi(page, numberedIngredients(30));
    await page.goto('/ingredient-maintenance?page=abc');

    await expectCurrentPage(page, 1);
    await expect(page.getByText('Showing 1–25 of 30')).toBeVisible();
  });

  test('AC-05 moves to the last available page when the URL is past the end', async ({ page }) => {
    await mockIngredientApi(page, numberedIngredients(30));
    await page.goto('/ingredient-maintenance?page=99');

    await expectCurrentPage(page, 2);
    await expect(page).toHaveURL(/\?page=2$/);
    await expect(page.getByText('Showing 26–30 of 30')).toBeVisible();
  });

  test('AC-08 moves back a page after archiving the only ingredient on the last page', async ({ page }) => {
    await mockIngredientApi(page, numberedIngredients(26));
    await page.goto('/ingredient-maintenance?page=2');
    await expect(page.getByText('Showing 26–26 of 26')).toBeVisible();

    await page.getByRole('button', { name: 'Archive Ingredient 026' }).first().click();
    await page.getByRole('dialog', { name: 'Archive ingredient?' })
      .getByRole('button', { name: 'Archive' }).click();

    await expect(pagination(page)).toBeHidden();
    await expect(page).toHaveURL(/\/ingredient-maintenance$/);
    await expect(page.getByText('Showing 1–25 of 25')).toBeVisible();
  });

  test('AC-05 returns to the same list page and search after cancelling an edit', async ({ page }) => {
    const ingredients = numberedIngredients(60);
    await mockIngredientApi(page, ingredients);
    await mockEditorDetail(page, ingredients[30]);
    await page.goto('/ingredient-maintenance?search=ingredient&page=2');
    await expectCurrentPage(page, 2);

    await page.getByRole('link', { name: 'Edit Ingredient 031' }).first().click();
    await expect(page.getByRole('heading', { name: 'Edit Ingredient' })).toBeVisible();
    await page.getByRole('button', { name: 'Cancel' }).click();

    await expect(page).toHaveURL(/\/ingredient-maintenance\?search=ingredient&page=2$/);
    await expectCurrentPage(page, 2);
    await expect(page.getByRole('searchbox', { name: 'Search ingredients' })).toHaveValue('ingredient');
  });

  test('AC-11 shows only the first, current and last page on mobile', async ({ page }) => {
    await mockIngredientApi(page, numberedIngredients(200));
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/ingredient-maintenance?page=4');

    await expect(pagination(page).getByRole('button', { name: /^Page \d+$/ }))
      .toHaveText(['1', '4', '8']);
    await expect(pagination(page).getByRole('button', { name: 'Previous page' })).toBeVisible();
    await expect(pagination(page).getByText('Previous', { exact: true })).toHaveCSS('position', 'absolute');
  });
});
