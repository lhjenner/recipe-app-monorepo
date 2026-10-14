import { expect, test } from '@playwright/test';
import { mockAuthApi } from '../support/mockAuthApi.ts';

const navigationItems = [
  { label: 'Home', route: '/home', pageTitle: 'Home' },
  { label: 'Recipe List', route: '/recipes', pageTitle: 'Recipe List' },
  { label: 'Add Recipe', route: '/recipes/new', pageTitle: 'Add Recipe' },
  { label: 'Grocery List', route: '/grocery-list', pageTitle: 'Grocery List' },
  { label: 'Meal Planner', route: '/meal-planner', pageTitle: 'Meal Planner' },
  { label: 'Food Diary', route: '/food-diary', pageTitle: 'Food Diary' },
  { label: 'Ingredient Maintenance', route: '/ingredient-maintenance', pageTitle: 'Ingredient Maintenance' },
  { label: 'Archived Recipes', route: '/recipes/archived', pageTitle: 'Archived Recipes' },
];

test.describe('Navigation menu and stub pages', () => {
  test.beforeEach(async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/home');
  });

  test('AC-01 shows the brand, ordered navigation, and Log out in the desktop header', async ({ page }) => {
    const header = page.getByTestId('app-header');
    const navigation = page.getByRole('navigation', { name: 'Main' });
    const logout = page.getByRole('button', { name: 'Log out' });

    await expect(header.getByRole('link', { name: 'Recipe App home' })).toBeVisible();
    await expect(navigation).toBeVisible();
    await expect(logout).toBeVisible();
    expect((await logout.boundingBox())!.x).toBeGreaterThan((await navigation.boundingBox())!.x);
  });

  test('AC-02 displays menu items in the required order', async ({ page }) => {
    const navigation = page.getByRole('navigation', { name: 'Main' });

    await expect(navigation.getByRole('link')).toHaveText(navigationItems.map((item) => item.label));
  });

  test('AC-03 separates Log out from navigation links', async ({ page }) => {
    const navigation = page.getByRole('navigation', { name: 'Main' });
    const logout = page.getByRole('button', { name: 'Log out' });

    await expect(logout).toHaveClass(/app-logout/);
    expect((await logout.boundingBox())!.x).toBeGreaterThan((await navigation.boundingBox())!.x);
  });

  test('AC-04 navigates to each destination and renders its titled page', async ({ page }) => {
    for (const item of navigationItems) {
      await page.getByRole('link', { name: item.label, exact: true }).click();

      await expect(page).toHaveURL(new RegExp(`${item.route}$`));
      await expect(page.getByRole('heading', { name: item.pageTitle, exact: true })).toBeVisible();
      await expect(page).toHaveTitle(`${item.pageTitle} – Recipe App`);
    }
  });

  test('AC-05 gives the menu an accessible navigation landmark', async ({ page }) => {
    await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible();
  });

  test('AC-13 wraps without horizontal overflow at a narrow desktop width', async ({ page }) => {
    await page.setViewportSize({ width: 720, height: 800 });

    const header = page.getByTestId('app-header');
    await expect(header).toBeVisible();
    expect(await header.evaluate((element) => element.getBoundingClientRect().height)).toBeGreaterThan(76);
    expect(await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth)).toBe(false);
  });

  test('AC-31 renders unknown authenticated routes inside the app shell', async ({ page }) => {
    await page.goto('/unknown-route');

    await expect(page.getByTestId('app-header')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Page not found' })).toBeVisible();
  });
});

test('AC-12 matches the login header presentation', async ({ page }) => {
  const api = await mockAuthApi(page);
  await page.goto('/');
  const loginHeader = page.locator('.login-header');
  const loginPresentation = await loginHeader.evaluate((element) => {
    const style = window.getComputedStyle(element);
    return {
      backgroundColor: style.backgroundColor,
      borderBottomColor: style.borderBottomColor,
      borderBottomWidth: style.borderBottomWidth,
      height: style.height,
    };
  });

  api.setSession(true);
  await page.goto('/home');
  const appHeader = page.getByTestId('app-header');
  const appPresentation = await appHeader.evaluate((element) => {
    const style = window.getComputedStyle(element);
    return {
      backgroundColor: style.backgroundColor,
      borderBottomColor: style.borderBottomColor,
      borderBottomWidth: style.borderBottomWidth,
      height: style.height,
    };
  });

  expect(appPresentation).toEqual(loginPresentation);
});
