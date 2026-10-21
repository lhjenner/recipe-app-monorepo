import { expect, test, type Page } from '@playwright/test';
import { loginRejectsCredentials, mockAuthApi, testUser, validCredentials } from '../support/mockAuthApi.ts';

async function logIn(page: Page) {
  await page.getByLabel('Email').fill(validCredentials.email);
  await page.getByLabel('Password').fill(validCredentials.password);
  await page.getByRole('button', { name: 'Log in' }).click();
}

test.describe('Route protection (PRD 02)', () => {
  test('AC-25 remembers a deep link, including its query and fragment, across a reload and failed login', async ({ page }) => {
    const api = await mockAuthApi(page);
    const destination = '/grocery-list?view=week#shopping';
    await page.goto(destination);

    await expect(page).toHaveURL(`/?${new URLSearchParams({ returnTo: destination })}`);
    await page.reload();
    api.setLoginHandler(loginRejectsCredentials);
    await logIn(page);
    await expect(page.getByRole('alert')).toHaveText('Invalid email or password');
    await expect(page).toHaveURL(`/?${new URLSearchParams({ returnTo: destination })}`);

    api.setLoginHandler(async (route) => {
      await route.fulfill({ json: testUser });
      return true;
    });
    await logIn(page);

    await expect(page).toHaveURL(destination);
    await expect(page.getByRole('heading', { name: 'Grocery List', exact: true })).toBeVisible();
  });

  test('AC-26 a protected API 401 clears the session and returns to the current page after login', async ({ page }) => {
    const api = await mockAuthApi(page, { hasSession: true });
    const destination = '/grocery-list?view=week#shopping';
    await page.goto('/home');
    await page.getByRole('link', { name: 'Grocery List', exact: true }).click();
    await page.goto(destination);
    await expect(page.getByTestId('app-header')).toBeVisible();
    api.setSession(false);
    await page.route('**/api/ingredients', (route) => route.fulfill({ status: 401 }));

    const status = await page.evaluate(async () => {
      const modulePath = '/src/services/apiClient.ts';
      const { apiFetch }: typeof import('../../../src/services/apiClient.js') = await import(modulePath);
      return (await apiFetch('/api/ingredients')).status;
    });

    expect(status).toBe(401);
    await expect(page).toHaveURL(`/?${new URLSearchParams({ returnTo: destination })}`);
    await expect(page.getByTestId('app-header')).toHaveCount(0);
    await expect(page.getByRole('status')).toContainText('Your session has expired. Please log in again.');
    await logIn(page);
    await expect(page).toHaveURL(destination);
    await expect(page.getByTestId('app-header')).toBeVisible();
  });

  test('AC-26 logout returning 401 redirects to login rather than offering a retry on a protected page', async ({ page }) => {
    const api = await mockAuthApi(page, { hasSession: true });
    await page.goto('/grocery-list');
    api.setLogoutHandler(async (route) => {
      await route.fulfill({ status: 401 });
      return true;
    });

    await page.getByRole('button', { name: 'Log out' }).click();

    await expect(page).toHaveURL('/?returnTo=%2Fgrocery-list');
    await expect(page.getByTestId('app-header')).toHaveCount(0);
    await logIn(page);
    await expect(page).toHaveURL('/grocery-list');
  });

  for (const destination of ['https://evil.example', '//evil.example', '/\\evil.example', '/recipes/../..//evil.example']) {
    test(`AC-27 ignores the unsafe returnTo ${destination}`, async ({ page }) => {
      await mockAuthApi(page);
      await page.goto(`/?${new URLSearchParams({ returnTo: destination })}`);

      await logIn(page);

      await expect(page).toHaveURL('/home');
      await expect(page.getByText('Widgets coming soon')).toBeVisible();
    });
  }

  test('AC-28 an existing session on login goes to Home, ignoring returnTo', async ({ page }) => {
    await mockAuthApi(page, { hasSession: true });
    await page.goto('/?returnTo=%2Fgrocery-list');

    await expect(page).toHaveURL('/home');
    await expect(page.getByRole('button', { name: 'Log out' })).toBeVisible();
  });

  test('explicit logout discards an earlier intended destination and the next login goes Home', async ({ page }) => {
    await mockAuthApi(page);
    await page.goto('/grocery-list');
    await logIn(page);
    await expect(page).toHaveURL('/grocery-list');

    await page.getByRole('button', { name: 'Log out' }).click();
    await expect(page).toHaveURL('/');
    await logIn(page);

    await expect(page).toHaveURL('/home');
  });

  test('startup API failure keeps protected content hidden and allows login to the intended page', async ({ page }) => {
    await mockAuthApi(page);
    await page.route('**/api/auth/me', (route) => route.fulfill({ status: 503 }));
    await page.goto('/grocery-list');

    await expect(page).toHaveURL('/?returnTo=%2Fgrocery-list');
    await expect(page.getByRole('status')).toHaveText('We could not check your current session. You can still log in.');
    await expect(page.getByTestId('app-header')).toHaveCount(0);
    await logIn(page);

    await expect(page).toHaveURL('/grocery-list');
    await expect(page.getByTestId('app-header')).toBeVisible();
  });

  for (const hasSession of [true, false]) {
    for (const path of ['/', '/grocery-list']) {
      test(`AC-29 only loading is shown at ${path} while checking session (active=${hasSession})`, async ({ page }) => {
        await mockAuthApi(page, { hasSession });
        let releaseSession!: () => void;
        const gate = new Promise<void>((resolve) => { releaseSession = resolve; });
        await page.route('**/api/auth/me', async (route) => {
          await gate;
          await route.fallback();
        });

        try {
          await page.goto(path);
          await expect(page.getByRole('status')).toHaveText('Checking your session…');
          await expect(page.getByRole('heading', { name: 'Log in' })).toHaveCount(0);
          await expect(page.getByTestId('app-header')).toHaveCount(0);
          await expect(page.getByRole('heading', { name: 'Grocery List', exact: true })).toHaveCount(0);
        } finally {
          releaseSession();
        }

        if (hasSession) {
          await expect(page.getByTestId('app-header')).toBeVisible();
        } else {
          await expect(page.getByRole('heading', { name: 'Log in' })).toBeVisible();
        }
      });
    }
  }

  for (const path of ['/', '/register', '/forgot-password', '/reset-password']) {
    test(`AC-30 no app shell on the auth page ${path}`, async ({ page }) => {
      await mockAuthApi(page);
      await page.goto(path);

      await expect(page.getByRole('main')).toBeVisible();
      await expect(page.getByTestId('app-header')).toHaveCount(0);
      await expect(page.getByRole('navigation', { name: 'Main', exact: true })).toHaveCount(0);
      await expect(page.getByRole('button', { name: 'Log out' })).toHaveCount(0);
    });
  }

  test('AC-32 an unknown URL is protected and returns to the shell 404 after login', async ({ page }) => {
    await mockAuthApi(page);
    await page.goto('/does-not-exist');

    await expect(page).toHaveURL('/?returnTo=%2Fdoes-not-exist');
    await expect(page.getByTestId('app-header')).toHaveCount(0);
    await logIn(page);

    await expect(page).toHaveURL('/does-not-exist');
    await expect(page.getByRole('heading', { name: 'Page not found' })).toBeVisible();
    await expect(page.getByTestId('app-header')).toBeVisible();
  });
});
