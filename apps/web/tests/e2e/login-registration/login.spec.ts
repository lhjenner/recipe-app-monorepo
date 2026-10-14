import { expect, test } from '@playwright/test';

test.describe('Login page', () => {
  let loginRequestCount: number;

  test.beforeEach(async ({ page }) => {
    loginRequestCount = 0;
    await page.route('**/api/auth/login', async (route) => {
      loginRequestCount += 1;
      await route.continue();
    });
    await page.goto('/');
  });

  test('AC-01 shows login fields, submit button, and account links', async ({ page }) => {
    await expect(page.getByLabel('Email')).toBeVisible();
    await expect(page.getByLabel('Password')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Log in' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Create account' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Forgot password' })).toBeVisible();
  });

  test('AC-02 shows an inline error for an empty email without sending a request', async ({ page }) => {
    await page.getByLabel('Password').fill('password123');
    await page.getByRole('button', { name: 'Log in' }).click();

    await expect(page.getByLabel('Email')).toHaveAttribute('aria-invalid', 'true');
    await expect(page.getByText('Email is required')).toBeVisible();
    expect(loginRequestCount).toBe(0);
  });

  test('AC-02 shows an inline error for an empty password without sending a request', async ({ page }) => {
    await page.getByLabel('Email').fill('person@example.com');
    await page.getByRole('button', { name: 'Log in' }).click();

    await expect(page.getByLabel('Password')).toHaveAttribute('aria-invalid', 'true');
    await expect(page.getByText('Password is required')).toBeVisible();
    expect(loginRequestCount).toBe(0);
  });

  test('AC-03 shows an inline error for a malformed email without sending a request', async ({ page }) => {
    await page.getByLabel('Email').fill('not-an-email');
    await page.getByLabel('Password').fill('password123');
    await page.getByRole('button', { name: 'Log in' }).click();

    await expect(page.getByLabel('Email')).toHaveAttribute('aria-invalid', 'true');
    await expect(page.getByText('Enter a valid email address')).toBeVisible();
    expect(loginRequestCount).toBe(0);
  });
});
