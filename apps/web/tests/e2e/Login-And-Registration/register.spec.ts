import { expect, test } from '@playwright/test';

type RegistrationPayload = {
  email: string;
  password: string;
};

type MockResponse = {
  status: number;
  body: object;
};

test.describe('Registration page', () => {
  let registrationRequestCount: number;
  let registrationPayload: RegistrationPayload | undefined;
  let mockResponse: MockResponse | undefined;

  test.beforeEach(async ({ page }) => {
    registrationRequestCount = 0;
    registrationPayload = undefined;
    mockResponse = undefined;

    await page.route('**/api/auth/register', async (route) => {
      registrationRequestCount += 1;
      registrationPayload = route.request().postDataJSON() as RegistrationPayload;

      if (mockResponse) {
        await route.fulfill({
          status: mockResponse.status,
          contentType: 'application/json',
          body: JSON.stringify(mockResponse.body),
        });
        return;
      }

      await route.continue();
    });

    await page.goto('/register');
  });

  test('shows registration fields, submit button, and a link back to login', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Create account' })).toBeVisible();
    await expect(page.getByLabel('Email')).toBeVisible();
    await expect(page.getByLabel('Password')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Create account' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Log in' })).toBeVisible();
  });

  test('shows an inline error for an empty email without sending a request', async ({ page }) => {
    await page.getByLabel('Password').fill('password123');
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page.getByLabel('Email')).toHaveAttribute('aria-invalid', 'true');
    await expect(page.getByText('Email is required')).toBeVisible();
    expect(registrationRequestCount).toBe(0);
  });

  test('shows an inline error for an empty password without sending a request', async ({ page }) => {
    await page.getByLabel('Email').fill('person@example.com');
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page.getByLabel('Password')).toHaveAttribute('aria-invalid', 'true');
    await expect(page.getByText('Password is required')).toBeVisible();
    expect(registrationRequestCount).toBe(0);
  });

  test('shows an inline error for a malformed email without sending a request', async ({ page }) => {
    await page.getByLabel('Email').fill('not-an-email');
    await page.getByLabel('Password').fill('password123');
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page.getByLabel('Email')).toHaveAttribute('aria-invalid', 'true');
    await expect(page.getByText('Enter a valid email address')).toBeVisible();
    expect(registrationRequestCount).toBe(0);
  });

  test('posts valid account details and shows success', async ({ page }) => {
    mockResponse = {
      status: 201,
      body: {
        id: 'a88f9e5c-7e9b-4f06-9f6d-ea14f30f90a1',
        email: 'new@example.com',
        createdAtUtc: '2026-10-07T00:00:00Z',
      },
    };

    await page.getByLabel('Email').fill('new@example.com');
    await page.getByLabel('Password').fill('password123');
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page.getByRole('status')).toHaveText('Account created. You can now log in.');
    expect(registrationRequestCount).toBe(1);
    expect(registrationPayload).toEqual({ email: 'new@example.com', password: 'password123' });
  });

  test('shows the API problem message when the email is already registered', async ({ page }) => {
    mockResponse = {
      status: 409,
      body: {
        type: 'about:blank',
        title: 'Email already registered',
        status: 409,
        detail: 'A user with this email address already exists.',
      },
    };

    await page.getByLabel('Email').fill('existing@example.com');
    await page.getByLabel('Password').fill('password123');
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page.getByRole('alert')).toHaveText('Email already registered');
    expect(registrationRequestCount).toBe(1);
  });
});
