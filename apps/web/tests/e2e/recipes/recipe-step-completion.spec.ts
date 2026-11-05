import { expect, test } from '@playwright/test';
import { mockAuthApi } from '../support/mockAuthApi.ts';
import { mockRecipeApi, recipeDetail, recipeId, secondRecipeId } from '../support/mockRecipeApi.ts';

test.beforeEach(async ({ page }) => {
  await mockAuthApi(page, { hasSession: true });
  await mockRecipeApi(page, {
    summaries: [
      { id: recipeId, name: recipeDetail.name },
      { id: secondRecipeId, name: 'Another recipe' },
    ],
    details: {
      [recipeId]: recipeDetail,
      [secondRecipeId]: { ...recipeDetail, id: secondRecipeId, name: 'Another recipe', image: null },
    },
  });
  await page.goto(`/recipes/${recipeId}`);
});

test('toggles paired steps from any content and supports keyboard without write requests', async ({ page }) => {
  const writes: string[] = [];
  page.on('request', (request) => {
    if (request.method() !== 'GET') writes.push(request.method());
  });
  const step = page.locator('.recipe-step-paired').first();
  const instructions = step.locator('.recipe-step-instructions');
  await instructions.click();
  await expect(step).not.toHaveClass(/recipe-step-done/);
  await instructions.dblclick();
  await expect(step).toHaveClass(/recipe-step-done/);
  await expect(step).toHaveAccessibleName('Step 1, done');
  for (const text of [step.locator('h2'), step.locator('h3'), instructions, step.locator('.recipe-step-notes')]) {
    await expect(text).toHaveCSS('font-style', 'italic');
    await expect(text).toHaveCSS('color', 'rgb(158, 154, 154)');
  }
  await step.getByRole('heading', { name: 'Step 1', exact: true }).dblclick();
  await expect(step).not.toHaveClass(/recipe-step-done/);
  await expect(instructions).toHaveCSS('font-style', 'normal');
  await step.focus();
  await step.press('Enter');
  await expect(step).toHaveClass(/recipe-step-done/);
  await step.press('Space');
  await expect(step).not.toHaveClass(/recipe-step-done/);
  await step.locator('.recipe-step-items').dblclick();
  await expect(step).toHaveClass(/recipe-step-done/);
  await expect(page.locator('.recipe-step').nth(1)).not.toHaveClass(/recipe-step-done/);
  await expect(page.getByRole('checkbox')).toHaveCount(0);
  expect(writes).toHaveLength(0);
});

test('resets on refresh, leaving the page, and another recipe', async ({ page }) => {
  const step = page.locator('.recipe-step-paired').first();
  await step.dblclick();
  await expect(step).toHaveClass(/recipe-step-done/);
  await page.reload();
  await expect(step).toBeVisible();
  await expect(step).not.toHaveClass(/recipe-step-done/);
  await step.dblclick();
  await page.getByRole('link', { name: 'Recipe List', exact: true }).click();
  await page.getByRole('link', { name: 'Tofu Bowl', exact: true }).click();
  await expect(step).toBeVisible();
  await expect(step).not.toHaveClass(/recipe-step-done/);
  await step.dblclick();
  await page.getByRole('link', { name: 'Recipe List', exact: true }).click();
  await page.getByRole('link', { name: 'Another Recipe', exact: true }).click();
  await expect(step).toBeVisible();
  await expect(step).not.toHaveClass(/recipe-step-done/);
});

test.describe('touch completion', () => {
  test.use({ hasTouch: true, viewport: { width: 375, height: 800 } });

  test('double taps toggle once and a single tap does not complete a step', async ({ page }) => {
    const step = page.locator('.recipe-step-paired').first();
    const instructions = step.locator('.recipe-step-instructions');
    await instructions.tap();
    await expect(step).not.toHaveClass(/recipe-step-done/);
    // Reload to separate the single-tap check from the double-tap gesture.
    await page.reload();
    await instructions.tap();
    await instructions.tap();
    await expect(step).toHaveClass(/recipe-step-done/);
    await expect(step).toHaveAccessibleName('Step 1, done');
    await step.locator('.recipe-step-items').tap();
    await step.locator('.recipe-step-items').tap();
    await expect(step).not.toHaveClass(/recipe-step-done/);
  });
});
