import { expect, test } from '@playwright/test';
import { mockAuthApi } from '../support/mockAuthApi.ts';
import {
  mockRecipeApi,
  recipeDetail,
  recipeId,
  secondRecipeId,
} from '../support/mockRecipeApi.ts';

test.beforeEach(async ({ page }) => {
  await mockAuthApi(page, { hasSession: true });
});

test('restores search and page from the URL and returns from a GUID detail route', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 800 });
  await mockRecipeApi(page, {
    summaries: [{ id: recipeId, name: 'Tofu bowl' }],
    details: { [recipeId]: recipeDetail },
    listResponse: () => ({
      status: 200,
      body: { items: [{ id: recipeId, name: 'Tofu bowl' }], totalCount: 26, page: 2, pageSize: 25 },
    }),
  });
  await page.goto('/recipes?search=tofu&page=2');

  await expect(page.getByRole('searchbox', { name: 'Search recipes' })).toHaveValue('tofu');
  await expect(page.getByRole('link', { name: /tofu bowl/i })).toHaveAttribute('href', `/recipes/${recipeId}`);
  await page.getByRole('link', { name: /tofu bowl/i }).click();
  await expect(page).toHaveURL(new RegExp(`/recipes/${recipeId}$`));
  await page.getByRole('link', { name: /back to recipes/i }).click();

  await expect(page).toHaveURL('/recipes?search=tofu&page=2');
  await expect(page.getByRole('searchbox', { name: 'Search recipes' })).toHaveValue('tofu');
});

test('debounces changed searches, resets to page one, and ignores a late stale response', async ({ page }) => {
  const api = await mockRecipeApi(page, {
    summaries: [
      { id: recipeId, name: 'Tofu bowl' },
      { id: secondRecipeId, name: 'Pasta bake' },
    ],
    listResponse: async (url) => {
      const search = url.searchParams.get('search') ?? '';
      if (search === 'tofu') {
        await new Promise((resolve) => setTimeout(resolve, 500));
        return {
          status: 200,
          body: { items: [{ id: recipeId, name: 'Tofu bowl' }], totalCount: 1, page: 1, pageSize: 25 },
        };
      }
      if (search === 'pasta') {
        return {
          status: 200,
          body: { items: [{ id: secondRecipeId, name: 'Pasta bake' }], totalCount: 1, page: 1, pageSize: 25 },
        };
      }
      return {
        status: 200,
        body: { items: [], totalCount: 0, page: 1, pageSize: 25 },
      };
    },
  });
  await page.goto('/recipes?page=2');
  const search = page.getByRole('searchbox', { name: 'Search recipes' });
  await search.fill('tofu');
  await expect.poll(() => api.listRequests.some((query) => query.includes('search=tofu'))).toBe(true);
  await search.fill('pasta');

  await expect(page).toHaveURL(/\/recipes\?search=pasta$/);
  await expect(page.getByRole('link', { name: 'Pasta bake' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Tofu bowl' })).toBeHidden();
});

test('shows distinct empty, no-match, invalid-response, and retryable error states', async ({ page }) => {
  let shouldFail = true;
  await mockRecipeApi(page, {
    listResponse: (url) => {
      if (url.searchParams.get('search')) {
        return { status: 200, body: { items: [], totalCount: 0, page: 1, pageSize: 25 } };
      }
      if (shouldFail) {
        return { status: 503, body: { status: 503, title: 'Service unavailable' } };
      }
      return { status: 200, body: { items: [], totalCount: 0, page: 1, pageSize: 25 } };
    },
  });
  await page.goto('/recipes');
  await expect(page.getByRole('button', { name: /retry/i })).toBeVisible();
  shouldFail = false;
  await page.getByRole('button', { name: /retry/i }).click();
  await expect(page.getByText('No recipes yet')).toBeVisible();

  await page.getByRole('searchbox', { name: 'Search recipes' }).fill('absent');
  await expect(page.getByText(/no (recipes|results).*match/i)).toBeVisible();
});

test('rejects malformed GUID summary data instead of treating it as an empty list', async ({ page }) => {
  await mockRecipeApi(page, {
    listResponse: () => ({
      status: 200,
      body: { items: [{ id: 'legacy-id', name: 'Invalid' }], totalCount: 1, page: 1, pageSize: 25 },
    }),
  });
  await page.goto('/recipes');

  await expect(page.getByText('No recipes yet')).toBeHidden();
  await expect(page.getByRole('button', { name: /retry/i })).toBeVisible();
});

test('keeps the recipe list keyboard-accessible and free of horizontal overflow on mobile and desktop', async ({ page }) => {
  await mockRecipeApi(page);

  for (const width of [375, 1280]) {
    await page.setViewportSize({ width, height: 800 });
    await page.goto('/recipes');
    const recipeLink = page.getByRole('link', { name: /tofu bowl/i });
    await expect(recipeLink).toBeVisible();
    await page.getByRole('searchbox', { name: 'Search recipes' }).focus();
    await page.keyboard.press('Tab');
    await expect(recipeLink).toBeFocused();
    expect(await page.evaluate(() =>
      document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
  }
});
