import { expect, test } from '@playwright/test';
import { mockAuthApi } from '../support/mockAuthApi.ts';

test.describe('Active desktop navigation item', () => {
  test.beforeEach(async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/grocery-list');
  });

  test('AC-06 marks only the current destination as active', async ({ page }) => {
    const navigation = page.getByRole('navigation', { name: 'Main' });
    const activeLink = navigation.getByRole('link', { name: 'Grocery List', exact: true });

    await expect(activeLink).toHaveAttribute('aria-current', 'page');
    await expect(activeLink).toHaveClass(/app-navigation-link-active/);
    await expect(navigation.getByRole('link', { name: 'Home', exact: true })).not.toHaveAttribute('aria-current');
  });

  test('AC-08 selecting the active item does not navigate or add history', async ({ page }) => {
    const activeLink = page.getByRole('link', { name: 'Grocery List', exact: true });
    const historyLength = await page.evaluate(() => window.history.length);

    await activeLink.click();

    await expect(page).toHaveURL(/\/grocery-list$/);
    expect(await page.evaluate(() => window.history.length)).toBe(historyLength);
    await expect(activeLink).toBeFocused();
  });

  test('AC-09 does not mark an item active on the protected 404 page', async ({ page }) => {
    await page.goto('/unknown-route');

    await expect(page.getByRole('navigation', { name: 'Main' }).getByRole('link')).toHaveCount(8);
    await expect(page.locator('[aria-current="page"]')).toHaveCount(0);
  });
});
