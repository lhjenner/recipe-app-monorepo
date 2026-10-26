import { expect, test } from 'vitest';
import { readListSearch } from '../../src/ingredientListLocation';

test.each([
  [{ listSearch: '?page=7&search=wat' }, '?page=7&search=wat'],
  [{ listSearch: '' }, ''],
  [null, ''],
  [undefined, ''],
  [{ listSearch: 42 }, ''],
  [{ listSearch: '//evil.example' }, ''],
  [{ listSearch: '/ingredient-maintenance/new' }, ''],
])('reads a safe list query string from navigation state %#', (state, expected) => {
  expect(readListSearch(state)).toBe(expected);
});
