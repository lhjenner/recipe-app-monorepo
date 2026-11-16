import { expect, test } from '@playwright/test';
import { mockAuthApi } from '../support/mockAuthApi.ts';
import { groceryList, mockGroceryApi, selectedStoreId } from '../support/mockGroceryApi.ts';
import { ingredient, mockIngredientApi } from '../support/mockIngredientListApi.ts';

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
  await expect(dialog.getByRole('status')).toContainText('Linked to an existing Ingredient.');

  await name.fill('crumpets');
  await dialog.getByRole('spinbutton', { name: 'Quantity' }).focus();
  await expect(name).toHaveValue('Crumpets');
  await expect(dialog.getByRole('status')).toHaveCount(0);
  await expect(dialog.getByRole('checkbox', { name: 'Save to master Ingredient list' })).toBeVisible();
});

test.describe('Grocery list reads', () => {
  test('groups desktop items by selected-store department and persists the store', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await mockGroceryApi(page);
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
