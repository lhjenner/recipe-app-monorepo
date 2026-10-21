import { expect, test } from 'vitest';
import { getSafeReturnTo } from '../../src/returnTo';

test.each([
  '/grocery-list',
  '/recipes/new?source=planner#ingredients',
  '/recipes?search=fish%20%26%20chips',
])('accepts the local destination %s', (destination) => {
  expect(getSafeReturnTo(destination)).toBe(destination);
});

test.each([
  null,
  '',
  'recipes',
  'https://evil.example',
  '//evil.example',
  'javascript:alert(1)',
  '\\\\evil.example',
  '/\\evil.example',
  '/\n/evil.example',
  '/\t/evil.example',
  '/ /evil.example',
  '/recipes/../..//evil.example',
])('falls back to Home for the unsafe destination %s (AC-27)', (destination) => {
  expect(getSafeReturnTo(destination)).toBe('/home');
});

test('normalizes dot segments before navigating', () => {
  expect(getSafeReturnTo('/recipes/../grocery-list')).toBe('/grocery-list');
});
