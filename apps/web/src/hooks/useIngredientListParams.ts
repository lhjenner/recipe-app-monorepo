import { useCallback, useState } from 'react';
import { useSearchParams } from 'react-router-dom';

interface ListParamChanges {
  search?: string;
  page?: number;
  archived?: boolean;
}

interface ListFilters {
  search: string;
  archived: boolean;
}

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

/** Keeps the list's page, search and archived filter in the URL so Back, refresh and links restore them. */
export function useIngredientListParams() {
  const [params, setParams] = useSearchParams();
  const urlSearch = params.get('search') ?? '';
  const urlArchived = params.get('archived') === 'true';

  // Router updates commit after the input event, so controlled inputs read a local copy that updates
  // immediately. Whenever the URL itself changes (typing, Back, a link), the local copy adopts it.
  const [filters, setFilters] = useState<ListFilters>({ search: urlSearch, archived: urlArchived });
  const [seenUrl, setSeenUrl] = useState<ListFilters>(filters);
  if (seenUrl.search !== urlSearch || seenUrl.archived !== urlArchived) {
    const fromUrl = { search: urlSearch, archived: urlArchived };
    setSeenUrl(fromUrl);
    setFilters(fromUrl);
  }

  const update = useCallback((changes: ListParamChanges, replace: boolean) => {
    setParams((current) => {
      const next = new URLSearchParams(current);
      if (changes.search !== undefined) {
        setOrDelete(next, 'search', changes.search || null);
      }
      if (changes.archived !== undefined) {
        setOrDelete(next, 'archived', changes.archived ? 'true' : null);
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

  return {
    search: filters.search,
    page: parsePage(params.get('page')),
    archived: filters.archived,
    setPage,
    setSearch: (search: string) => {
      setFilters((current) => ({ ...current, search }));
      update({ search, page: 1 }, true);
    },
    setArchived: (archived: boolean) => {
      setFilters((current) => ({ ...current, archived }));
      update({ archived, page: 1 }, false);
    },
  };
}
