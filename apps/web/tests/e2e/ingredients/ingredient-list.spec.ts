import { expect, test } from '@playwright/test';
import { mockAuthApi } from '../support/mockAuthApi.ts';
import {
  ingredient,
  mockIngredientApi,
  storeDefinitions,
} from '../support/mockIngredientListApi.ts';

test.describe('Ingredient Maintenance list', () => {
  test('AC-05, AC-08, AC-09, and AC-10 show the desktop table and mapping states', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await mockIngredientApi(page, [
      ingredient('Basil', {
        categoryName: null,
        stores: [
          { storeCode: storeDefinitions[0].code, state: 'mapped', departmentName: 'Produce' },
          { storeCode: storeDefinitions[1].code, state: 'notStocked', departmentName: null },
          { storeCode: storeDefinitions[2].code, state: 'unmapped', departmentName: null },
        ],
      }),
      ingredient('Water', {
        categoryName: null,
        excludeFromGroceryList: true,
        stores: [
          { storeCode: storeDefinitions[0].code, state: 'mapped', departmentName: 'Drinks' },
          { storeCode: storeDefinitions[1].code, state: 'notStocked', departmentName: null },
          { storeCode: storeDefinitions[2].code, state: 'unmapped', departmentName: null },
        ],
      }),
    ]);

    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/ingredient-maintenance');

    await expect(page.getByRole('heading', { name: 'Ingredient Maintenance' })).toBeVisible();
    await expect(page).toHaveTitle('Ingredient Maintenance – Recipe App');
    const table = page.getByRole('table', { name: 'Ingredients' });
    await expect(table).toBeVisible();
    const basilRow = table.getByRole('row', { name: /Basil/ });
    await expect(table.getByRole('columnheader')).toHaveText([
      'Ingredient',
      'Category',
      'New World Greenmeadows',
      "Pak'n Save Tamatea",
      'Woolworths Carlyle',
      'Actions',
    ]);
    await expect(basilRow.getByRole('cell').nth(0)).toHaveText('Missing');
    await expect(basilRow.getByRole('cell').nth(1)).toHaveText('Produce');
    await expect(basilRow.getByText('Not stocked')).toBeVisible();
    await expect(basilRow.getByText('Missing')).toHaveCount(2);
    const waterRow = table.getByRole('row', { name: /Water/ });
    await expect(waterRow.getByRole('rowheader')).toHaveText('Water');
    for (const cell of [0, 1, 2, 3]) {
      await expect(waterRow.getByRole('cell').nth(cell)).toHaveText('Excluded');
    }
    await expect(waterRow.getByText(/Missing|Not stocked|Drinks/)).toHaveCount(0);
    await expect(table.getByText('Excluded from grocery list')).toHaveCount(0);
    await expect(table.getByRole('link', { name: 'Edit Basil' })).toBeVisible();
    await expect(table.getByRole('button', { name: 'Archive Basil' })).toBeVisible();
  });

  test('shows lowercase stored names, categories, and departments in title case', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await mockIngredientApi(page, [
      ingredient('salt and pepper', {
        categoryName: 'baking aisle',
        stores: [
          { storeCode: storeDefinitions[0].code, state: 'mapped', departmentName: 'fruit & veg' },
          { storeCode: storeDefinitions[1].code, state: 'unmapped', departmentName: null },
          { storeCode: storeDefinitions[2].code, state: 'unmapped', departmentName: null },
        ],
      }),
    ]);

    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/ingredient-maintenance');

    const row = page.getByRole('table', { name: 'Ingredients' }).getByRole('row', { name: /Salt and Pepper/ });
    await expect(row.getByRole('rowheader')).toHaveText('Salt and Pepper');
    await expect(row.getByRole('cell').nth(0)).toHaveText('Baking Aisle');
    await expect(row.getByRole('cell').nth(1)).toHaveText('Fruit & Veg');
    await expect(row.getByRole('link', { name: 'Edit Salt and Pepper' })).toBeVisible();
  });

  test('AC-06 and AC-07 debounce search and show the no-match message', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    const requests = await mockIngredientApi(page, [
      ingredient('Green Apple'),
      ingredient('Banana'),
    ]);
    await page.goto('/ingredient-maintenance');

    const search = page.getByRole('searchbox', { name: 'Search ingredients' });
    await search.fill('missing');
    await expect(page.getByText("No ingredients match 'missing'")).toBeVisible();
    expect(requests.some((query) => query.includes('search=missing'))).toBe(true);
  });

  test('AC-05 resets paging after a search and provides pagination controls', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    const ingredients = Array.from({ length: 30 }, (_, index) =>
      ingredient(`Ingredient ${String(index + 1).padStart(2, '0')}`));
    const requests = await mockIngredientApi(page, ingredients);
    await page.goto('/ingredient-maintenance');

    const pagination = page.getByRole('navigation', { name: 'Ingredient pages' });
    await expect(pagination.getByRole('button', { name: 'Page 1' })).toHaveAttribute('aria-current', 'page');
    await expect(page.getByText('Showing 1–25 of 30')).toBeVisible();
    await pagination.getByRole('button', { name: 'Next page' }).click();
    await expect(pagination.getByRole('button', { name: 'Page 2' })).toHaveAttribute('aria-current', 'page');
    await expect(page).toHaveURL(/\?page=2$/);
    await page.getByRole('searchbox', { name: 'Search ingredients' }).fill('ingredient 01');
    await expect(page.getByText('Showing 1–1 of 1')).toBeVisible();
    await expect(pagination).toBeHidden();
    await expect(page).toHaveURL(/\?search=ingredient\+01$/);
    expect(requests.some((query) => query.includes('page=1') && query.includes('search=ingredient'))).toBe(true);
  });

  test('AC-11 renders cards rather than the table at the mobile breakpoint', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    const requests = await mockIngredientApi(page, Array.from({ length: 30 }, (_, index) =>
      ingredient(
        `Ingredient ${String(index + 1).padStart(2, '0')}`,
        {
          excludeFromGroceryList: index < 2,
          categoryName: index === 0 ? null : 'Produce',
        },
      )));
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/ingredient-maintenance');

    await expect(page.getByRole('table', { name: 'Ingredients' })).toBeHidden();
    const card = page.getByRole('article', { name: 'Ingredient 01' });
    await expect(card).toBeVisible();
    const heading = card.getByRole('heading', { name: 'Ingredient 01', exact: true });
    await expect(heading).toBeVisible();
    await expect(heading.getByRole('link')).toHaveCount(0);
    await expect(card.getByRole('link', { name: 'Edit Ingredient 01', exact: true }))
      .toHaveAttribute('href', /\/ingredient-maintenance\/[^/]+$/);
    await expect(card.getByText('New World Greenmeadows')).toBeVisible();
    await expect(card.getByText('Excluded', { exact: true })).toHaveCount(4);
    await expect(card.getByText('Missing')).toHaveCount(0);
    await expect(card.getByText('Excluded from grocery list')).toHaveCount(0);
    const categorisedExcludedCard = page.getByRole('article', { name: 'Ingredient 02' });
    await expect(categorisedExcludedCard.getByText('Produce')).toBeVisible();
    await expect(categorisedExcludedCard.getByText('Excluded', { exact: true })).toHaveCount(3);
    await expect(page.getByRole('article', { name: 'Ingredient 03' }).getByText('Missing')).toHaveCount(3);
    const pagination = page.getByRole('navigation', { name: 'Ingredient pages' });
    await expect(pagination.getByRole('button', { name: 'Page 1' })).toHaveAttribute('aria-current', 'page');
    await pagination.getByRole('button', { name: 'Next page' }).click();
    await expect(pagination.getByRole('button', { name: 'Page 2' })).toHaveAttribute('aria-current', 'page');
    await page.getByRole('searchbox', { name: 'Search ingredients' }).fill('ingredient 01');
    await expect(page.getByRole('article', { name: 'Ingredient 01' })).toBeVisible();
    await expect(pagination).toBeHidden();
    expect(requests.some((query) => query.includes('search=ingredient'))).toBe(true);
  });

  test('AC-12 loads only archived ingredients and renders a restore action', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    const requests = await mockIngredientApi(page, [
      ingredient('Active Ingredient'),
      ingredient('Archived Ingredient', { archived: true }),
    ]);
    await page.goto('/ingredient-maintenance');
    await page.getByRole('checkbox', { name: 'Show archived' }).check();

    const table = page.getByRole('table', { name: 'Ingredients' });
    await expect(table.getByText('Archived Ingredient')).toBeVisible();
    await expect(table.getByText('Active Ingredient')).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Restore Archived Ingredient' })).toBeVisible();
    expect(requests.some((query) => query.includes('archived=true'))).toBe(true);
    await page.getByRole('button', { name: 'Restore Archived Ingredient' }).click();
    await page.getByRole('dialog', { name: 'Restore ingredient?' })
      .getByRole('button', { name: 'Restore', exact: true }).click();
    await expect(page.getByText('No archived ingredients.')).toBeVisible();
    await page.getByRole('checkbox', { name: 'Show archived' }).uncheck();
    await expect(page.getByRole('table', { name: 'Ingredients' }).getByText('Archived Ingredient')).toBeVisible();
    expect(requests.some((request) => request.endsWith('/restore'))).toBe(true);
  });

  test('confirms archive in an app modal before removing the ingredient from the active list', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    const requests = await mockIngredientApi(page, [ingredient('Archive Me')]);
    let nativePromptShown = false;
    page.on('dialog', async (dialog) => {
      nativePromptShown = true;
      await dialog.dismiss();
    });
    await page.goto('/ingredient-maintenance');

    const archiveButton = page.getByRole('button', { name: 'Archive Archive Me' });
    const confirmation = page.getByRole('dialog', { name: 'Archive ingredient?' });
    await archiveButton.click();
    await expect(confirmation).toContainText('Archive Me');
    await expect(confirmation.getByRole('button', { name: 'Cancel' })).toBeFocused();
    await confirmation.getByRole('button', { name: 'Cancel' }).click();
    await expect(confirmation).toBeHidden();
    expect(requests.some((request) => request.endsWith('/archive'))).toBe(false);

    await archiveButton.click();
    await page.keyboard.press('Escape');
    await expect(confirmation).toBeHidden();
    expect(requests.some((request) => request.endsWith('/archive'))).toBe(false);

    await archiveButton.click();
    await confirmation.getByRole('button', { name: 'Archive', exact: true }).click();
    await expect(page.getByText('No ingredients yet.')).toBeVisible();
    expect(requests.some((request) => request.endsWith('/archive'))).toBe(true);
    expect(nativePromptShown).toBe(false);
  });
});
