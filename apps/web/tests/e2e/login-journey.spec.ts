import { expect, test } from '@playwright/test';
import { loginRejectsCredentials, mockAuthApi, testUser, validCredentials, type AuthApiMock } from './support/mockAuthApi.ts';

async function submitLogin(page: import('@playwright/test').Page, email: string, password: string) {
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Log in' }).click();
}

test.describe('Login journey', () => {
  let api: AuthApiMock;

  test.beforeEach(async ({ page }) => {
    api = await mockAuthApi(page);
    await page.goto('/');
  });

  test('AC-04 valid credentials sign the user in and route to the landing page', async ({ page }) => {
    await submitLogin(page, validCredentials.email, validCredentials.password);

    await expect(page).toHaveURL(/\/home$/);
    await expect(page.getByRole('heading', { name: /You're signed in/ })).toBeVisible();
    await expect(page.getByText(`Signed in as ${testUser.email}`)).toBeVisible();
    expect(api.loginRequests).toEqual([validCredentials]);
  });

  test('AC-05 wrong credentials show one generic error and stay on the login page', async ({ page }) => {
    api.setLoginHandler(loginRejectsCredentials);

    await submitLogin(page, 'unknown@example.com', 'password123');
    const alert = page.getByRole('alert');
    await expect(alert).toHaveText('Invalid email or password');

    await submitLogin(page, validCredentials.email, 'wrong-password');
    await expect(alert).toHaveText('Invalid email or password');

    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByLabel('Email')).toHaveValue(validCredentials.email);
  });

  test('shows a service unavailable error when the API cannot be reached', async ({ page }) => {
    api.setLoginHandler(async (route) => {
      await route.abort('failed');
      return false;
    });

    await submitLogin(page, validCredentials.email, validCredentials.password);

    await expect(page.getByRole('alert')).toHaveText(
      'The authentication service is unavailable. Please try again.',
    );
    await expect(page).toHaveURL(/\/$/);
  });

  test('shows service unavailable, not a credentials error, for a bodiless 502', async ({ page }) => {
    api.setLoginHandler(async (route) => {
      await route.fulfill({ status: 502 });
      return false;
    });

    await submitLogin(page, validCredentials.email, validCredentials.password);

    await expect(page.getByRole('alert')).toHaveText(
      'The authentication service is unavailable. Please try again.',
    );
  });

  test('AC-07 in-flight login disables the button, shows progress, and keeps layout stable', async ({ page }) => {
    let releaseLogin!: () => void;
    const gate = new Promise<void>((resolve) => {
      releaseLogin = resolve;
    });
    api.setLoginHandler(async (route) => {
      await gate;
      await route.fulfill({ status: 401, contentType: 'application/problem+json', body: JSON.stringify({ status: 401, title: 'Invalid email or password' }) });
      return false;
    });

    const button = page.getByRole('button', { name: 'Log in' });
    const idleBox = await button.boundingBox();
    await page.getByLabel('Email').fill(validCredentials.email);
    await page.getByLabel('Password').fill(validCredentials.password);
    await button.click();

    const busyButton = page.getByRole('button', { name: 'Logging in…' });
    await expect(busyButton).toBeDisabled();
    const busyBox = await busyButton.boundingBox();
    expect(busyBox?.width).toBeCloseTo(idleBox!.width, 0);
    expect(busyBox?.height).toBeCloseTo(idleBox!.height, 0);

    await expect(page.getByRole('link', { name: 'Create account' })).toBeEnabled();
    await expect(page.getByRole('link', { name: 'Forgot password' })).toBeEnabled();

    await page.getByLabel('Password').press('Enter');
    expect(api.loginRequests).toHaveLength(1);

    releaseLogin();
    await expect(page.getByRole('button', { name: 'Log in' })).toBeEnabled();
  });
});
