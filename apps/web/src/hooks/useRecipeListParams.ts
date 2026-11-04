import { useCallback, useState } from 'react';
import { useSearchParams } from 'react-router-dom';

function parsePage(value: string | null): number {
  if (!value || !/^\d+$/.test(value)) {
    return 1;
  }
  const page = Number(value);
  return Number.isSafeInteger(page) && page >= 1 ? page : 1;
}

function setOrDelete(params: URLSearchParams, key: string, value: string | null) {
  if (value === null) {
    params.delete(key);
  } else {
    params.set(key, value);
  }
}

export function useRecipeListParams() {
  const [params, setParams] = useSearchParams();
  const urlSearch = params.get('search') ?? '';
  const [search, setSearchState] = useState(urlSearch);
  const [seenUrlSearch, setSeenUrlSearch] = useState(urlSearch);

  if (seenUrlSearch !== urlSearch) {
    setSeenUrlSearch(urlSearch);
    setSearchState(urlSearch);
  }

  const update = useCallback((changes: { search?: string; page?: number }, replace: boolean) => {
    setParams((current) => {
      const next = new URLSearchParams(current);
      if (changes.search !== undefined) {
        setOrDelete(next, 'search', changes.search || null);
      }
      if (changes.page !== undefined) {
        setOrDelete(next, 'page', changes.page > 1 ? String(changes.page) : null);
      }
      return next;
    }, { replace });
  }, [setParams]);

  const setPage = useCallback(
    (page: number, options: { replace?: boolean } = {}) => update({ page }, options.replace ?? false),
    [update],
  );
  const setSearch = useCallback((nextSearch: string) => {
    setSearchState(nextSearch);
    update({ search: nextSearch, page: 1 }, true);
  }, [update]);

  return {
    search,
    page: parsePage(params.get('page')),
    setSearch,
    setPage,
  };
}
