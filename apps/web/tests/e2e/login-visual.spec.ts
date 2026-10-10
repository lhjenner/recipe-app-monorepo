import { expect, test } from '@playwright/test';
import { mockAuthApi } from './support/mockAuthApi.ts';

const viewports = [
  { name: 'desktop', size: { width: 1280, height: 800 } },
  { name: 'mobile', size: { width: 375, height: 667 } },
];

test.describe('Login page responsive presentation', () => {
  for (const { name, size } of viewports) {
    test(`AC-08 renders well on ${name}`, async ({ page }) => {
      await mockAuthApi(page);
      await page.setViewportSize(size);
      await page.goto('/');
      await expect(page.getByRole('heading', { name: 'Log in' })).toBeVisible();

      const hasHorizontalScroll = await page.evaluate(
        () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
      );
      expect(hasHorizontalScroll).toBe(false);

      for (const control of [page.getByLabel('Email'), page.getByLabel('Password'), page.getByRole('button', { name: 'Log in' })]) {
        const box = await control.boundingBox();
        expect(box!.x).toBeGreaterThanOrEqual(0);
        expect(box!.x + box!.width).toBeLessThanOrEqual(size.width);
      }

      await expect(page).toHaveScreenshot(`login-${name}.png`, { fullPage: true, animations: 'disabled' });
    });
  }

  test('AC-08 uses a single stylesheet set at both sizes', async ({ page }) => {
    await mockAuthApi(page);
    const stylesheetsBySize: string[][] = [];

    for (const { size } of viewports) {
      await page.setViewportSize(size);
      await page.goto('/');
      stylesheetsBySize.push(await page.evaluate(() =>
        Array.from(document.styleSheets).map((sheet) => sheet.href ?? 'inline').sort()));
    }

    expect(stylesheetsBySize[0]).toEqual(stylesheetsBySize[1]);
  });
});
