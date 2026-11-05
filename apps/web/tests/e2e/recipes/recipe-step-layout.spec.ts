import { expect, test } from '@playwright/test';
import { mockAuthApi } from '../support/mockAuthApi.ts';
import { mockRecipeApi, recipeDetail, recipeId } from '../support/mockRecipeApi.ts';

test('pairs step items and instructions and lets single-content steps fill the width', async ({ page }) => {
  await mockAuthApi(page, { hasSession: true });
  const firstStep = recipeDetail.steps[0]!;
  await mockRecipeApi(page, {
    details: {
      [recipeId]: {
        ...recipeDetail,
        steps: [
          {
            ...firstStep,
            instructions: `${'Long instructions remain readable and wrap naturally. '.repeat(8)}\nServe warm.`,
          },
          { position: 1, items: [], instructions: 'Instructions only.' },
          { ...firstStep, position: 2, instructions: ' ' },
          {
            position: 3,
            items: [{ ...firstStep.items[0]!, text: ' ', quantityText: null, unitText: null, notesText: null }],
            instructions: 'Blank items are not a second column.',
          },
        ],
      },
    },
  });

  for (const width of [320, 375, 700, 701, 1280]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto(`/recipes/${recipeId}`);
    await expect(page.getByRole('heading', { name: 'Step 4', exact: true })).toBeVisible();
    const steps = page.getByRole('list', { name: 'Recipe steps' });
    const geometry = await steps.evaluate((element) =>
      Array.from(element.children).map((step) => {
        function rect(target: Element) {
          const { x, y, width, height } = target.getBoundingClientRect();
          return { x, y, width, height };
        }
        return {
          step: rect(step),
          content: Array.from(step.querySelectorAll('.recipe-step-items, .recipe-step-instructions')).map(rect),
        };
      }));
    const paired = geometry[0]!.content;
    expect(paired).toHaveLength(2);
    expect(paired[0]!.y).toBeCloseTo(paired[1]!.y, 0);
    expect(paired[0]!.width).toBeCloseTo(paired[1]!.width, 0);
    expect(paired[1]!.x - paired[0]!.x - paired[0]!.width).toBeCloseTo(12, 0);
    for (const single of geometry.slice(1)) {
      expect(single.content).toHaveLength(1);
      expect(single.content[0]!.width).toBeCloseTo(single.step.width, 0);
    }
    await expect(steps.locator('.recipe-step-instructions').first()).toHaveCSS('white-space', 'pre-wrap');
    expect(await page.evaluate(() =>
      document.documentElement.scrollWidth > document.documentElement.clientWidth)).toBe(false);
  }
});
