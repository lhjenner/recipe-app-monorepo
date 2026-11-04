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

test('renders ordered recipe metadata, tags, typed items, and instructions without loading an image', async ({ page }) => {
  const api = await mockRecipeApi(page);
  await page.goto(`/recipes/${recipeId}`);

  await expect(page.getByRole('heading', { name: 'Tofu Bowl', level: 1 })).toBeVisible();
  await expect(page.getByText('Family recipe')).toBeVisible();
  await expect(page.getByText('2-3')).toBeVisible();
  await expect(page.getByText('about 15 minutes')).toBeVisible();
  await expect(page.getByText('quick')).toBeVisible();
  await expect(page.getByText('weeknight')).toBeVisible();

  const step = page.getByRole('listitem').filter({ hasText: 'For the sauce' }).first();
  const stepContent = await step.innerText();
  expect(stepContent.indexOf('For the sauce')).toBeLessThan(stepContent.indexOf('1/2 block Firm Tofu'));
  expect(stepContent.indexOf('1/2 block Firm Tofu')).toBeLessThan(stepContent.indexOf('<script>not executable</script>'));
  expect(stepContent.indexOf('<script>not executable</script>')).toBeLessThan(stepContent.indexOf('Mix gently.'));
  await expect(page.getByText('Mix gently.\nDo not overwork.')).toBeVisible();
  await expect(page.getByText('Serve warm.')).toBeVisible();
  expect(api.imageRequests).toEqual([]);
});

test('title-cases recipe and current Ingredient names while preserving other recipe text', async ({ page }) => {
  await mockRecipeApi(page);
  await page.goto(`/recipes/${recipeId}`);

  await expect(page.getByRole('heading', { name: 'Tofu Bowl' })).toBeVisible();
  await expect(page).toHaveTitle('Tofu Bowl – Recipe App');
  await expect(page.getByText('Firm Tofu', { exact: true })).toBeVisible();
  await expect(page.getByText('2-3')).toBeVisible();
  await expect(page.getByText('1/2')).toBeVisible();
  await expect(page.getByText('Mix gently.\nDo not overwork.')).toBeVisible();
  await expect(page.getByText('Serve warm.')).toBeVisible();
  await expect(page.getByText('<script>not executable</script>')).toBeVisible();
  await expect(page.locator('.recipe-detail-page script')).toHaveCount(0);
  await expect(page.getByRole('button', { name: /edit|delete|archive|scale|duplicate/i })).toHaveCount(0);
  await expect(page.getByRole('link', { name: recipeDetail.sourceUrl! }))
    .toHaveAttribute('href', recipeDetail.sourceUrl!);
  await expect(page.getByRole('link', { name: recipeDetail.sourceUrl! }))
    .toHaveAttribute('rel', 'noopener noreferrer');
});

test('renders only valid absolute HTTP and HTTPS source URLs as external links', async ({ page }) => {
  const details = { [recipeId]: { ...recipeDetail, sourceUrl: 'javascript:alert(document.domain)' } };
  await mockRecipeApi(page, { details });
  await page.goto(`/recipes/${recipeId}`);

  await expect(page.getByText('javascript:alert(document.domain)', { exact: true })).toBeVisible();
  await expect(page.getByText('Link unavailable')).toBeVisible();
  await expect(page.getByRole('link', { name: /javascript:/i })).toHaveCount(0);

  for (const safeUrl of ['http://example.test/source', 'https://example.test/secure-source']) {
    details[recipeId] = { ...recipeDetail, sourceUrl: safeUrl };
    await page.goto(`/recipes/${recipeId}`);
    await expect(page.getByRole('link', { name: safeUrl }))
      .toHaveAttribute('href', safeUrl);
    await expect(page.getByRole('link', { name: safeUrl }))
      .toHaveAttribute('rel', 'noopener noreferrer');
  }

  for (const unsafeUrl of [
    'https://[invalid',
    'data:text/html,unsafe',
    'https://user:password@example.test/private',
    ' https://example.test/space ',
  ]) {
    details[recipeId] = { ...recipeDetail, sourceUrl: unsafeUrl };
    await page.goto(`/recipes/${recipeId}`);
    await expect(page.getByText(unsafeUrl, { exact: true })).toBeVisible();
    await expect(page.getByText('Link unavailable')).toBeVisible();
    await expect(page.getByRole('link', { name: unsafeUrl })).toHaveCount(0);
  }
});

test('shows loading, retryable failures, and a distinct not-found state', async ({ page }) => {
  let releaseDetail: (() => void) | undefined;
  let shouldFail = true;
  await mockRecipeApi(page, {
    detailResponse: async (url) => {
      if (!url.pathname.endsWith(`/${recipeId}`)) {
        return { status: 404, body: { status: 404, title: 'Not Found' } };
      }
      if (shouldFail) {
        return await new Promise((resolve) => {
          releaseDetail = () => resolve({
            status: 503,
            body: { status: 503, title: 'Recipe service unavailable' },
          });
        });
      }
      return { status: 200, body: recipeDetail };
    },
  });
  await page.goto(`/recipes/${recipeId}`);

  await expect(page.getByRole('status')).toHaveText('Loading recipe…');
  releaseDetail?.();
  await expect(page.getByRole('alert')).toContainText('Recipe service unavailable');
  shouldFail = false;
  await page.getByRole('button', { name: 'Retry recipe' }).click();
  await expect(page.getByRole('heading', { name: 'Tofu Bowl' })).toBeVisible();

  await page.goto('/recipes/019a0000-0000-7000-8000-000000000003');
  await expect(page.getByRole('heading', { name: 'Recipe not found' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Back to recipes' })).toHaveAttribute('href', '/recipes');
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
