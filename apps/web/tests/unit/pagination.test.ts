import { describe, expect, test } from 'vitest';
import { getPageItems } from '../../src/pagination';

describe('getPageItems', () => {
  test.each([
    [1, 1, [1]],
    [1, 7, [1, 2, 3, 4, 5, 6, 7]],
    [4, 7, [1, 2, 3, 4, 5, 6, 7]],
  ])('shows every page when page %i of %i fits', (current, total, expected) => {
    expect(getPageItems(current, total, 2)).toEqual(expected);
  });

  test('keeps the first and last page visible with ellipses around the current window', () => {
    expect(getPageItems(7, 13, 2)).toEqual([1, 'ellipsis', 5, 6, 7, 8, 9, 'ellipsis', 13]);
  });

  test('only adds an ellipsis on the side that skips pages', () => {
    expect(getPageItems(1, 13, 2)).toEqual([1, 2, 3, 'ellipsis', 13]);
    expect(getPageItems(13, 13, 2)).toEqual([1, 'ellipsis', 11, 12, 13]);
  });

  test('shows the page number instead of an ellipsis that would hide a single page', () => {
    expect(getPageItems(5, 13, 2)).toEqual([1, 2, 3, 4, 5, 6, 7, 'ellipsis', 13]);
    expect(getPageItems(9, 13, 2)).toEqual([1, 'ellipsis', 7, 8, 9, 10, 11, 12, 13]);
  });

  test('shows only the boundaries and current page on mobile', () => {
    expect(getPageItems(7, 13, 0)).toEqual([1, 'ellipsis', 7, 'ellipsis', 13]);
    expect(getPageItems(1, 13, 0)).toEqual([1, 'ellipsis', 13]);
    expect(getPageItems(2, 3, 0)).toEqual([1, 2, 3]);
  });

  test('clamps an out-of-range current page', () => {
    expect(getPageItems(99, 13, 2)).toEqual([1, 'ellipsis', 11, 12, 13]);
    expect(getPageItems(0, 13, 2)).toEqual([1, 2, 3, 'ellipsis', 13]);
  });
});
