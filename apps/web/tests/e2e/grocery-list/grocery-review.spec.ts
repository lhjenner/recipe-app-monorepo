import { expect, test } from '@playwright/test';
import { mockAuthApi } from '../support/mockAuthApi.ts';
import { groceryList, mockGroceryApi } from '../support/mockGroceryApi.ts';

test('reviews concurrent changes per field before saving the resolved draft', async ({ page }) => {
  await mockAuthApi(page, { hasSession: true });
  await mockGroceryApi(page);
  await page.route('**/api/departments', (route) => route.fulfill({ json: [] }));
  let latest = groceryList;
  await page.route('**/api/grocery-list', (route) => route.fulfill({ json: latest }));
  let saved: Record<string, unknown> | null = null;
  await page.route('**/api/grocery-list/items/*', (route) => {
    saved = route.request().postDataJSON() as Record<string, unknown>;
    return route.fulfill({ json: { commandId: saved.commandId, resultListVersion: 3, resultIds: [groceryList.items[0].id] } });
  });
  await page.goto('/grocery-list');
  await page.getByRole('row', { name: /Green Apples/ }).getByRole('button', { name: 'Edit', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: /Edit green apples/i });
  await dialog.getByLabel('Quantity').fill('3');
  await dialog.getByLabel('Note (optional)').fill('My note');
  latest = {
    ...groceryList, version: 2,
    items: groceryList.items.map((item, index) => index === 0
      ? { ...item, outstandingPurchaseQuantity: 4, note: 'Other note' } : item),
  };
  await page.evaluate(() => window.dispatchEvent(new Event('online')));
  await dialog.getByRole('button', { name: 'Review latest list' }).click();
  await expect(dialog.getByText('My quantity: 3')).toBeVisible();
  await expect(dialog.getByText('Latest quantity: 4')).toBeVisible();
  await dialog.getByRole('radio', { name: 'Use latest quantity' }).check();
  await dialog.getByRole('radio', { name: 'Keep my note' }).check();
  await dialog.getByRole('button', { name: 'Apply review choices' }).click();
  await dialog.getByRole('button', { name: 'Save changes' }).click();
  await expect.poll(() => saved).toMatchObject({
    expectedListVersion: 2, outstandingPurchaseQuantity: 4, note: 'My note',
  });
});

test('retries an ambiguous removal even after the list stops showing the item', async ({ page }) => {
  await mockAuthApi(page, { hasSession: true });
  await mockGroceryApi(page);
  let latest = groceryList;
  await page.route('**/api/grocery-list', (route) => route.fulfill({ json: latest }));
  const requests: { command: string | undefined; version: string | undefined }[] = [];
  await page.route('**/api/grocery-list/items/*', async (route) => {
    const headers = route.request().headers();
    requests.push({ command: headers['x-command-id'], version: headers['x-expected-list-version'] });
    if (requests.length === 1) {
      await route.abort('failed');
    } else {
      await route.fulfill({ status: 204 });
    }
  });
  await page.goto('/grocery-list');
  await page.getByRole('button', { name: 'Remove green apples from Grocery List' }).click();
  await page.getByRole('dialog', { name: 'Remove grocery item?' })
    .getByRole('button', { name: 'Remove item' }).click();
  const retry = page.getByRole('button', { name: 'Retry removal' });
  await expect(retry).toBeVisible();
  latest = { ...groceryList, version: 2, items: groceryList.items.slice(1) };
  await page.evaluate(() => window.dispatchEvent(new Event('online')));
  await expect(page.getByRole('button', { name: 'Remove green apples from Grocery List' })).toHaveCount(0);
  await retry.click();
  await expect.poll(() => requests.length).toBe(2);
  expect(requests[0].command).toBeTruthy();
  expect(requests[0].version).toBe('1');
  expect(requests[1]).toEqual(requests[0]);
  await expect(retry).toHaveCount(0);
});
