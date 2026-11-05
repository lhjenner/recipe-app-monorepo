import { expect, test } from '@playwright/test';
import { mockAuthApi } from '../support/mockAuthApi.ts';
import { mockRecipeApi, recipeDetail, recipeId } from '../support/mockRecipeApi.ts';

test('pairs mobile metadata fields while preserving full-width source and desktop order', async ({ page }) => {
  await mockAuthApi(page, { hasSession: true });
  const details = {
    [recipeId]: {
      ...recipeDetail,
      sourceText: 'A very long recipe source name that needs room to wrap across the entire metadata row',
      cookTimeText: '3 minutes',
    },
  };
  await mockRecipeApi(page, { details });

  for (const width of [320, 375, 700, 1280]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto(`/recipes/${recipeId}`);
    const metadata = page.locator('.recipe-metadata');
    await expect(metadata.getByText('3 minutes', { exact: true })).toBeVisible();
    const bounds = await metadata.locator(':scope > div').evaluateAll((elements) =>
      Object.fromEntries(elements.map((element) => {
        const { x, y, width, height } = element.getBoundingClientRect();
        return [element.querySelector('dt')!.textContent!, { x, y, width, height }];
      })));

    if (width <= 700) {
      const panel = await metadata.boundingBox();
      expect(bounds.Source!.width).toBeCloseTo(panel!.width, 0);
      expect(bounds.Servings!.y).toBe(bounds['Date added']!.y);
      expect(bounds['Prep time']!.y).toBe(bounds['Cook time']!.y);
      expect(bounds.Servings!.x).toBe(bounds['Prep time']!.x);
      expect(bounds['Date added']!.x).toBe(bounds['Cook time']!.x);
      expect(bounds['Date added']!.x).toBeGreaterThan(bounds.Servings!.x);
      expect(bounds.Servings!.y).toBeGreaterThanOrEqual(bounds.Source!.y + bounds.Source!.height);
      expect(bounds['Prep time']!.y).toBeGreaterThan(bounds.Servings!.y);
    } else {
      expect(bounds.Source!.x).toBeLessThan(bounds.Servings!.x);
      expect(bounds.Servings!.x).toBeLessThan(bounds['Prep time']!.x);
      expect(bounds['Prep time']!.x).toBeLessThan(bounds['Cook time']!.x);
    }
    expect(await page.evaluate(() =>
      document.documentElement.scrollWidth > document.documentElement.clientWidth)).toBe(false);
  }

  details[recipeId] = {
    ...details[recipeId],
    sourceText: '',
    servingsText: '',
    prepTimeText: '',
  };
  await page.setViewportSize({ width: 375, height: 900 });
  await page.goto(`/recipes/${recipeId}`);
  const metadata = page.locator('.recipe-metadata');
  await expect(metadata.locator('dt')).toHaveText(['Cook time', 'Date added']);
  const cook = await metadata.locator('div').filter({ hasText: 'Cook time' }).boundingBox();
  const date = await metadata.locator('div').filter({ hasText: 'Date added' }).boundingBox();
  expect(cook!.x).toBe(date!.x);
  expect(cook!.y).toBeGreaterThan(date!.y);
});
