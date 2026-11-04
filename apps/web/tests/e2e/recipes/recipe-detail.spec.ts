import { expect, test } from '@playwright/test';
import { mockAuthApi } from '../support/mockAuthApi.ts';
import {
  mockRecipeApi,
  recipeDetail,
  recipeId,
  syntheticPngBytes,
} from '../support/mockRecipeApi.ts';

test.beforeEach(async ({ page }) => {
  await mockAuthApi(page, { hasSession: true });
});

test('renders GUID detail content in order as inert text and exposes no write controls', async ({ page }) => {
  await mockRecipeApi(page);
  await page.goto(`/recipes/${recipeId}`);

  await expect(page.getByRole('heading', { name: 'Tofu Bowl' })).toBeVisible();
  await expect(page).toHaveTitle('Tofu Bowl – Recipe App');
  await expect(page.getByText('2-3')).toBeVisible();
  await expect(page.getByText('1/2')).toBeVisible();
  await expect(page.getByText('Mix gently.\nDo not overwork.')).toBeVisible();
  await expect(page.getByText('Serve warm.')).toBeVisible();
  await expect(page.getByText('<script>not executable</script>')).toBeVisible();
  await expect(page.locator('script')).toHaveCount(0);
  await expect(page.getByRole('button', { name: /edit|delete|archive|scale|duplicate/i })).toHaveCount(0);
  await expect(page.getByRole('link', { name: /source/i })).toHaveAttribute('href', recipeDetail.sourceUrl!);
});

test('uses the authenticated image endpoint and shows readable recipe text when image delivery fails', async ({ page }) => {
  let failImage = true;
  const api = await mockRecipeApi(page, {
    imageResponse: async (route) => {
      await route.fulfill(failImage
        ? { status: 503, contentType: 'application/problem+json', json: { status: 503, title: 'Unavailable' } }
        : { status: 200, contentType: 'image/png', body: syntheticPngBytes });
    },
  });
  await page.goto(`/recipes/${recipeId}`);

  await expect(page.getByRole('heading', { name: 'Tofu Bowl' })).toBeVisible();
  await expect(page.getByText(/image.*(unavailable|could not load)/i)).toBeVisible();
  await expect(page.getByText('Mix gently.\nDo not overwork.')).toBeVisible();
  expect(api.imageRequests).toEqual([`/api/recipes/${recipeId}/image`]);

  failImage = false;
  await page.getByRole('button', { name: /retry image/i }).click();
  await expect(page.getByRole('img', { name: /tofu bowl/i })).toBeVisible();
  expect(api.imageRequests).toHaveLength(2);
});

test('treats a missing image as normal and preserves no-image detail content', async ({ page }) => {
  await mockRecipeApi(page, {
    details: { [recipeId]: { ...recipeDetail, image: null } },
  });
  await page.goto(`/recipes/${recipeId}`);

  await expect(page.getByRole('heading', { name: 'Tofu Bowl' })).toBeVisible();
  await expect(page.getByRole('img')).toHaveCount(0);
  await expect(page.getByText('Serve warm.')).toBeVisible();
});

test('rejects legacy identifiers and malformed detail URLs without capturing reserved recipe routes', async ({ page }) => {
  await mockRecipeApi(page);
  await page.goto('/recipes/legacy-recipe-id');
  await expect(page.getByRole('heading', { name: /not found/i })).toBeVisible();

  await page.goto('/recipes/not-a-guid');
  await expect(page.getByRole('heading', { name: /not found/i })).toBeVisible();

  await page.goto('/recipes/new');
  await expect(page.getByRole('heading', { name: 'Add Recipe' })).toBeVisible();
  await page.goto('/recipes/archived');
  await expect(page.getByRole('heading', { name: 'Archived Recipes' })).toBeVisible();
});

test('renders detail content without horizontal overflow at mobile and desktop widths', async ({ page }) => {
  await mockRecipeApi(page);

  for (const width of [375, 1280]) {
    await page.setViewportSize({ width, height: 800 });
    await page.goto(`/recipes/${recipeId}`);
    await expect(page.getByRole('heading', { name: 'Tofu Bowl' })).toBeVisible();
    expect(await page.evaluate(() =>
      document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
  }
});
