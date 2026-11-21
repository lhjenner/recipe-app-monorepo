import { expect, test } from '@playwright/test';
import { mockAuthApi } from '../support/mockAuthApi.ts';
import { groceryList, mockGroceryApi, selectedStoreId } from '../support/mockGroceryApi.ts';
import { ingredient, mockIngredientApi } from '../support/mockIngredientListApi.ts';

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

    await expect(page.getByRole('status')).toContainText('Grocery list updated.');
    await expect(page.getByRole('table', { name: 'Outstanding grocery items' })).toBeVisible();
    expect(readCount).toBeGreaterThan(initialReadCount);
  });
});
