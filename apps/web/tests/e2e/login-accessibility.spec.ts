import { expect, test } from '@playwright/test';
import { mockAuthApi } from './support/mockAuthApi.ts';

test.describe('Login accessibility', () => {
  test.beforeEach(async ({ page }) => {
    await mockAuthApi(page);
    await page.goto('/');
  });

  test('AC-09 every input has an associated label', async ({ page }) => {
    await expect(page.getByRole('textbox', { name: 'Email' })).toBeVisible();
    await expect(page.getByLabel('Password')).toHaveAttribute('type', 'password');
  });

  test('AC-09 tab order follows the visual order with a visible focus ring', async ({ page }) => {
    const inOrder = [
      page.getByRole('link', { name: 'Recipe App home' }),
      page.getByLabel('Email'),
      page.getByLabel('Password'),
      page.getByRole('button', { name: 'Log in' }),
      page.getByRole('link', { name: 'Create account' }),
      page.getByRole('link', { name: 'Forgot password' }),
    ];

    for (const element of inOrder) {
      await page.keyboard.press('Tab');
      await expect(element).toBeFocused();
      await expect(element).not.toHaveCSS('outline-style', 'none');
      await expect(element).not.toHaveCSS('outline-width', '0px');
    }
  });

  test('AC-09 the form can be submitted with the keyboard alone', async ({ page }) => {
    await page.getByLabel('Email').focus();
    await page.keyboard.type('person@example.com');
    await page.keyboard.press('Tab');
    await page.keyboard.type('password123');
    await page.keyboard.press('Enter');

    await expect(page).toHaveURL(/\/home$/);
  });
});
