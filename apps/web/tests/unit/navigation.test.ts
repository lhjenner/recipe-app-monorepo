import { expect, test } from 'vitest'
import { getActiveNavigationItem, navigationItems } from '../../src/navigation'

test('lists menu destinations in their required order', () => {
  expect(navigationItems).toEqual([
    { label: 'Home', route: '/home', pageTitle: 'Home' },
    { label: 'Recipe List', route: '/recipes', pageTitle: 'Recipe List' },
    { label: 'Add Recipe', route: '/recipes/new', pageTitle: 'Add Recipe' },
    { label: 'Grocery List', route: '/grocery-list', pageTitle: 'Grocery List' },
    { label: 'Meal Planner', route: '/meal-planner', pageTitle: 'Meal Planner' },
    { label: 'Food Diary', route: '/food-diary', pageTitle: 'Food Diary' },
    { label: 'Ingredient Maintenance', route: '/ingredient-maintenance', pageTitle: 'Ingredient Maintenance' },
    { label: 'Archived Recipes', route: '/recipes/archived', pageTitle: 'Archived Recipes' },
  ])
})

test.each([
  ['/home', 'Home'],
  ['/recipes', 'Recipe List'],
  ['/recipes/123', 'Recipe List'],
  ['/recipes/new', 'Add Recipe'],
  ['/recipes/new/step-2', 'Add Recipe'],
  ['/recipes/archived', 'Archived Recipes'],
  ['/recipes/archived/2026', 'Archived Recipes'],
  ['/unknown-route', undefined],
  ['/recipes-legacy', undefined],
])('finds the longest matching navigation route for %s', (pathname, expectedLabel) => {
  expect(getActiveNavigationItem(pathname)?.label).toBe(expectedLabel)
})
