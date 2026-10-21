import { expect, test } from '@playwright/test';
import { mockAuthApi } from '../support/mockAuthApi.ts';

const pages = [
  { route: '/home', heading: 'Home' },
  { route: '/grocery-list', heading: 'Grocery List' },
  { route: '/food-diary', heading: 'Food Diary' },
  { route: '/ingredient-maintenance', heading: 'Ingredient Maintenance' },
];

test.describe('Shared page layout', () => {
  test.beforeEach(async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await page.route('**/api/supermarkets', (route) =>
      route.fulfill({ json: [] }));
    await page.route('**/api/ingredients**', (route) =>
      route.fulfill({ json: { items: [], totalCount: 0, page: 1, pageSize: 25 } }));
  });

  for (const width of [320, 390, 700, 701, 1280, 1920]) {
    for (const destination of pages) {
      test(`${destination.heading} fills the available width at ${width}px`, async ({ page }) => {
        await page.setViewportSize({ width, height: 900 });
        await page.goto(destination.route);

        const panel = page.getByRole('region', { name: destination.heading, exact: true });
        await expect(panel).toBeVisible();
        const bounds = await panel.boundingBox();
        if (!bounds) {
          throw new Error('The page panel has no bounding box.');
        }

        const gutter = width <= 700 ? 16 : 32;
        expect(bounds.x).toBeCloseTo(gutter, 0);
        expect(bounds.width).toBeCloseTo(width - (2 * gutter), 0);
        expect(width - bounds.x - bounds.width).toBeCloseTo(gutter, 0);
        expect(await page.evaluate(() =>
          document.documentElement.scrollWidth > document.documentElement.clientWidth)).toBe(false);
      });
    }
  }
});
