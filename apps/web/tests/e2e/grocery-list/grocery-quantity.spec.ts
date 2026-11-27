import { expect, test } from '@playwright/test';
import type { GroceryItemUpdateRequest } from '../../../src/features/grocery-list/services/groceryListApi.ts';
import { mockAuthApi } from '../support/mockAuthApi.ts';
import { groceryList, mockGroceryApi, selectedStoreId } from '../support/mockGroceryApi.ts';

for (const width of [320, 390, 1280]) {
  test(`updates purchase quantities inline without units at ${width}px`, async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await mockGroceryApi(page);
    const list = structuredClone(groceryList);
    const salt = list.items.find((item) => item.name === 'table salt')!;
    salt.outstandingPurchaseQuantity = 1;
    salt.note = 'Finely ground';
    salt.adHocStoreId = selectedStoreId;
    list.items[0].outstandingPurchaseQuantity = 9999;
    list.items[1].outstandingPurchaseQuantity = 250;
    list.items[1].purchaseUnit = 'g';
    list.items[1].dimension = 'mass';
    const writes: GroceryItemUpdateRequest[] = [];
    await page.route('**/api/grocery-list', (route) => route.fulfill({ json: list }));
    await page.route('**/api/grocery-list/items/*', async (route) => {
      const request = route.request().postDataJSON() as GroceryItemUpdateRequest;
      expect(route.request().method()).toBe('PUT');
      expect(request.expectedListVersion).toBe(list.version);
      const id = route.request().url().split('/').at(-1);
      const item = list.items.find((value) => value.id === id);
      if (!item) {
        throw new Error('Quantity update targeted an unknown grocery item.');
      }
      writes.push(request);
      item.outstandingPurchaseQuantity = request.outstandingPurchaseQuantity;
      list.version += 1;
      await route.fulfill({
        json: { commandId: request.commandId, resultListVersion: list.version, resultIds: [item.id] },
      });
    });
    await page.setViewportSize({ width, height: 844 });
    await page.goto('/grocery-list');

    const quantity = page.getByRole('spinbutton', { name: 'Quantity to buy for Table Salt' });
    const decrease = page.getByRole('button', { name: 'Decrease quantity for Table Salt' });
    await expect(quantity).toHaveValue('1');
    await expect(page.getByRole('spinbutton', { name: 'Quantity to buy for Kale' })).toHaveValue('250');
    await expect(decrease).toBeDisabled();
    await expect(page.getByRole('button', { name: 'Increase quantity for Green Apples' })).toBeDisabled();
    await page.getByRole('button', { name: 'Increase quantity for Table Salt' }).click();
    await expect(quantity).toHaveValue('2');
    await decrease.click();
    await expect(quantity).toHaveValue('1');
    await expect(decrease).toBeDisabled();
    await expect(page.getByRole('dialog')).toHaveCount(0);
    expect(writes.map((request) => request.outstandingPurchaseQuantity)).toEqual([2, 1]);
    expect(writes[0]).toMatchObject({
      purchaseUnit: 'item', note: 'Finely ground', departmentUpdate: null,
      adHocStoreId: selectedStoreId, adHocDepartmentId: null,
    });
    const buyArea = width > 700
      ? page.getByRole('row', { name: /Table Salt/ }).getByRole('cell').nth(1)
      : page.locator('.grocery-item-card').filter({ has: quantity }).locator('.grocery-item-card-controls');
    if (width <= 700) {
      const card = page.locator('.grocery-item-card').filter({ has: quantity });
      await expect(card.getByText('To buy:', { exact: true })).toHaveCount(0);
      const cardBox = await card.boundingBox();
      expect(cardBox).not.toBeNull();
      const quantityBox = await quantity.boundingBox();
      expect(quantityBox).not.toBeNull();
      expect(quantityBox!.width).toBeLessThanOrEqual(48);
      const editBox = await card.getByRole('button', { name: 'Edit', exact: true }).boundingBox();
      expect(editBox).not.toBeNull();
      expect(editBox!.width).toBeGreaterThanOrEqual(40);
      for (const button of [decrease, card.getByRole('button', { name: 'Increase quantity for Table Salt' })]) {
        const box = await button.boundingBox();
        expect(box).not.toBeNull();
        expect(Math.abs(box!.width - box!.height)).toBeLessThanOrEqual(1);
      }
      const controls = [
        decrease,
        quantity,
        card.getByRole('button', { name: 'Increase quantity for Table Salt' }),
        card.getByRole('button', { name: 'Mark purchased', exact: true }),
        card.getByRole('button', { name: 'Edit', exact: true }),
        card.getByRole('button', { name: 'Remove table salt from Grocery List' }),
      ];
      let centreY: number | null = null;
      let right = cardBox!.x;
      for (const control of controls) {
        const box = await control.boundingBox();
        expect(box).not.toBeNull();
        centreY ??= box!.y + box!.height / 2;
        expect(Math.abs(box!.y + box!.height / 2 - centreY)).toBeLessThanOrEqual(1);
        expect(Math.abs(box!.height - quantityBox!.height)).toBeLessThanOrEqual(1);
        expect(box!.x).toBeGreaterThanOrEqual(right);
        right = box!.x + box!.width;
        expect(right).toBeLessThanOrEqual(cardBox!.x + cardBox!.width);
      }
      const rowBox = await buyArea.boundingBox();
      const decreaseBox = await decrease.boundingBox();
      expect(rowBox).not.toBeNull();
      expect(decreaseBox).not.toBeNull();
      expect(Math.abs(decreaseBox!.x - rowBox!.x)).toBeLessThanOrEqual(1);
      expect(Math.abs(right - rowBox!.x - rowBox!.width)).toBeLessThanOrEqual(1);
    }
    await expect(buyArea).not.toContainText('item');
    await page.reload();
    await expect(quantity).toHaveValue('1');
  });
}

