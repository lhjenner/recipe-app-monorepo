import { expect, test } from '@playwright/test';
import { mockAuthApi } from './support/mockAuthApi.ts';

const menuLabels = [
  'Home',
  'Recipe List',
  'Add Recipe',
  'Grocery List',
  'Meal Planner',
  'Food Diary',
  'Ingredient Maintenance',
  'Archived Recipes',
];

async function openFlyout(page: import('@playwright/test').Page) {
  await page.getByTestId('mobile-menu-button').click();
  await expect(page.getByTestId('mobile-flyout')).toBeVisible();
}

test.describe('Mobile navigation flyout', () => {
  test.beforeEach(async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/home');
  });

  test('AC-14 shows the compact mobile header', async ({ page }) => {
    await expect(page.getByTestId('mobile-menu-button')).toHaveAccessibleName('Open menu');
    await expect(page.getByTestId('mobile-page-title')).toHaveText('Home');
    await expect(page.getByRole('navigation', { name: 'Main' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Log out' })).toHaveCount(0);
  });

  test('AC-15 opens an accessible, scroll-locking flyout', async ({ page }) => {
    const menuButton = page.getByTestId('mobile-menu-button');
    await openFlyout(page);

    await expect(menuButton).toHaveAttribute('aria-expanded', 'true');
    await expect(menuButton).toHaveAttribute('aria-controls', 'mobile-flyout');
    await expect(page.getByTestId('mobile-flyout').getByRole('link')).toHaveText(menuLabels);
    await expect(page.getByTestId('mobile-flyout').getByRole('button', { name: 'Log out' })).toHaveClass(/flyout-logout/);
    await expect(page.getByTestId('mobile-flyout').getByRole('link', { name: 'Home', exact: true })).toBeFocused();
    await expect(page.locator('body')).toHaveCSS('overflow', 'hidden');
  });

  test('AC-16 selecting a destination closes the flyout and releases scroll lock', async ({ page }) => {
    await openFlyout(page);
    await page.getByTestId('mobile-flyout').getByRole('link', { name: 'Grocery List', exact: true }).click();

    await expect(page).toHaveURL(/\/grocery-list$/);
    await expect(page.getByTestId('mobile-flyout')).toHaveCount(0);
    await expect(page.locator('body')).not.toHaveCSS('overflow', 'hidden');
  });

  test('AC-17 closes on Escape and outside tap, restoring focus to the menu button', async ({ page }) => {
    const menuButton = page.getByTestId('mobile-menu-button');
    await openFlyout(page);
    await page.keyboard.press('Escape');

    await expect(page.getByTestId('mobile-flyout')).toHaveCount(0);
    await expect(menuButton).toBeFocused();
    await expect(page.locator('body')).not.toHaveCSS('overflow', 'hidden');

    await openFlyout(page);
    await page.getByTestId('mobile-flyout-overlay').click({ position: { x: 350, y: 5 } });

    await expect(page.getByTestId('mobile-flyout')).toHaveCount(0);
    await expect(menuButton).toBeFocused();
  });

  test('AC-18 traps focus within the flyout', async ({ page }) => {
    const flyout = page.getByTestId('mobile-flyout');
    await openFlyout(page);

    await page.keyboard.press('Shift+Tab');
    await expect(flyout.getByRole('button', { name: 'Log out' })).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(flyout.getByRole('link', { name: 'Home', exact: true })).toBeFocused();
  });

  test('AC-19 closes the flyout when switching to desktop width', async ({ page }) => {
    await openFlyout(page);
    await page.setViewportSize({ width: 800, height: 667 });

    await expect(page.getByTestId('mobile-flyout')).toHaveCount(0);
    await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible();
    await expect(page.locator('body')).not.toHaveCSS('overflow', 'hidden');
  });

  test('AC-20 disables flyout animation for reduced motion', async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await openFlyout(page);

    await expect(page.getByTestId('mobile-flyout')).toHaveCSS('animation-name', 'none');
  });

  test('AC-21 selecting the active item closes the flyout without navigation', async ({ page }) => {
    const historyLength = await page.evaluate(() => window.history.length);
    await openFlyout(page);
    await page.getByTestId('mobile-flyout').getByRole('link', { name: 'Home', exact: true }).click();

    await expect(page).toHaveURL(/\/home$/);
    expect(await page.evaluate(() => window.history.length)).toBe(historyLength);
    await expect(page.getByTestId('mobile-flyout')).toHaveCount(0);
  });
});
