import { expect, test } from '@playwright/test';
import type { GroceryRecipeRemovalCommand } from '../../../src/features/grocery-list/services/groceryListApi.ts';
import { mockAuthApi } from '../support/mockAuthApi.ts';
import { groceryList, mockGroceryApi } from '../support/mockGroceryApi.ts';

function contrastRatio(foreground: string, background: string): number {
  const luminance = (color: string) => {
    const values = color.match(/\d+/g);
    if (!values || values.length !== 3) throw new Error(`Unsupported colour: ${color}`);
    const [red, green, blue] = values.map((value) => {
      const channel = Number(value) / 255;
      return channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
    });
    return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
  };
  const values = [luminance(foreground), luminance(background)].sort((a, b) => b - a);
  return (values[0] + 0.05) / (values[1] + 0.05);
}

for (const width of [320, 390, 1280]) {
  test(`keeps recipe removal icons beside long names at ${width}px`, async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await mockGroceryApi(page);
    const list = structuredClone(groceryList);
    const names = [
      'baby banana blueberry muffins with extra delicious berries',
      'supercalifragilisticexpialidociousblueberrymuffins',
    ];
    list.items[0].contributions = names.map((name, index) => ({
      id: `00000000-0000-7000-8000-00000000051${index}`,
      origin: 'recipe' as const,
      state: 'outstanding' as const,
      originalAmountText: '1',
      originalUnitText: 'item',
      originalNotesText: null,
      interpretedAmount: 1,
      canonicalUnit: 'item',
      unresolvedReason: null,
      recipeId: `00000000-0000-7000-8000-00000000050${index}`,
      recipeStepId: null,
      recipeStepItemId: null,
      recipeNameSnapshot: name,
    }));
    await page.route('**/api/grocery-list', (route) => route.fulfill({ json: list }));
    await page.setViewportSize({ width, height: 844 });
    await page.goto('/grocery-list');
    const accordion = page.locator('details.grocery-recipe-sources');
    await accordion.getByText('Recipes on Grocery List').click();

    const rows = accordion.getByRole('listitem');
    await expect(rows).toHaveCount(2);
    for (const row of await rows.all()) {
      const link = row.getByRole('link');
      const button = row.getByRole('button', { name: /Remove .* from Grocery List/ });
      await expect(button.locator('svg')).toHaveCount(1);
      const [rowBox, linkBox, buttonBox] = await Promise.all([
        row.boundingBox(), link.boundingBox(), button.boundingBox(),
      ]);
      expect(rowBox).not.toBeNull();
      expect(linkBox).not.toBeNull();
      expect(buttonBox).not.toBeNull();
      expect(buttonBox!.width).toBeGreaterThanOrEqual(40);
      expect(buttonBox!.height).toBeGreaterThanOrEqual(40);
      expect(linkBox!.x + linkBox!.width).toBeLessThanOrEqual(buttonBox!.x);
      expect(buttonBox!.x + buttonBox!.width).toBeLessThanOrEqual(rowBox!.x + rowBox!.width);
      expect(Math.abs(buttonBox!.y + buttonBox!.height / 2 - rowBox!.y - rowBox!.height / 2))
        .toBeLessThanOrEqual(1);
      if (width <= 390) {
        expect(linkBox!.height).toBeGreaterThan(30);
      }
      const tooltip = button.getByText('Remove', { exact: true });
      await button.hover();
      if (width > 700) {
        await expect(tooltip).toBeVisible();
      } else {
        await expect(tooltip).toBeHidden();
      }
    }
  });
}

