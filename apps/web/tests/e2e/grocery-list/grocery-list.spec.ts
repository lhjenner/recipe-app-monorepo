import { expect, test } from '@playwright/test';
import { mockAuthApi } from '../support/mockAuthApi.ts';
import { groceryList, mockGroceryApi, selectedStoreId } from '../support/mockGroceryApi.ts';
import { ingredient, mockIngredientApi } from '../support/mockIngredientListApi.ts';
import type { GroceryManualEntryCommand } from '../../../src/features/grocery-list/services/groceryListApi.ts';

test('requires confirmation before removing an item and allows cancellation with Escape', async ({ page }) => {
  await mockAuthApi(page, { hasSession: true });
  await mockGroceryApi(page);
  let removals = 0;
  await page.route('**/api/grocery-list/items/*', async (route) => {
    expect(route.request().method()).toBe('DELETE');
    removals++;
    await route.fulfill({ status: 204 });
  });
  await page.goto('/grocery-list');
  const remove = page.getByRole('button', { name: 'Remove green apples from Grocery List' });
  await remove.click();
  const dialog = page.getByRole('dialog', { name: 'Remove grocery item?' });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('button')).toHaveText(['Remove item', 'Cancel']);
  await expect(dialog.getByRole('button', { name: 'Cancel', exact: true })).toBeFocused();
  expect(removals).toBe(0);
  await page.keyboard.press('Escape');
  await expect(dialog).toHaveCount(0);
  expect(removals).toBe(0);
  await expect(remove).toBeFocused();
  await remove.click();
  await dialog.getByRole('button', { name: 'Remove item', exact: true }).click();
  await expect.poll(() => removals).toBe(1);
});

test('grocery dialogs contain focus, close with Escape and restore the trigger', async ({ page }) => {
  await mockAuthApi(page, { hasSession: true });
  await mockGroceryApi(page);
  await mockIngredientApi(page, []);
  for (const lookup of ['categories', 'departments']) {
    await page.route(`**/api/${lookup}`, (route) => route.fulfill({ json: [] }));
  }
  await page.goto('/grocery-list');
  for (const trigger of [
    page.getByRole('button', { name: 'Add items', exact: true }),
    page.getByRole('row', { name: /Green Apples/ }).getByRole('button', { name: 'Edit', exact: true }),
  ]) {
    await trigger.click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.locator('input').first()).toBeFocused();
    for (const theme of ['light', 'dark']) {
      await page.evaluate((value) => { document.documentElement.dataset.theme = value; }, theme);
      await expect(dialog).toHaveCSS('color', await page.locator('body').evaluate((element) =>
        getComputedStyle(element).color));
    }
    await page.getByLabel('Group for store').evaluate((element: HTMLSelectElement) => element.focus());
    await expect(dialog.locator('input').first()).toBeFocused();
    for (let index = 0; index < 15; index++) {
      await page.keyboard.press('Tab');
      const focus = await dialog.evaluate((element) => ({
        inside: element.contains(document.activeElement),
        activeTag: document.activeElement?.tagName,
        modal: element.matches(':modal'),
        documentFocused: document.hasFocus(),
      }));
      expect(focus.modal).toBe(true);
      expect(focus.inside || (!focus.documentFocused && focus.activeTag === 'BODY'),
        JSON.stringify(focus)).toBe(true);
    }
    await page.keyboard.press('Shift+Tab');
    await expect.poll(() => dialog.evaluate((element) =>
      element.contains(document.activeElement))).toBe(true);
    await page.keyboard.press('Escape');
    await expect(dialog).toHaveCount(0);
    await expect(trigger).toBeFocused();
  }
});

test('cancels a manual entry from the form actions without submitting', async ({ page }) => {
  await mockAuthApi(page, { hasSession: true });
  await mockGroceryApi(page);
  await mockIngredientApi(page, []);
  for (const lookup of ['categories', 'departments']) {
    await page.route(`**/api/${lookup}`, (route) => route.fulfill({ json: [] }));
  }
  let submissions = 0;
  await page.route('**/api/grocery-list/items', async (route) => {
    submissions++;
    await route.abort();
  });
  await page.goto('/grocery-list');
  const trigger = page.getByRole('button', { name: 'Add items', exact: true });
  for (const width of [1280, 390]) {
    await page.setViewportSize({ width, height: 844 });
    await trigger.click();
    const dialog = page.getByRole('dialog', { name: 'Add items to Grocery List' });
    await dialog.getByRole('combobox', { name: 'Name' }).fill('Oats');
    const add = dialog.getByRole('button', { name: 'Add to Grocery List' });
    const cancel = dialog.getByRole('button', { name: 'Cancel', exact: true });
    await expect(cancel).toHaveClass(/btn-danger-secondary/);
    await expect(add.locator('..').getByRole('button')).toHaveText(['Add to Grocery List', 'Add row', 'Cancel']);
    await expect(dialog.getByRole('button', { name: 'Close' })).toHaveCount(0);
    await cancel.click();
    await expect(dialog).toHaveCount(0);
    await expect(trigger).toBeFocused();
  }
  expect(submissions).toBe(0);
});

