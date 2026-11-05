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

test('renders ordered recipe metadata, tags, typed items, instructions, and its private image', async ({ page }) => {
  const api = await mockRecipeApi(page);
  await page.goto(`/recipes/${recipeId}`);

  await expect(page.getByRole('heading', { name: 'Tofu Bowl', level: 1 })).toBeVisible();
  await expect(page.getByText('Family recipe')).toBeVisible();
  await expect(page.getByText('2-3')).toBeVisible();
  await expect(page.getByText('about 15 minutes')).toBeVisible();
  await expect(page.getByText('quick')).toBeVisible();
  await expect(page.getByText('weeknight')).toBeVisible();
  await expect(page.locator('.recipe-steps')).toHaveCSS('list-style-type', 'none');
  await expect(page.getByRole('heading', { name: 'Step 1', exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Step 2', exact: true })).toBeVisible();
  await expect(page.locator('#recipe-detail-heading + .recipe-source')
    .getByRole('link', { name: 'View original recipe source' })).toBeVisible();
  await expect(page.locator('.recipe-metadata')).not.toContainText('Source URL');

  const step = page.getByRole('listitem').filter({ hasText: 'For the sauce' }).first();
  await expect(step.getByRole('heading', { name: 'For the sauce', level: 3 })).toBeVisible();
  await expect(step.locator('.recipe-step-note')).toHaveText('<script>not executable</script>');
  const stepContent = await step.innerText();
  expect(stepContent.indexOf('For the sauce')).toBeLessThan(stepContent.indexOf('1/2 block Firm Tofu'));
  expect(stepContent.indexOf('1/2 block Firm Tofu')).toBeLessThan(stepContent.indexOf('<script>not executable</script>'));
  expect(stepContent.indexOf('<script>not executable</script>')).toBeLessThan(stepContent.indexOf('Mix gently.'));
  await expect(page.getByText('Mix gently.\nDo not overwork.')).toBeVisible();
  await expect(page.getByText('Serve warm.')).toBeVisible();
  await expect(page.getByRole('img', { name: 'Tofu Bowl recipe' })).toBeVisible();
  expect(api.imageRequests.every((path) => path === `/api/recipes/${recipeId}/image`)).toBe(true);
});

test('shows the back link only on mobile and gives tags a primary-colour border', async ({ page }) => {
  await mockRecipeApi(page);
  await page.goto(`/recipes/${recipeId}`);
  const backLink = page.locator('.recipe-back-link');
  for (const width of [375, 700, 701, 1280]) {
    await page.setViewportSize({ width, height: 800 });
    if (width <= 700) {
      await expect(backLink).toBeVisible();
    } else {
      await expect(backLink).toBeHidden();
    }
  }
  const badge = page.getByRole('list', { name: 'Recipe tags' }).getByRole('listitem').first();
  await expect(badge).toHaveCSS('border-top-width', '1px');
  await expect(badge).toHaveCSS('border-top-style', 'solid');
  await expect(badge).toHaveCSS('border-top-color', 'rgb(74, 124, 89)');
  await expect(badge).toHaveCSS('color', 'rgb(30, 30, 30)');
  await expect(badge).toHaveCSS('background-color', 'rgb(238, 241, 237)');
});

for (const width of [375, 1280]) {
  test(`returns to Recipe List from the header after scrolling at ${width}px`, async ({ page }) => {
    await mockRecipeApi(page, {
      details: {
        [recipeId]: {
          ...recipeDetail,
          steps: Array.from({ length: 20 }, (_, position) => ({
            ...recipeDetail.steps[0]!,
            position,
          })),
        },
      },
    });
    await page.setViewportSize({ width, height: 800 });
    await page.goto(`/recipes/${recipeId}`);
    await page.getByRole('heading', { name: 'Step 20', exact: true }).scrollIntoViewIfNeeded();
    expect(await page.evaluate(() => window.scrollY)).toBeGreaterThan(0);
    if (width <= 700) {
      await page.getByTestId('mobile-menu-button').click();
    }
    const listLink = page.getByRole('link', { name: 'Recipe List', exact: true });
    await expect(listLink).not.toHaveAttribute('aria-current');
    await expect(listLink).not.toHaveClass(/app-navigation-link-active|flyout-link-active/);
    await listLink.click();
    await expect(page).toHaveURL(/\/recipes$/);
    await expect(page.getByRole('heading', { name: 'Recipe List', exact: true })).toBeVisible();
    await expect(page.getByRole('searchbox', { name: 'Search recipes' })).toBeVisible();
  });
}

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
  await expect(page.getByRole('link', { name: 'View original recipe source' }))
    .toHaveAttribute('href', recipeDetail.sourceUrl!);
  await expect(page.getByRole('link', { name: 'View original recipe source' }))
    .toHaveAttribute('rel', 'noopener noreferrer');
});

test('renders only valid absolute HTTP and HTTPS source URLs as external links', async ({ page }) => {
  const details = { [recipeId]: { ...recipeDetail, sourceUrl: 'javascript:alert(document.domain)' } };
  await mockRecipeApi(page, { details });
  await page.goto(`/recipes/${recipeId}`);

  await expect(page.getByText('javascript:alert(document.domain)', { exact: true })).toBeVisible();
  await expect(page.getByText('Link unavailable')).toBeVisible();
  await expect(page.getByRole('link', { name: /javascript:/i })).toHaveCount(0);

  for (const safeUrl of [
    'http://example.test/source',
    `https://example.test/${'long-recipe-path-'.repeat(20)}?source=original`,
  ]) {
    details[recipeId] = { ...recipeDetail, sourceUrl: safeUrl };
    await page.goto(`/recipes/${recipeId}`);
    await expect(page.getByRole('link', { name: 'View original recipe source' }))
      .toHaveAttribute('href', safeUrl);
    await expect(page.getByRole('link', { name: 'View original recipe source' }))
      .toHaveAttribute('rel', 'noopener noreferrer');
    await expect(page.getByText(safeUrl, { exact: true })).toHaveCount(0);
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
  await page.setViewportSize({ width: 375, height: 800 });
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
  const initialImageRequestCount = api.imageRequests.length;
  expect(initialImageRequestCount).toBeGreaterThan(0);
  expect(api.imageRequests.every((path) => path === `/api/recipes/${recipeId}/image`)).toBe(true);

  failImage = false;
  await page.getByRole('button', { name: /retry image/i }).click();
  const image = page.getByRole('img', { name: 'Tofu Bowl recipe' });
  await expect(image).toBeVisible();
  await expect(image).toHaveAttribute('width', '1');
  await expect(image).toHaveAttribute('height', '1');
  await expect(image).toHaveAttribute('src', /^blob:/);
  expect(api.imageRequests).toHaveLength(initialImageRequestCount + 1);
});

test('loads images only from the private app endpoint and fits responsive viewports', async ({ page }) => {
  const api = await mockRecipeApi(page);

  for (const width of [375, 1280]) {
    await page.setViewportSize({ width, height: 800 });
    await page.goto(`/recipes/${recipeId}`);

    const image = page.getByRole('img', { name: 'Tofu Bowl recipe' });
    await expect(image).toBeVisible();
    expect(api.imageRequests.at(-1)).toBe(`/api/recipes/${recipeId}/image`);
    expect(await page.evaluate(() =>
      document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
  }
});

test('keeps desktop images compact beside the heading and preserves the mobile image order', async ({ page }) => {
  let releaseImage: (() => void) | undefined;
  await mockRecipeApi(page, {
    imageResponse: async (route) => {
      await new Promise<void>((resolve) => { releaseImage = resolve; });
      await route.fulfill({ status: 200, contentType: 'image/png', body: syntheticPngBytes });
    },
  });
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto(`/recipes/${recipeId}`);
  const frame = page.locator('.recipe-image-frame');
  await expect(page.getByText('Loading recipe image…')).toBeVisible();
  const loadingBounds = await frame.boundingBox();
  expect(loadingBounds).not.toBeNull();
  expect(loadingBounds!.width).toBeLessThanOrEqual(200);
  expect(loadingBounds!.height).toBeLessThanOrEqual(160);
  releaseImage?.();
  await expect(page.getByRole('img', { name: 'Tofu Bowl recipe' })).toBeVisible();
  await expect(frame).toHaveAttribute('aria-busy', 'false');
  expect(await frame.boundingBox()).toEqual(loadingBounds);

  for (const width of [701, 1280, 1920, 375, 700]) {
    await page.setViewportSize({ width, height: 900 });
    const imageBounds = await frame.boundingBox();
    const headingBounds = await page.getByRole('heading', { name: 'Tofu Bowl', exact: true }).boundingBox();
    const metadataBounds = await page.locator('.recipe-metadata').boundingBox();
    const stepsBounds = await page.getByRole('list', { name: 'Recipe steps' }).boundingBox();
    expect(imageBounds).not.toBeNull();
    expect(headingBounds).not.toBeNull();
    expect(metadataBounds).not.toBeNull();
    expect(stepsBounds).not.toBeNull();
    if (width > 700) {
      expect(imageBounds!.width).toBeLessThanOrEqual(200);
      expect(imageBounds!.height).toBeLessThanOrEqual(160);
      expect(imageBounds!.x).toBeGreaterThanOrEqual(headingBounds!.x + headingBounds!.width);
      expect(metadataBounds!.y).toBeGreaterThanOrEqual(imageBounds!.y + imageBounds!.height);
      expect(imageBounds!.y).toBeLessThan(metadataBounds!.y);
    } else {
      expect(imageBounds!.y).toBeGreaterThanOrEqual(metadataBounds!.y + metadataBounds!.height);
      expect(imageBounds!.width).toBeCloseTo(metadataBounds!.width, 0);
    }
    expect(stepsBounds!.y).toBeGreaterThanOrEqual(imageBounds!.y + imageBounds!.height);
    await expect(page.locator('.recipe-image')).toHaveCSS('object-fit', 'contain');
  }
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
