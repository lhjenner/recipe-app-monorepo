import { expect, test, type Page } from '@playwright/test';
import { mockAuthApi } from '../support/mockAuthApi.ts';
import { mockRecipeApi, recipeDetail, recipeId } from '../support/mockRecipeApi.ts';

test.beforeEach(async ({ page }) => {
  await mockAuthApi(page, { hasSession: true });
  await mockRecipeApi(page);
});

for (const width of [320, 375, 1280]) {
  test(`reviews and adds Recipe requirements from the read-only Recipe view at ${width}px`, async ({ page }) => {
    const commands: Array<{ method: string; body: Record<string, unknown> }> = [];
    await mockRecipeGroceryApi(page, commands, false);
    await page.setViewportSize({ width, height: 900 });
    await page.goto(`/recipes/${recipeId}`);
    const addButton = page.locator('.recipe-grocery-actions')
      .getByRole('button', { name: 'Add to Grocery List', exact: true, includeHidden: true });
    const tags = page.getByRole('list', { name: 'Recipe tags', includeHidden: true });
    await expect(addButton).toBeVisible();
    await expect(tags).toBeVisible();
    const buttonBounds = await addButton.boundingBox();
    const tagBounds = await tags.boundingBox();
    expect(tagBounds!.y - (buttonBounds!.y + buttonBounds!.height)).toBeGreaterThanOrEqual(8);

    await addButton.click();
    await expect(page.getByRole('heading', { name: 'Review Recipe grocery items' })).toBeVisible();
    const reviewDialog = page.getByRole('dialog');
    const reviewConfirm = reviewDialog.getByRole('button', { name: 'Confirm', exact: true });
    const reviewCancel = reviewDialog.getByRole('button', { name: 'Cancel', exact: true });
    await expect(reviewCancel).toHaveClass(/btn-danger-secondary/);
    await expect(reviewCancel).toBeFocused();
    await expect(reviewDialog.getByRole('button', { name: 'Close', exact: true })).toHaveCount(0);
    const reviewConfirmBounds = await reviewConfirm.boundingBox();
    const reviewCancelBounds = await reviewCancel.boundingBox();
    expect(reviewCancelBounds!.x - (reviewConfirmBounds!.x + reviewConfirmBounds!.width)).toBeGreaterThanOrEqual(8);
    expect(Math.abs(reviewCancelBounds!.y - reviewConfirmBounds!.y)).toBeLessThan(2);
    await reviewCancel.click();
    expect(commands).toHaveLength(0);
    await addButton.click();
    await expect(reviewCancel).toBeVisible();
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
      .getByRole('button', { name: 'Confirm', exact: true })
      .click();
    await expect(page.getByRole('link', { name: 'View Grocery List' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Remove from Grocery List', exact: true })).toBeVisible();
    await expect(page.getByText('On Grocery List', { exact: true })).toBeVisible();
    const groceryCommand = commands.at(-1);
    expect(groceryCommand?.method).toBe('POST');
    expect(groceryCommand?.body).toMatchObject({
      expectedListVersion: 0,
      expectedRecipeVersion: 7,
      confirmRepeat: false,
    });

    const removeButton = page.getByRole('button', { name: 'Remove from Grocery List', exact: true });
    if (width <= 700) {
      const addBounds = await addButton.boundingBox();
      const removeBounds = await removeButton.boundingBox();
      expect(removeBounds!.x).toBeCloseTo(addBounds!.x);
      expect(removeBounds!.width).toBeCloseTo(addBounds!.width);
      expect(removeBounds!.y - (addBounds!.y + addBounds!.height)).toBeGreaterThanOrEqual(8);
    }

    await addButton.click();
    const dialog = page.getByRole('dialog');
    const confirm = dialog.getByRole('button', { name: 'Confirm', exact: true });
    const cancel = dialog.getByRole('button', { name: 'Cancel', exact: true });
    await expect(cancel).toBeVisible();
    await expect(cancel).toHaveClass(/btn-danger-secondary/);
    await expect(cancel).toBeFocused();
    const confirmBounds = await confirm.boundingBox();
    const cancelBounds = await cancel.boundingBox();
    expect(cancelBounds!.x - (confirmBounds!.x + confirmBounds!.width)).toBeGreaterThanOrEqual(8);
    expect(Math.abs(cancelBounds!.y - confirmBounds!.y)).toBeLessThan(2);
    await cancel.click();
    expect(commands).toHaveLength(1);

    await removeButton.click();
    await expect(confirm).toBeVisible();
    await expect(cancel).toHaveClass(/btn-danger-secondary/);
    await expect(cancel).toBeFocused();
    const removeConfirmBounds = await confirm.boundingBox();
    const removeCancelBounds = await cancel.boundingBox();
    expect(removeCancelBounds!.x - (removeConfirmBounds!.x + removeConfirmBounds!.width)).toBeGreaterThanOrEqual(8);
    expect(Math.abs(removeCancelBounds!.y - removeConfirmBounds!.y)).toBeLessThan(2);
    await cancel.click();
    expect(commands).toHaveLength(1);
  });
}

test('requires explicit repeat confirmation and removes only outstanding Recipe requirements', async ({ page }) => {
  const commands: Array<{ method: string; body: Record<string, unknown> }> = [];
  await mockRecipeGroceryApi(page, commands, true, true);
  await page.goto(`/recipes/${recipeId}`);

  await page.getByRole('button', { name: 'Add to Grocery List', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('already on your Grocery List');
  await expect(page.getByRole('region', { name: 'Included Recipe Ingredients' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Cancel', exact: true }).click();
  expect(commands).toHaveLength(0);
  await page.getByRole('button', { name: 'Add to Grocery List', exact: true }).click();
  await page.getByRole('button', { name: 'Confirm', exact: true }).click();
  await expect(page.getByRole('link', { name: 'View Grocery List' })).toBeVisible();
  expect(commands.at(-1)?.body).toMatchObject({ confirmRepeat: true });

  await page.getByRole('button', { name: 'Remove from Grocery List', exact: true }).click();
  await page.getByRole('button', { name: 'Cancel', exact: true }).click();
  expect(commands).toHaveLength(1);
  await page.getByRole('button', { name: 'Remove from Grocery List', exact: true }).click();
  await page.getByRole('button', { name: 'Confirm', exact: true }).click();
  await expect(page.getByRole('link', { name: 'View Grocery List' })).toBeVisible();
  expect(commands.at(-1)?.method).toBe('DELETE');
  expect(commands.at(-1)?.body).toMatchObject({ expectedListVersion: 9 });
  await expect(page.getByRole('button', { name: 'Remove from Grocery List', exact: true })).toHaveCount(0);
  await expect(page.getByText('On Grocery List', { exact: true })).toBeVisible();
});

test('highlights demand from other recipes and manual additions without offering recipe removal', async ({ page }) => {
  const commands: Array<{ method: string; body: Record<string, unknown> }> = [];
  await mockRecipeGroceryApi(page, commands, false, true);
  await page.goto(`/recipes/${recipeId}`);
  await expect(page.getByText('On Grocery List', { exact: true })).toBeVisible();
  await expect(page.locator('.recipe-step-ingredient').filter({ hasText: 'Firm Tofu' }))
    .toHaveClass(/recipe-step-on-grocery-list/);
  await expect(page.getByRole('button', { name: 'Remove from Grocery List', exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Add to Grocery List', exact: true }).click();
  await expect(page.getByRole('region', { name: 'Included Recipe Ingredients' })).toBeVisible();
  await page.getByRole('dialog').getByRole('button', { name: 'Confirm', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Remove from Grocery List', exact: true })).toBeVisible();
});

async function mockRecipeGroceryApi(
  page: Page,
  commands: Array<{ method: string; body: Record<string, unknown> }>,
  initiallyOutstanding: boolean,
  hasOtherDemand = false,
) {
  let hasOutstandingAddition = initiallyOutstanding;
  let listVersion = initiallyOutstanding ? 8 : 0;
  await page.route('**/api/grocery-list', async (route) => {
    const contribution = (id: string, sourceRecipeId: string | null) => ({
      id, origin: sourceRecipeId ? 'recipe' : 'manual', state: 'outstanding',
      originalAmountText: '1', originalUnitText: 'item', originalNotesText: null,
      interpretedAmount: 1, canonicalUnit: 'item', unresolvedReason: null,
      recipeId: sourceRecipeId, recipeStepId: null, recipeStepItemId: null,
      recipeNameSnapshot: sourceRecipeId ? 'Recipe' : null,
    });
    const contributions = [
      ...(hasOutstandingAddition ? [contribution('019a0000-0000-7000-8000-000000000021', recipeId)] : []),
      ...(hasOtherDemand ? [
        contribution('019a0000-0000-7000-8000-000000000022', '019a0000-0000-7000-8000-000000000002'),
        contribution('019a0000-0000-7000-8000-000000000023', null),
      ] : []),
    ];
    await route.fulfill({ json: {
      id: '019a0000-0000-7000-8000-000000000010', version: listVersion, completedItemCount: 0,
      items: contributions.length ? [{
        id: '019a0000-0000-7000-8000-000000000020',
        ingredientId: '019a0000-0000-7000-8000-000000000013',
        name: 'Firm Tofu', state: 'outstanding', note: null,
        dimension: 'count', countLabel: 'item', requirementAmount: contributions.length,
        requirementUnit: 'item', outstandingPurchaseQuantity: contributions.length,
        purchaseUnit: 'item', ingredientVersion: null, adHocStoreId: null,
        adHocDepartmentId: null, adHocDepartmentName: null, storeMappings: [], contributions,
      }] : [],
    } });
  });
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