test('manual entry suggests Ingredients while typing and links only a chosen suggestion', async ({ page }) => {
  await mockAuthApi(page, { hasSession: true });
  await mockGroceryApi(page);
  const searches = await mockIngredientApi(page, [
    ingredient('crumpets'), ingredient('crushed chilli'),
  ]);
  for (const lookup of ['categories', 'departments']) {
    await page.route(`**/api/${lookup}`, (route) => route.fulfill({ json: [] }));
  }
  await page.goto('/grocery-list');
  await page.getByRole('button', { name: 'Add items', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Add items to Grocery List' });
  const name = dialog.getByRole('combobox', { name: 'Name', exact: true });
  await name.fill('cru');
  await expect(dialog.getByRole('option')).toHaveText(['Crumpets', 'Crushed Chilli']);
  await expect(dialog.getByRole('button', { name: /^Create / })).toHaveCount(0);

  await name.fill('crum');
  await expect(dialog.getByRole('option')).toHaveText(['Crumpets']);
  await expect.poll(() => searches.some((search) =>
    new URLSearchParams(search).get('search') === 'crum')).toBe(true);
  await dialog.getByRole('option', { name: 'Crumpets', exact: true }).click();
  await expect(name).toHaveValue('Crumpets');
  await expect(dialog.getByRole('listbox')).toHaveCount(0);
  const badge = dialog.getByRole('img', { name: 'Linked to existing Ingredient' });
  await expect(badge).toBeVisible();
  await expect(badge).toHaveAttribute('title', 'Linked to existing Ingredient');
  await expect(badge).toHaveCSS('border-radius', '50%');
  await expect(dialog.getByRole('button', { name: 'Use as an ad-hoc item' })).toHaveCount(0);

  for (const width of [320, 1280]) {
    await page.setViewportSize({ width, height: 844 });
    const nameBox = await name.boundingBox();
    const badgeBox = await badge.boundingBox();
    const arrowBox = await dialog.getByRole('button', { name: 'Show Name options' }).boundingBox();
    expect(nameBox).not.toBeNull();
    expect(badgeBox).not.toBeNull();
    expect(arrowBox).not.toBeNull();
    expect(badgeBox!.x).toBeGreaterThan(nameBox!.x);
    expect(badgeBox!.y).toBeGreaterThan(nameBox!.y);
    expect(badgeBox!.y + badgeBox!.height).toBeLessThan(nameBox!.y + nameBox!.height);
    expect(badgeBox!.x + badgeBox!.width).toBeLessThanOrEqual(arrowBox!.x);
    expect(badgeBox!.width).toBeLessThanOrEqual(24);
    expect(badgeBox!.height).toBe(badgeBox!.width);
  }

  await name.fill('crumpets');
  await dialog.getByRole('spinbutton', { name: 'Quantity' }).focus();
  await expect(name).toHaveValue('Crumpets');
  await expect(badge).toHaveCount(0);
  await expect(dialog.getByRole('checkbox', { name: 'Save to master Ingredient list' })).toBeVisible();
  await name.fill('crum');
  await dialog.getByRole('option', { name: 'Crumpets', exact: true }).click();
  await expect(badge).toBeVisible();
});

test('manual entry aligns name, quantity, note and remove controls responsively', async ({ page }) => {
  await mockAuthApi(page, { hasSession: true });
  await mockGroceryApi(page);
  await mockIngredientApi(page, []);
  for (const lookup of ['categories', 'departments']) {
    await page.route(`**/api/${lookup}`, (route) => route.fulfill({ json: [] }));
  }
  await page.goto('/grocery-list');
  await page.getByRole('button', { name: 'Add items', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Add items to Grocery List' });
  const firstRow = dialog.getByRole('group', { name: 'Item 1' });
  await expect(firstRow.getByRole('button', { name: 'Remove row' })).toHaveCount(0);
  const firstName = (await firstRow.getByRole('combobox', { name: 'Name' }).boundingBox())!;
  const firstStepper = (await firstRow.getByRole('spinbutton', { name: 'Quantity' }).locator('..').boundingBox())!;
  expect(Math.abs(firstName.y - firstStepper.y)).toBeLessThanOrEqual(1);
  await dialog.getByRole('button', { name: 'Add row' }).click();
  for (const theme of ['light', 'dark']) {
    await page.evaluate((value) => { document.documentElement.dataset.theme = value; }, theme);
    const primaryColor = await dialog.getByRole('button', { name: 'Add to Grocery List' })
      .evaluate((element) => getComputedStyle(element).backgroundColor);
    for (const item of ['Item 1', 'Item 2']) {
      await expect(dialog.getByRole('group', { name: item })).toHaveCSS('border-top-color', primaryColor);
    }
  }
  await page.evaluate(() => { document.documentElement.dataset.theme = 'light'; });
  const row = dialog.getByRole('group', { name: 'Item 1' });
  const name = row.getByRole('combobox', { name: 'Name' });
  const quantity = row.getByRole('spinbutton', { name: 'Quantity' });
  const stepper = quantity.locator('..');
  const note = row.getByLabel('Note (optional)');
  const remove = row.getByRole('button', { name: 'Remove row' });

  for (const width of [701, 1280]) {
    await page.setViewportSize({ width, height: 900 });
    const nameBox = (await name.boundingBox())!;
    const stepperBox = (await stepper.boundingBox())!;
    const noteBox = (await note.boundingBox())!;
    const removeBox = (await remove.boundingBox())!;
    expect(Math.abs(nameBox.y - stepperBox.y)).toBeLessThanOrEqual(1);
    expect(Math.abs(noteBox.y - removeBox.y)).toBeLessThanOrEqual(1);
    expect(noteBox.y).toBeGreaterThan(nameBox.y);
    expect(Math.abs(nameBox.x - noteBox.x)).toBeLessThanOrEqual(1);
    expect(Math.abs(nameBox.width - noteBox.width)).toBeLessThanOrEqual(1);
    expect(Math.abs(stepperBox.x - removeBox.x)).toBeLessThanOrEqual(1);
    expect(Math.abs(stepperBox.width - removeBox.width)).toBeLessThanOrEqual(1);
  }

  const saveIngredient = row.getByRole('checkbox', { name: 'Save to master Ingredient list' });
  await saveIngredient.check();
  const category = row.getByLabel('Category (optional)');
  await expect(category).toBeVisible();
  expect((await category.boundingBox())!.y).toBeGreaterThan((await note.boundingBox())!.y);
  await saveIngredient.uncheck();

  for (const width of [320, 390, 700]) {
    await page.setViewportSize({ width, height: 844 });
    const mobileName = (await name.boundingBox())!;
    const mobileStepper = (await stepper.boundingBox())!;
    const mobileNote = (await note.boundingBox())!;
    const mobileRemove = (await remove.boundingBox())!;
    expect(mobileNote.y).toBeGreaterThan(mobileName.y);
    expect(mobileStepper.y).toBeGreaterThan(mobileNote.y);
    expect(Math.abs(mobileName.x - mobileNote.x)).toBeLessThanOrEqual(1);
    expect(Math.abs(mobileName.width - mobileNote.width)).toBeLessThanOrEqual(1);
    expect(Math.abs(mobileStepper.y - mobileRemove.y), JSON.stringify({ width, mobileStepper, mobileRemove }))
      .toBeLessThanOrEqual(1);
    expect(mobileRemove.x).toBeGreaterThan(mobileStepper.x + mobileStepper.width);
    await expect.poll(() => remove.evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(true);
    await quantity.fill('9999');
    const quantityFits = await quantity.evaluate((element: HTMLInputElement) => {
      const style = getComputedStyle(element);
      const canvas = document.createElement('canvas');
      const context = canvas.getContext('2d')!;
      context.font = `${style.fontWeight} ${style.fontSize} ${style.fontFamily}`;
      const availableWidth = element.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight);
      return context.measureText(element.value).width <= availableWidth;
    });
    expect(quantityFits).toBe(true);
  }
  await dialog.getByRole('group', { name: 'Item 2' }).getByRole('button', { name: 'Remove row' }).click();
  await expect(dialog.getByRole('group', { name: 'Item 2' })).toHaveCount(0);
  await expect(row.getByRole('button', { name: 'Remove row' })).toHaveCount(0);
  await expect(quantity).toBeVisible();
});

test('duplicate review opens above the draft, supports Escape, and cancels both dialogs', async ({ page }) => {
  await mockAuthApi(page, { hasSession: true });
  await mockGroceryApi(page);
  await mockIngredientApi(page, []);
  for (const lookup of ['categories', 'departments']) {
    await page.route(`**/api/${lookup}`, (route) => route.fulfill({ json: [] }));
  }
  let submissions = 0;
  await page.route('**/api/grocery-list/items', async (route) => {
    submissions++;
    await route.fulfill({
      status: 409,
      json: {
        detail: 'One or more entries match an outstanding item.',
        duplicateReviews: [{
          entryIndex: 0, targetItemId: groceryList.items[0].id, targetEntryIndex: null,
          existingQuantity: 1, increment: 1, resultingQuantity: 2,
          itemName: 'green apples', purchaseUnit: 'item',
        }],
      },
    });
  });
  await page.goto('/grocery-list');
  const trigger = page.getByRole('button', { name: 'Add items', exact: true });
  await trigger.click();
  const dialog = page.getByRole('dialog', { name: 'Add items to Grocery List' });
  await dialog.getByRole('combobox', { name: 'Name' }).fill('green apples');
  await dialog.getByRole('button', { name: 'Add to Grocery List' }).click();
  const review = page.getByRole('dialog', { name: 'Review matching grocery items' });
  await expect(review).toBeVisible();
  await expect(review.getByText('Green Apples', { exact: true })).toBeVisible();
  await expect(review.getByText('Green Apples', { exact: true })).toHaveCSS('font-weight', '600');
  await expect(review.getByText('New Total: 2 item(s)', { exact: true })).toBeVisible();
  await expect(review.getByRole('button', { name: 'Back to editing' })).toBeFocused();
  for (const width of [320, 390, 700]) {
    await page.setViewportSize({ width, height: 844 });
    const [nameBottom, totalTop] = await review.getByRole('listitem').evaluate((item) => {
      const [name, total] = item.firstElementChild!.children;
      return [name.getBoundingClientRect().bottom, total.getBoundingClientRect().top];
    });
    expect(totalTop).toBeGreaterThanOrEqual(nameBottom);
    const buttons = await Promise.all(['Confirm reviewed increments', 'Back to editing', 'Cancel'].map(
      (name) => review.getByRole('button', { name, exact: true }).boundingBox(),
    ));
    for (const [index, button] of buttons.entries()) {
      expect(button).not.toBeNull();
      expect(button!.width).toBeCloseTo(buttons[0]!.width, 1);
      if (index > 0) expect(button!.y).toBeGreaterThanOrEqual(buttons[index - 1]!.y + buttons[index - 1]!.height);
    }
  }
  for (const width of [701, 1280]) {
    await page.setViewportSize({ width, height: 844 });
    const [confirm, back, cancel] = await Promise.all(
      ['Confirm reviewed increments', 'Back to editing', 'Cancel'].map(
        (name) => review.getByRole('button', { name, exact: true }).boundingBox(),
      ),
    );
    expect(confirm).not.toBeNull();
    expect(back).not.toBeNull();
    expect(cancel).not.toBeNull();
    expect(confirm!.y + confirm!.height).toBeLessThanOrEqual(back!.y);
    expect(back!.y).toBeCloseTo(cancel!.y, 1);
    expect(back!.width).toBeCloseTo(cancel!.width, 1);
    expect(back!.x).toBeCloseTo(confirm!.x, 1);
    expect(cancel!.x + cancel!.width).toBeCloseTo(confirm!.x + confirm!.width, 1);
  }
  await page.keyboard.press('Escape');
  await expect(review).toHaveCount(0);
  await expect(dialog.getByRole('combobox', { name: 'Name' })).toHaveValue('Green Apples');
  await expect(dialog.getByRole('button', { name: 'Add to Grocery List' })).toBeFocused();
  await dialog.getByRole('button', { name: 'Add to Grocery List' }).click();
  await expect(review).toBeVisible();
  await review.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(dialog).toHaveCount(0);
  await expect(trigger).toBeFocused();
  expect(submissions).toBe(2);
  await trigger.click();
  await expect(dialog.getByRole('combobox', { name: 'Name' })).toHaveValue('');
});

test('confirms a selected duplicate while adding non-duplicate items', async ({ page }) => {
  await mockAuthApi(page, { hasSession: true });
  await mockGroceryApi(page);
  await mockIngredientApi(page, []);
  for (const lookup of ['categories', 'departments']) {
    await page.route(`**/api/${lookup}`, (route) => route.fulfill({ json: [] }));
  }
  const commands: GroceryManualEntryCommand[] = [];
  await page.route('**/api/grocery-list/items', async (route) => {
    const command = route.request().postDataJSON() as GroceryManualEntryCommand;
    commands.push(command);
    if (commands.length === 1) {
      await route.fulfill({
        status: 409,
        json: {
          detail: 'Review duplicate grocery items.',
          duplicateReviews: [0, 2].map((entryIndex) => ({
            entryIndex, targetItemId: groceryList.items[entryIndex].id, targetEntryIndex: null,
            existingQuantity: 1, increment: 1, resultingQuantity: 2,
            itemName: command.items[entryIndex].name, purchaseUnit: 'item',
          })),
        },
      });
    } else {
      await route.fulfill({
        status: 201,
        json: { commandId: command.commandId, resultListVersion: 2, resultIds: [groceryList.items[0].id] },
      });
    }
  });
  await page.goto('/grocery-list');
  await page.getByRole('button', { name: 'Add items', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Add items to Grocery List' });
  await dialog.getByRole('group', { name: 'Item 1' }).getByRole('combobox', { name: 'Name' }).fill('Oats');
  for (const [index, name] of ['Rice', 'Beans'].entries()) {
    await dialog.getByRole('button', { name: 'Add row' }).click();
    await dialog.getByRole('group', { name: `Item ${index + 2}` }).getByRole('combobox', { name: 'Name' }).fill(name);
  }
  await dialog.getByRole('button', { name: 'Add to Grocery List' }).click();
  const review = page.getByRole('dialog', { name: 'Review matching grocery items' });
  await expect(review.getByText('1 other item will be added.')).toBeVisible();
  for (const width of [320, 390, 700]) {
    await page.setViewportSize({ width, height: 844 });
    expect(await review.evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(true);
    const buttons = await Promise.all([
      'Confirm reviewed increments', 'Back to editing', 'Skip duplicates and add other items',
    ].map((name) => review.getByRole('button', { name, exact: true }).boundingBox()));
    for (const [index, button] of buttons.entries()) {
      expect(button).not.toBeNull();
      expect(button!.width).toBeCloseTo(buttons[0]!.width, 1);
      if (index > 0) expect(button!.y).toBeGreaterThanOrEqual(buttons[index - 1]!.y + buttons[index - 1]!.height);
    }
  }
  await page.setViewportSize({ width: 1280, height: 844 });
  const [confirm, back, skip] = await Promise.all(
    ['Confirm reviewed increments', 'Back to editing', 'Skip duplicates and add other items'].map(
      (name) => review.getByRole('button', { name, exact: true }).boundingBox(),
    ),
  );
  expect(confirm).not.toBeNull();
  expect(back).not.toBeNull();
  expect(skip).not.toBeNull();
  expect(confirm!.y + confirm!.height).toBeLessThanOrEqual(back!.y);
  expect(back!.y).toBeCloseTo(skip!.y, 1);
  expect(back!.width).toBeCloseTo(skip!.width, 1);
  expect(back!.x).toBeCloseTo(confirm!.x, 1);
  expect(skip!.x + skip!.width).toBeCloseTo(confirm!.x + confirm!.width, 1);
  await review.getByRole('button', { name: 'Remove Oats from review' }).click();
  await expect(review.getByRole('status')).toHaveText('1 removed item will not be added.');
  await expect(review.getByRole('button', { name: 'Remove Beans from review' })).toHaveCount(0);
  await review.getByRole('button', { name: 'Confirm reviewed increments' }).click();
  await expect(dialog).toHaveCount(0);
  expect(commands).toHaveLength(2);
  expect(commands[1].items.map((item) => item.name)).toEqual(['Rice', 'Beans']);
  expect(commands[1].reviewedDuplicates).toHaveLength(1);
  expect(commands[1].reviewedDuplicates[0].entryIndex).toBe(1);
  expect(commands[1].commandId).not.toBe(commands[0].commandId);
});

test('retries the exact selected batch after an uncertain response', async ({ page }) => {
  await mockAuthApi(page, { hasSession: true });
  await mockGroceryApi(page);
  await mockIngredientApi(page, []);
  for (const lookup of ['categories', 'departments']) {
    await page.route(`**/api/${lookup}`, (route) => route.fulfill({ json: [] }));
  }
  const commands: GroceryManualEntryCommand[] = [];
  await page.route('**/api/grocery-list/items', async (route) => {
    const command = route.request().postDataJSON() as GroceryManualEntryCommand;
    commands.push(command);
    if (commands.length === 1) {
      await route.fulfill({
        status: 409,
        json: {
          detail: 'Review duplicate grocery items.',
          duplicateReviews: [{
            entryIndex: 0, targetItemId: groceryList.items[0].id, targetEntryIndex: null,
            existingQuantity: 1, increment: 1, resultingQuantity: 2,
            itemName: 'Oats', purchaseUnit: 'item',
          }],
        },
      });
    } else if (commands.length === 2) {
      await route.fulfill({ status: 500, json: { detail: 'The result is unknown.' } });
    } else {
      await route.fulfill({
        status: 201,
        json: { commandId: command.commandId, resultListVersion: 2, resultIds: [groceryList.items[0].id] },
      });
    }
  });
  await page.goto('/grocery-list');
  await page.getByRole('button', { name: 'Add items', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Add items to Grocery List' });
  await dialog.getByRole('group', { name: 'Item 1' }).getByRole('combobox', { name: 'Name' }).fill('Oats');
  await dialog.getByRole('button', { name: 'Add row' }).click();
  await dialog.getByRole('group', { name: 'Item 2' }).getByRole('combobox', { name: 'Name' }).fill('Rice');
  await dialog.getByRole('button', { name: 'Add to Grocery List' }).click();
  const review = page.getByRole('dialog', { name: 'Review matching grocery items' });
  await review.getByRole('button', { name: 'Skip duplicates and add other items' }).click();
  await expect(review.getByRole('alert')).toHaveText('The result is unknown.');
  await expect(review.getByRole('button', { name: 'Back to editing' })).toBeDisabled();
  await page.keyboard.press('Escape');
  await expect(review).toBeVisible();
  await review.getByRole('button', { name: 'Retry addition' }).click();
  await expect(dialog).toHaveCount(0);
  expect(commands).toHaveLength(3);
  expect(commands[2]).toEqual(commands[1]);
  expect(commands[2].items.map((item) => item.name)).toEqual(['Rice']);
});

test.describe('Grocery list reads', () => {
  test('expands recipe sources from the requirement without shifting desktop columns', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await mockGroceryApi(page);
    const recipeName = 'Apple Crumble With Cinnamon, Maple Syrup and Toasted Walnuts';
    await page.route('**/api/grocery-list', (route) => route.fulfill({
      json: {
        ...groceryList,
        items: groceryList.items.map((item) => item.name === 'rolled oats' ? {
          ...item,
          name: 'maple syrup',
          dimension: 'volume',
          requirementAmount: 20,
          requirementUnit: 'ml',
          outstandingPurchaseQuantity: 1,
          contributions: [1, 2].map((step) => ({
            id: `00000000-0000-7000-8000-00000000050${step}`,
            origin: 'recipe',
            state: 'outstanding',
            originalAmountText: '2',
            originalUnitText: 'tsp',
            originalNotesText: null,
            interpretedAmount: 10,
            canonicalUnit: 'ml',
            unresolvedReason: null,
            recipeId: '00000000-0000-7000-8000-000000000503',
            recipeStepId: `00000000-0000-7000-8000-00000000051${step}`,
            recipeStepItemId: null,
            recipeNameSnapshot: recipeName,
          })),
        } : item),
      },
    }));
    await page.setViewportSize({ width: 900, height: 844 });
    await page.goto('/grocery-list');

    const table = page.getByRole('table', { name: 'Outstanding grocery items' });
    const requirement = table.getByText('Requirement: 20 ml', { exact: true });
    const source = table.getByRole('link', { name: new RegExp(recipeName, 'i') });
    const actions = table.getByRole('row', { name: /Maple Syrup/ }).getByRole('cell').last();
    const edit = actions.getByRole('button', { name: 'Edit', exact: true });
    const remove = actions.getByRole('button', { name: /Remove maple syrup/ });
    await expect(requirement).toBeVisible();
    await expect(table.getByText(/1 unverified Recipe suggestion/)).toBeVisible();
    await expect(table.getByText(/1 unverified Recipe suggestion/)).not.toContainText('remaining quantity');
    await expect(source).toBeHidden();
    const columnGeometry = () => table.getByRole('columnheader').evaluateAll((headers) =>
      headers.map((header) => {
        const { x, width } = header.getBoundingClientRect();
        return { x, width };
      }),
    );
    await page.evaluate(() => document.fonts.ready.then(() => undefined));
    await requirement.scrollIntoViewIfNeeded();
    const collapsedEdit = await edit.boundingBox();
    const collapsedRemove = await remove.boundingBox();
    const actionCell = await actions.boundingBox();
    expect(collapsedEdit!.x + collapsedEdit!.width).toBeLessThan(collapsedRemove!.x);
    expect(collapsedRemove!.y).toBeLessThan(collapsedEdit!.y + collapsedEdit!.height);
    expect(collapsedEdit!.y).toBeLessThan(collapsedRemove!.y + collapsedRemove!.height);
    expect(collapsedRemove!.x + collapsedRemove!.width).toBeLessThanOrEqual(actionCell!.x + actionCell!.width);
    const collapsedColumns = await columnGeometry();
    const collapsedRequirement = await requirement.boundingBox();
    expect(collapsedRequirement).not.toBeNull();
    const chevronPoint = { x: collapsedRequirement!.x + 7, y: collapsedRequirement!.y + 10 };
    await page.mouse.click(chevronPoint.x, chevronPoint.y);
    await expect(source).toBeVisible();
    await expect(source).toHaveCount(1);
    await expect(source.locator('..')).toHaveText(/ — 4 tsp$/);
    expect(await columnGeometry()).toEqual(collapsedColumns);
    expect(await requirement.boundingBox()).toEqual(collapsedRequirement);
    expect(await edit.boundingBox()).toEqual(collapsedEdit);
    expect(await remove.boundingBox()).toEqual(collapsedRemove);
    await page.mouse.click(chevronPoint.x, chevronPoint.y);
    await expect(source).toBeHidden();
    expect(await columnGeometry()).toEqual(collapsedColumns);
    await page.setViewportSize({ width: 320, height: 844 });
    await expect(page.locator('.grocery-item-card').filter({ hasText: 'Maple Syrup' })
      .getByText(/1 unverified Recipe suggestion/)).toBeVisible();
  });

  test('toggles departments independently and preserves expansion across desktop and mobile', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await mockGroceryApi(page);
    await page.setViewportSize({ width: 1280, height: 844 });
    await page.goto('/grocery-list');
    await page.getByLabel('Group for store').selectOption(selectedStoreId);

    const table = page.getByRole('table', { name: 'Outstanding grocery items' });
    const produce = table.getByRole('button', { name: 'Produce', exact: true });
    const pantry = table.getByRole('button', { name: 'Pantry', exact: true });
    await expect(produce).toHaveAttribute('aria-expanded', 'true');
    await expect(pantry).toHaveAttribute('aria-expanded', 'true');
    await produce.click();
    await expect(produce).toHaveAttribute('aria-expanded', 'false');
    await expect(table.getByRole('row', { name: /Green Apples/ })).toHaveCount(0);
    await expect(table.getByRole('row', { name: /Rolled Oats/ })).toBeVisible();
    await pantry.focus();
    await pantry.press('Enter');
    await expect(pantry).toHaveAttribute('aria-expanded', 'false');
    await expect(table.getByRole('row', { name: /Rolled Oats/ })).toHaveCount(0);

    await page.setViewportSize({ width: 390, height: 844 });
    const mobileProduce = page.getByRole('heading', { name: 'Produce', exact: true }).getByRole('button');
    const mobilePantry = page.getByRole('heading', { name: 'Pantry', exact: true }).getByRole('button');
    await expect(mobileProduce).toHaveAttribute('aria-expanded', 'false');
    await expect(mobilePantry).toHaveAttribute('aria-expanded', 'false');
    await mobileProduce.click();
    await expect(page.getByRole('heading', { name: 'Green Apples', exact: true })).toBeVisible();
    await expect(mobilePantry).toHaveAttribute('aria-expanded', 'false');
    await mobilePantry.focus();
    await mobilePantry.press('Space');
    await expect(page.getByRole('heading', { name: 'Rolled Oats', exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Green Apples', exact: true })).toBeVisible();

    await page.setViewportSize({ width: 1280, height: 844 });
    await expect(produce).toHaveAttribute('aria-expanded', 'true');
    await expect(pantry).toHaveAttribute('aria-expanded', 'true');
  });

  test('matches mobile department heading colours to desktop in both themes', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await mockGroceryApi(page);
    await page.goto('/grocery-list');
    await page.getByLabel('Group for store').selectOption(selectedStoreId);

    for (const theme of ['light', 'dark']) {
      await page.evaluate((value) => { document.documentElement.dataset.theme = value; }, theme);
      await page.setViewportSize({ width: 1280, height: 844 });
      const desktopHeading = page.getByRole('table', { name: 'Outstanding grocery items' })
        .getByRole('row', { name: 'Produce', exact: true }).locator('th');
      const colours = await desktopHeading.evaluate((element) => {
        const style = getComputedStyle(element);
        return { background: style.backgroundColor, text: style.color };
      });

      for (const width of [320, 390]) {
        await page.setViewportSize({ width, height: 844 });
        for (const name of ['Produce', 'Pantry', 'Uncategorised', 'Not stocked here']) {
          const heading = page.getByRole('heading', { name, exact: true });
          await expect(heading).toBeVisible();
          await expect(heading).toHaveCSS('background-color', colours.background);
          await expect(heading).toHaveCSS('color', colours.text);
        }
      }
    }
  });

  test('groups desktop items by selected-store department and persists the store', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await mockGroceryApi(page);
    await page.route('**/api/grocery-list', (route) => route.fulfill({
      json: {
        ...groceryList,
        items: groceryList.items.map((item, index) => index === 0
          ? { ...item, note: 'A shopping note that wraps over several lines in the requirements column. '.repeat(3) }
          : item),
      },
    }));
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/grocery-list');

    await expect(page.getByRole('heading', { name: 'Grocery List', exact: true })).toBeVisible();
    const table = page.getByRole('table', { name: 'Outstanding grocery items' });
    await expect(table).toBeVisible();
    await page.getByLabel('Group for store').selectOption(selectedStoreId);
    await expect(table.getByText('Produce', { exact: true })).toBeVisible();
    await expect(table.getByText('Not stocked here', { exact: true })).toBeVisible();
    await expect(table.getByText('Pantry', { exact: true })).toBeVisible();
    await expect(table.getByRole('row', { name: /Green Apples/ })).toBeVisible();
    const itemRow = table.getByRole('row', { name: /Green Apples/ });
    const rowBox = await itemRow.boundingBox();
    const actionBox = await itemRow.getByRole('cell').last().boundingBox();
    expect(rowBox).not.toBeNull();
    expect(actionBox).not.toBeNull();
    expect(Math.abs(actionBox!.y + actionBox!.height - rowBox!.y - rowBox!.height)).toBeLessThanOrEqual(1);
    for (const cell of await table.locator('tbody:last-child > tr:last-child > *').all()) {
      await expect(cell).toHaveCSS('border-bottom-width', '0px');
    }
    await page.reload();
    await expect(page.getByLabel('Group for store')).toHaveValue(selectedStoreId);
  });

  test('renders equivalent mobile cards and loads history with an Auckland date scope', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    const requests = await mockGroceryApi(page);
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/grocery-list');

    await page.getByLabel('Group for store').selectOption(selectedStoreId);
    await expect(page.getByRole('table', { name: 'Outstanding grocery items' })).toBeHidden();
    await expect(page.getByRole('heading', { name: 'Green Apples', exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Not stocked here', exact: true })).toBeVisible();

    await page.getByRole('link', { name: 'Purchase history' }).click();
    await expect(page.getByRole('heading', { name: 'Purchase History' })).toBeVisible();
    await expect(page.getByTestId('mobile-page-title')).toHaveText('Purchase History');
    await expect(page.getByRole('heading', { name: 'Green Apples' })).toBeVisible();
    await expect(page.getByRole('list', { name: 'Purchase history' }).getByText('Purchase reversed')).toBeVisible();
    await expect(requests.some((url) => new URL(url).searchParams.get('from') !== null)).toBe(true);

    for (const width of [320, 390, 1280]) {
      await page.setViewportSize({ width, height: 844 });
      for (const name of ['From', 'To']) {
        const input = page.getByLabel(name, { exact: true });
        const label = page.locator(`label[for="${await input.getAttribute('id')}"]`);
        const labelBox = await label.boundingBox();
        const inputBox = await input.boundingBox();
        expect(labelBox).not.toBeNull();
        expect(inputBox).not.toBeNull();
        expect(labelBox!.x + labelBox!.width).toBeLessThan(inputBox!.x);
        expect(labelBox!.y).toBeLessThan(inputBox!.y + inputBox!.height);
        expect(labelBox!.y + labelBox!.height).toBeGreaterThan(inputBox!.y);
      }
    }

    await page.getByRole('checkbox', { name: 'Show all dates' }).check();
    await expect.poll(() => requests.some((url) =>
      new URL(url).searchParams.get('allDates') === 'true')).toBe(true);
  });

  test('discovers a second-device update through visible-page polling', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await page.clock.install({ time: new Date('2026-04-05T00:00:00Z') });
    await page.clock.pauseAt(new Date('2026-04-05T00:00:01Z'));
    let readCount = 0;
    let secondDeviceUpdated = false;
    await page.route('**/api/supermarkets', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', json: [] }));
    await page.route('**/api/grocery-list', async (route) => {
      readCount += 1;
      const body = secondDeviceUpdated ? groceryList : { ...groceryList, items: [] };
      await route.fulfill({ status: 200, contentType: 'application/json', json: body });
    });
    await page.goto('/grocery-list');
    await expect(page.getByText('There are no outstanding grocery items.', { exact: true })).toBeVisible();
    const initialReadCount = readCount;

    secondDeviceUpdated = true;
    await page.clock.fastForward(15_000);

    const notice = page.getByRole('status').filter({ hasText: 'Grocery list updated.' });
    await expect(notice).toBeVisible();
    await expect(notice).toHaveCSS('background-color', 'rgb(255, 247, 225)');
    await expect(notice).toHaveCSS('border-top-color', 'rgb(255, 213, 138)');
    await expect(notice).toHaveCSS('color', 'rgb(67, 67, 67)');
    await page.evaluate(() => { document.documentElement.dataset.theme = 'dark'; });
    await expect(notice).toHaveCSS('background-color', 'rgb(73, 57, 31)');
    await expect(notice).toHaveCSS('border-top-color', 'rgb(255, 213, 138)');
    await expect(notice).toHaveCSS('color', 'rgb(198, 199, 198)');
    await expect(page.getByRole('table', { name: 'Outstanding grocery items' })).toBeVisible();
    expect(readCount).toBeGreaterThan(initialReadCount);
    await page.clock.fastForward(4_000);
    await expect(notice).toHaveCount(0);
  });
});
