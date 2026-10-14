import { expect, test, type Page } from '@playwright/test';
import { mockAuthApi } from '../support/mockAuthApi.ts';

const resetProblem = { status: 400, title: 'Invalid or expired reset token' };

async function mockForgot(page: Page, status: number) {
  const requests: { email: string }[] = [];
  await page.route('**/api/auth/forgot-password', async (route) => {
    requests.push(route.request().postDataJSON() as { email: string });
    await route.fulfill(status === 200 ? { status } : { status, contentType: 'application/problem+json', body: JSON.stringify({ status }) });
  });
  return requests;
}

test.describe('forgot password', () => {
  test('is reachable from the login page', async ({ page }) => {
    await mockAuthApi(page);
    await page.goto('/');
    await page.getByRole('link', { name: /forgot/i }).click();

    await expect(page).toHaveURL(/\/forgot-password$/);
    await expect(page.getByRole('heading', { name: 'Forgot your password?' })).toBeVisible();
  });

  test('shows a generic confirmation after submitting', async ({ page }) => {
    await mockAuthApi(page);
    const requests = await mockForgot(page, 200);
    await page.goto('/forgot-password');

    await page.getByLabel('Email').fill('anyone@example.com');
    await page.getByRole('button', { name: 'Send reset link' }).click();

    await expect(page.getByRole('status')).toHaveText("If that email is registered, we've sent a link to reset your password.");
    expect(requests).toEqual([{ email: 'anyone@example.com' }]);
  });

  test('validates the email client-side without calling the API', async ({ page }) => {
    await mockAuthApi(page);
    const requests = await mockForgot(page, 200);
    await page.goto('/forgot-password');

    await page.getByRole('button', { name: 'Send reset link' }).click();
    await expect(page.getByText('Email is required')).toBeVisible();

    await page.getByLabel('Email').fill('not-an-email');
    await page.getByRole('button', { name: 'Send reset link' }).click();
    await expect(page.getByText('Enter a valid email address')).toBeVisible();
    expect(requests).toHaveLength(0);
  });

  test('reports service unavailable', async ({ page }) => {
    await mockAuthApi(page);
    await mockForgot(page, 503);
    await page.goto('/forgot-password');

    await page.getByLabel('Email').fill('anyone@example.com');
    await page.getByRole('button', { name: 'Send reset link' }).click();

    await expect(page.getByRole('alert')).toContainText('unavailable');
  });
});

test.describe('reset password', () => {
  test('resets the password and links back to login', async ({ page }) => {
    await mockAuthApi(page);
    let body: { token: string; newPassword: string } | undefined;
    await page.route('**/api/auth/reset-password', async (route) => {
      body = route.request().postDataJSON() as { token: string; newPassword: string };
      await route.fulfill({ status: 204 });
    });
    await page.goto('/reset-password?token=abc123');

    await page.getByLabel('New password').fill('brand-new-pass');
    await page.getByRole('button', { name: 'Reset password' }).click();

    await expect(page.getByRole('status')).toContainText('Your password has been reset.');
    expect(body).toEqual({ token: 'abc123', newPassword: 'brand-new-pass' });

    await page.getByRole('link', { name: 'Log in' }).click();
    await expect(page).toHaveURL(/\/$/);
  });

  test('requires a new password', async ({ page }) => {
    await mockAuthApi(page);
    await page.goto('/reset-password?token=abc123');

    await page.getByRole('button', { name: 'Reset password' }).click();

    await expect(page.getByText('New password is required')).toBeVisible();
  });

  test('shows an error and a new-link option for an invalid or expired token', async ({ page }) => {
    await mockAuthApi(page);
    await page.route('**/api/auth/reset-password', (route) =>
      route.fulfill({ status: 400, contentType: 'application/problem+json', body: JSON.stringify(resetProblem) }));
    await page.goto('/reset-password?token=expired');

    await page.getByLabel('New password').fill('brand-new-pass');
    await page.getByRole('button', { name: 'Reset password' }).click();

    await expect(page.getByRole('alert')).toContainText('Invalid or expired reset token');
    await page.getByRole('link', { name: 'Request a new link' }).click();
    await expect(page).toHaveURL(/\/forgot-password$/);
  });

  test('explains a link with no token', async ({ page }) => {
    await mockAuthApi(page);
    await page.goto('/reset-password');

    await expect(page.getByRole('alert')).toContainText('This reset link is invalid.');
    await expect(page.getByLabel('New password')).toHaveCount(0);
  });
});