for (const width of [320, 390, 701, 1280]) {
  test(`grocery action icons fit and expose desktop tooltips at ${width}px`, async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await mockGroceryApi(page);
    await page.setViewportSize({ width, height: 844 });
    await page.goto('/grocery-list');

    const item = width > 700
      ? page.getByRole('row', { name: /Green Apples/ })
      : page.locator('.grocery-item-card').filter({ has: page.getByRole('heading', { name: 'Green Apples' }) });
    const container = width > 700 ? item.getByRole('cell').last() : item;
    const bounds = await container.boundingBox();
    expect(bounds).not.toBeNull();
    const tableBounds = width > 700 ? await page.locator('.grocery-items-table-wrap').boundingBox() : bounds;
    expect(tableBounds).not.toBeNull();
    for (const [name, tooltip] of [
      ['Mark purchased', 'Mark purchased'],
      ['Edit', 'Edit'],
      ['Remove green apples from Grocery List', 'Remove'],
    ]) {
      const button = item.getByRole('button', { name, exact: true });
      await expect(button.locator('svg')).toHaveCount(1);
      const box = await button.boundingBox();
      expect(box).not.toBeNull();
      expect(box!.width).toBeGreaterThanOrEqual(40);
      expect(box!.height).toBeGreaterThanOrEqual(40);
      expect(box!.x).toBeGreaterThanOrEqual(bounds!.x);
      expect(box!.x + box!.width).toBeLessThanOrEqual(bounds!.x + bounds!.width);
      expect(box!.x + box!.width).toBeLessThanOrEqual(tableBounds!.x + tableBounds!.width);
      const label = button.getByText(tooltip, { exact: true });
      await expect(label).toBeHidden();
      await button.hover();
      if (width > 700) {
        await expect(label).toBeVisible();
        const labelBox = await label.boundingBox();
        expect(labelBox).not.toBeNull();
        expect(labelBox!.x).toBeGreaterThanOrEqual(tableBounds!.x);
        expect(labelBox!.x + labelBox!.width).toBeLessThanOrEqual(tableBounds!.x + tableBounds!.width);
        await page.mouse.move(0, 0);
        await button.focus();
        await expect(label).toBeVisible();
      } else {
        await expect(label).toBeHidden();
      }
    }
  });
}

test.describe('wide touch-only device', () => {
  test.use({ viewport: { width: 820, height: 1180 }, hasTouch: true, isMobile: true });

  test('does not show action tooltips', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await mockGroceryApi(page);
    await page.goto('/grocery-list');
    const button = page.getByRole('row', { name: /Green Apples/ })
      .getByRole('button', { name: 'Mark purchased' });
    await button.focus();
    await expect(button.getByText('Mark purchased', { exact: true })).toBeHidden();
  });
});