for (const width of [320, 1280]) {
  test(`removes a contributing Recipe from the Grocery List at ${width}px`, async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await mockGroceryApi(page);
    const list = structuredClone(groceryList);
    const recipeId = '00000000-0000-7000-8000-000000000501';
    const otherId = '00000000-0000-7000-8000-000000000502';
    const contribution = (id: string, sourceId: string, name: string) => ({
      id, origin: 'recipe' as const, state: 'outstanding' as const,
      originalAmountText: '1', originalUnitText: 'item', originalNotesText: null,
      interpretedAmount: 1, canonicalUnit: 'item', unresolvedReason: null,
      recipeId: sourceId, recipeStepId: null, recipeStepItemId: null, recipeNameSnapshot: name,
    });
    const apple = list.items[0];
    apple.outstandingPurchaseQuantity = 4;
    apple.contributions = [
      contribution('00000000-0000-7000-8000-000000000511', recipeId, 'apple crumble'),
      contribution('00000000-0000-7000-8000-000000000512', recipeId, 'apple crumble'),
      contribution('00000000-0000-7000-8000-000000000513', otherId, 'pear tart'),
      { ...contribution('00000000-0000-7000-8000-000000000514', recipeId, 'apple crumble'),
        origin: 'manual', recipeId: null, recipeNameSnapshot: null },
    ];
    list.items[1].contributions = [
      contribution('00000000-0000-7000-8000-000000000515', recipeId, 'apple crumble'),
    ];
    const writes: GroceryRecipeRemovalCommand[] = [];
    await page.route('**/api/grocery-list', (route) => route.fulfill({ json: list }));
    await page.route(`**/api/grocery-list/recipes/${recipeId}/contributions`, async (route) => {
      expect(route.request().method()).toBe('DELETE');
      const request = route.request().postDataJSON() as GroceryRecipeRemovalCommand;
      writes.push(request);
      apple.contributions = apple.contributions.filter((source) => source.recipeId !== recipeId);
      apple.outstandingPurchaseQuantity = 2;
      list.items.splice(1, 1);
      list.version += 1;
      await route.fulfill({ json: { commandId: request.commandId, resultListVersion: list.version, resultIds: [] } });
    });
    await page.setViewportSize({ width, height: 844 });
    await page.goto('/grocery-list');
    const summary = page.getByText('Recipes on Grocery List', { exact: true });
    const accordion = page.locator('details.grocery-recipe-sources');
    await expect(accordion).not.toHaveAttribute('open', '');
    const selectorBox = await page.getByLabel('Group for store').boundingBox();
    const summaryBox = await summary.boundingBox();
    expect(summaryBox!.y).toBeGreaterThanOrEqual(selectorBox!.y + selectorBox!.height);
    await expect(summary).toHaveCSS('font-size', '18px');
    await expect(accordion).toHaveCSS('border-top-style', 'solid');
    await summary.focus();
    await page.keyboard.press('Enter');
    await expect(accordion.getByRole('link')).toHaveText(['Apple Crumble', 'Pear Tart']);
    const link = accordion.getByRole('link').first();
    for (const theme of ['light', 'dark']) {
      await page.evaluate((value) => { document.documentElement.dataset.theme = value; }, theme);
      const header = await summary.evaluate((element) => {
        const style = getComputedStyle(element);
        return { color: style.color, background: style.backgroundColor };
      });
      const content = await accordion.evaluate((element) => {
        const style = getComputedStyle(element);
        return { color: style.color, background: style.backgroundColor, border: style.borderTopColor };
      });
      if (theme === 'light') {
        expect(header.background).toBe('rgb(4, 219, 162)');
        expect(content.background).toBe('rgb(174, 255, 234)');
      } else {
        expect(header.background).not.toBe('rgb(4, 219, 162)');
        expect(content.background).not.toBe('rgb(174, 255, 234)');
      }
      expect(content.border).toBe(header.background);
      expect(contrastRatio(header.color, header.background)).toBeGreaterThanOrEqual(4.5);
      expect(contrastRatio(content.color, content.background)).toBeGreaterThanOrEqual(4.5);
      await page.mouse.move(0, 0);
      expect(contrastRatio(await link.evaluate((element) => getComputedStyle(element).color), content.background))
        .toBeGreaterThanOrEqual(4.5);
      await link.hover();
      expect(contrastRatio(await link.evaluate((element) => getComputedStyle(element).color), content.background))
        .toBeGreaterThanOrEqual(4.5);
    }
    const remove = accordion.getByRole('button', { name: 'Remove Apple Crumble from Grocery List' });
    await remove.click();
    const dialog = page.getByRole('dialog', { name: 'Remove Recipe from Grocery List?' });
    await expect(dialog.getByRole('button', { name: 'Cancel' })).toBeFocused();
    await dialog.getByRole('button', { name: 'Cancel' }).click();
    expect(writes).toHaveLength(0);
    await remove.click();
    await dialog.getByRole('button', { name: 'Remove Recipe', exact: true }).click();
    await expect.poll(() => writes.length).toBe(1);
    expect(writes[0]).toMatchObject({ commandId: expect.any(String), expectedListVersion: 1 });
    await expect(accordion.getByRole('link')).toHaveText(['Pear Tart']);
    await expect(page.getByRole('spinbutton', { name: 'Quantity to buy for Green Apples' })).toHaveValue('2');
    await expect(page.getByRole('spinbutton', { name: 'Quantity to buy for Kale' })).toHaveCount(0);
  });
}
