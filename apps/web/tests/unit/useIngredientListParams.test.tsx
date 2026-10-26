import { act, renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { MemoryRouter, useLocation, useNavigate } from 'react-router-dom';
import { expect, test } from 'vitest';
import { useIngredientListParams } from '../../src/hooks/useIngredientListParams';

function renderAt(url: string) {
  const wrapper = ({ children }: { children: ReactNode }) => (
    <MemoryRouter initialEntries={[url]}>{children}</MemoryRouter>
  );
  return renderHook(() => ({
    params: useIngredientListParams(),
    navigate: useNavigate(),
    search: useLocation().search,
  }), { wrapper });
}

test('reads page, search and archived from the URL', () => {
  const { result } = renderAt('/ingredient-maintenance?search=water&archived=true&page=3');

  expect(result.current.params).toMatchObject({ search: 'water', archived: true, page: 3 });
});

test.each(['abc', '0', '-2', '1.5', '99999999999999999999'])('falls back to page 1 for page=%s', (page) => {
  const { result } = renderAt(`/ingredient-maintenance?page=${page}`);

  expect(result.current.params.page).toBe(1);
});

test('changing the search updates the value immediately and resets the page', () => {
  const { result } = renderAt('/ingredient-maintenance?page=4');

  act(() => result.current.params.setSearch('water'));

  expect(result.current.params).toMatchObject({ search: 'water', page: 1 });
  expect(result.current.search).toBe('?search=water');
});

test('toggling archived resets the page and omits defaults from the URL', () => {
  const { result } = renderAt('/ingredient-maintenance?archived=true&page=2');

  act(() => result.current.params.setArchived(false));

  expect(result.current.params).toMatchObject({ archived: false, page: 1 });
  expect(result.current.search).toBe('');
});

test('adopts filters when the URL changes from outside the list controls', () => {
  const { result } = renderAt('/ingredient-maintenance?search=water');

  act(() => {
    void result.current.navigate('/ingredient-maintenance?search=flour&archived=true');
  });

  expect(result.current.params).toMatchObject({ search: 'flour', archived: true });
});
