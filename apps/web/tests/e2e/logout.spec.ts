import { expect, test } from '@playwright/test';
import { mockAuthApi, type LogoutHandler } from './support/mockAuthApi.ts';

const logoutFails: LogoutHandler = async (route) => {
  await route.fulfill({ status: 503 });
  return false;
};

test.describe('Logout', () => {
  test('AC-22 ends the session immediately without confirmation', async ({ page }) => {
    const api = await mockAuthApi(page, { hasSession: true });
    let confirmationShown = false;
    page.on('dialog', async (dialog) => {
      confirmationShown = true;
      await dialog.dismiss();
    });

    await page.goto('/home');
    await page.getByRole('button', { name: 'Log out' }).click();

    await expect(page.getByRole('heading', { name: 'Log in' })).toBeVisible();
    expect(api.logoutRequestCount).toBe(1);
    expect(confirmationShown).toBe(false);
  });

  test('AC-23 Back does not restore protected content after logout', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await page.goto('/home');
    await page.getByRole('link', { name: 'Grocery List', exact: true }).click();
    await expect(page).toHaveURL(/\/grocery-list$/);

    await page.getByRole('button', { name: 'Log out' }).click();
    await expect(page.getByRole('heading', { name: 'Log in' })).toBeVisible();

    await page.goBack();

    await expect(page.getByRole('heading', { name: 'Log in' })).toBeVisible();
    await expect(page.getByTestId('app-header')).toHaveCount(0);
  });

  test('AC-23 rechecks the session after a persisted pageshow restore', async ({ page }) => {
    const api = await mockAuthApi(page, { hasSession: true });
    await page.goto('/home');
    await expect(page.getByTestId('app-header')).toBeVisible();

    api.setSession(false);
    await page.evaluate(() => window.dispatchEvent(new PageTransitionEvent('pageshow', { persisted: true })));

    await expect(page.getByRole('heading', { name: 'Log in' })).toBeVisible();
    await expect(page.getByTestId('app-header')).toHaveCount(0);
  });

  test('AC-24 displays a retryable error when logout fails', async ({ page }) => {
    const api = await mockAuthApi(page, { hasSession: true });
    await page.goto('/grocery-list');
    api.setLogoutHandler(logoutFails);

    await page.getByRole('button', { name: 'Log out' }).click();

    await expect(page).toHaveURL(/\/grocery-list$/);
    await expect(page.getByRole('alert')).toHaveText('The authentication service is unavailable. Please try again.');
    expect(api.logoutRequestCount).toBe(1);

    api.setLogoutHandler(async (route) => {
      await route.fulfill({ status: 204 });
      return true;
    });
    await page.getByRole('button', { name: 'Log out' }).click();

    await expect(page.getByRole('heading', { name: 'Log in' })).toBeVisible();
    expect(api.logoutRequestCount).toBe(2);
  });
});
