import { act, renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { MemoryRouter, useLocation, useNavigate } from 'react-router-dom';
import { expect, test } from 'vitest';
import { useRecipeListParams } from '../../src/hooks/useRecipeListParams';

function renderAt(url: string) {
  const wrapper = ({ children }: { children: ReactNode }) => (
    <MemoryRouter initialEntries={[url]}>{children}</MemoryRouter>
  );
  return renderHook(() => ({
    params: useRecipeListParams(),
    navigate: useNavigate(),
    search: useLocation().search,
  }), { wrapper });
}

test('reads search and page from the URL and defaults malformed pages to one', () => {
  const { result, rerender } = renderAt('/recipes?search=tofu&page=3');
  expect(result.current.params).toMatchObject({ search: 'tofu', page: 3 });

  act(() => { void result.current.navigate('/recipes?search=pasta&page=abc'); });
  rerender();
  expect(result.current.params).toMatchObject({ search: 'pasta', page: 1 });
});

test('changing search immediately resets page one and removes default query values', () => {
  const { result } = renderAt('/recipes?page=4');

  act(() => result.current.params.setSearch('tofu'));

  expect(result.current.params).toMatchObject({ search: 'tofu', page: 1 });
  expect(result.current.search).toBe('?search=tofu');

  act(() => result.current.params.setSearch(''));
  expect(result.current.search).toBe('');
});

test('adopts search and page when browser navigation changes the URL', () => {
  const { result, rerender } = renderAt('/recipes?search=tofu&page=2');

  act(() => { void result.current.navigate('/recipes?search=pasta&page=4'); });
  rerender();

  expect(result.current.params).toMatchObject({ search: 'pasta', page: 4 });
});
