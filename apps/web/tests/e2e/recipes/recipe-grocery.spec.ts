import { expect, test, type Page } from '@playwright/test';
import { mockAuthApi } from '../support/mockAuthApi.ts';
import { mockRecipeApi, recipeDetail, recipeId } from '../support/mockRecipeApi.ts';

test.beforeEach(async ({ page }) => {
  await mockAuthApi(page, { hasSession: true });
  await mockRecipeApi(page);
});

for (const width of [375, 1280]) {
  test(`reviews and adds Recipe requirements from the read-only Recipe view at ${width}px`, async ({ page }) => {
    const commands: Array<{ method: string; body: Record<string, unknown> }> = [];
    await mockRecipeGroceryApi(page, commands, false);
    await page.setViewportSize({ width, height: 900 });
    await page.goto(`/recipes/${recipeId}`);
    const addButton = page.getByRole('button', { name: 'Add to grocery list', exact: true, includeHidden: true });
    const tags = page.getByRole('list', { name: 'Recipe tags', includeHidden: true });
    await expect(addButton).toBeVisible();
    await expect(tags).toBeVisible();
    const buttonBounds = await addButton.boundingBox();
    const tagBounds = await tags.boundingBox();
    expect(tagBounds!.y - (buttonBounds!.y + buttonBounds!.height)).toBeGreaterThanOrEqual(8);

    await addButton.click();
    await expect(page.getByRole('heading', { name: 'Review Recipe grocery items' })).toBeVisible();
    const backgroundButtonBounds = await addButton.boundingBox();
    const backgroundTagBounds = await tags.boundingBox();
    expect(backgroundTagBounds!.y - (backgroundButtonBounds!.y + backgroundButtonBounds!.height))
      .toBeCloseTo(tagBounds!.y - (buttonBounds!.y + buttonBounds!.height));
    const included = page.getByRole('region', { name: 'Included Recipe Ingredients' });
    const ingredient = included.getByText('Firm Tofu', { exact: true });
    const toggle = included.locator('summary');
    await expect(toggle).toHaveText('Included (1)');
    await expect(ingredient).toBeHidden();
    await toggle.click();
    await expect(ingredient).toBeVisible();
    await expect(included).not.toContainText('pressed');
    await toggle.press('Enter');
    await expect(ingredient).toBeHidden();
    await toggle.press('Enter');
    await expect(ingredient).toBeVisible();
    await expect(page.getByText('archived Ingredient reference')).toHaveCount(0);
    expect(await page.evaluate(() =>
      document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);

    await page.getByRole('dialog')
      .getByRole('button', { name: 'Add to Grocery List', exact: true })
      .click();
    await expect(page.getByRole('link', { name: 'View Grocery List' })).toBeVisible();
    const groceryCommand = commands.at(-1);
    expect(groceryCommand?.method).toBe('POST');
    expect(groceryCommand?.body).toMatchObject({
      expectedListVersion: 0,
      expectedRecipeVersion: 7,
      confirmRepeat: false,
    });
  });
}

test('requires explicit repeat confirmation and removes only outstanding Recipe requirements', async ({ page }) => {
  const commands: Array<{ method: string; body: Record<string, unknown> }> = [];
  await mockRecipeGroceryApi(page, commands, true);
  await page.goto(`/recipes/${recipeId}`);

  await page.getByRole('button', { name: 'Add to grocery list' }).click();
  await expect(page.getByRole('status')).toContainText('already has outstanding grocery requirements');
  await page.getByRole('button', { name: /Confirm and add another Recipe/ }).click();
  await expect(page.getByRole('link', { name: 'View Grocery List' })).toBeVisible();
  expect(commands.at(-1)?.body).toMatchObject({ confirmRepeat: true });

  await page.getByRole('button', { name: 'Add to grocery list' }).click();
  await expect(page.getByRole('region', { name: 'Included Recipe Ingredients' })
    .getByText('Firm Tofu', { exact: true })).toBeHidden();
  await page.getByRole('button', { name: 'Remove this Recipe’s outstanding requirements' }).click();
  await page.getByRole('button', { name: 'Confirm removal' }).click();
  await expect(page.getByRole('link', { name: 'View Grocery List' })).toBeVisible();
  expect(commands.at(-1)?.method).toBe('DELETE');
  expect(commands.at(-1)?.body).toMatchObject({ expectedListVersion: 9 });
});

async function mockRecipeGroceryApi(
  page: Page,
  commands: Array<{ method: string; body: Record<string, unknown> }>,
  initiallyOutstanding: boolean,
) {
  let hasOutstandingAddition = initiallyOutstanding;
  let listVersion = initiallyOutstanding ? 8 : 0;
  await page.route('**/api/grocery-list/recipes/**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    if (request.method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        json: {
          recipeId,
          recipeName: recipeDetail.name,
          recipeVersion: 7,
          groceryListId: hasOutstandingAddition ? '019a0000-0000-7000-8000-000000000010' : null,
          listVersion,
          hasOutstandingAddition,
          included: [{
            recipeStepId: '019a0000-0000-7000-8000-000000000011',
            stepPosition: 0,
            recipeStepItemId: '019a0000-0000-7000-8000-000000000012',
            itemPosition: 0,
            ingredientId: '019a0000-0000-7000-8000-000000000013',
            ingredientName: 'firm tofu',
            ingredientIsArchived: false,
            originalNameText: 'firm tofu',
            originalAmountText: '1/2',
            originalUnitText: 'block',
            originalNotesText: 'pressed',
            interpretationStatus: 'known',
            interpretedAmount: 0.5,
            dimension: 'count',
            canonicalUnit: 'block',
            countLabel: 'block',
            unresolvedReason: null,
          }],
          excluded: [],
        },
      });
      return;
    }

    const body = request.postDataJSON() as Record<string, unknown>;
    commands.push({ method: request.method(), body });
    if (url.pathname.endsWith('/contributions')) {
      hasOutstandingAddition = false;
    } else {
      hasOutstandingAddition = true;
    }
    listVersion += 1;
    await route.fulfill({
      status: request.method() === 'POST' ? 201 : 200,
      contentType: 'application/json',
      json: {
        commandId: body.commandId,
        resultListVersion: listVersion,
        resultIds: [],
      },
    });
  });
}
