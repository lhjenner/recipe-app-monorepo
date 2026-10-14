import { expect, test } from '@playwright/test';
import { mockAuthApi } from '../support/mockAuthApi.ts';

test.describe('Sticky app header', () => {
  test.beforeEach(async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/home');
  });

  test('AC-10 is sticky at the top of the viewport', async ({ page }) => {
    const header = page.getByTestId('app-header');

    await expect(header).toHaveCSS('position', 'sticky');
    await expect(header).toHaveCSS('top', '0px');
  });

  test('AC-11 remains fully visible after scrolling an overflowing stub page', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 300 });
    await page.goto('/recipes');

    expect(await page.evaluate(() => document.documentElement.scrollHeight > window.innerHeight)).toBe(true);
    await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));
    expect(await page.evaluate(() => window.scrollY)).toBeGreaterThan(0);

    const header = page.getByTestId('app-header');
    const headerBox = await header.boundingBox();
    expect(headerBox!.y).toBe(0);
    expect(headerBox!.y + headerBox!.height).toBeLessThanOrEqual(300);
  });
});
