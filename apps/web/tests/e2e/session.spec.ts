import { expect, test } from '@playwright/test';
import { mockAuthApi, validCredentials } from './support/mockAuthApi.ts';

test.describe('Session handling', () => {
  test('AC-12 a valid session routes straight to Home', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });

    await page.goto('/');

    await expect(page).toHaveURL(/\/home$/);
    await expect(page.getByRole('button', { name: 'Log out' })).toBeVisible();
  });

  test('AC-12 no session routes to the login page', async ({ page }) => {
    await mockAuthApi(page);

    await page.goto('/');

    await expect(page.getByRole('heading', { name: 'Log in' })).toBeVisible();
  });

  test('AC-12 the session is restored after a reload', async ({ page }) => {
    await mockAuthApi(page);
    await page.goto('/');
    await page.getByLabel('Email').fill(validCredentials.email);
    await page.getByLabel('Password').fill(validCredentials.password);
    await page.getByRole('button', { name: 'Log in' }).click();
    await expect(page).toHaveURL(/\/home$/);

    await page.reload();

    await expect(page).toHaveURL(/\/home$/);
    await expect(page.getByRole('button', { name: 'Log out' })).toBeVisible();
  });

  test('AC-13 a protected page redirects to login when the session is invalid', async ({ page }) => {
    await mockAuthApi(page);

    await page.goto('/home');

    await expect(page).toHaveURL('/?returnTo=%2Fhome');
    await expect(page.getByRole('heading', { name: 'Log in' })).toBeVisible();
  });

  test('AC-13 an expired session redirects to login on the next load', async ({ page }) => {
    const api = await mockAuthApi(page, { hasSession: true });
    await page.goto('/home');
    await expect(page.getByRole('button', { name: 'Log out' })).toBeVisible();

    api.setSession(false);
    await page.reload();

    await expect(page).toHaveURL('/?returnTo=%2Fhome');
    await expect(page.getByRole('heading', { name: 'Log in' })).toBeVisible();
  });

  test('AC-14 logging out ends the session and returns to the login page', async ({ page }) => {
    const api = await mockAuthApi(page, { hasSession: true });
    await page.goto('/home');

    await page.getByRole('button', { name: 'Log out' }).click();

    await expect(page.getByRole('heading', { name: 'Log in' })).toBeVisible();
    expect(api.logoutRequestCount).toBe(1);

    await page.goto('/home');
    await expect(page).toHaveURL('/?returnTo=%2Fhome');
  });
});
