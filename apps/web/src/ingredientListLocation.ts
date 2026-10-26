export const INGREDIENT_LIST_PATH = '/ingredient-maintenance';

export interface IngredientListLocationState {
  listSearch: string;
}

/** Router state for links leaving the list, so the editor can return to the same page/search. */
export function listLocationState(listSearch: string): IngredientListLocationState {
  return { listSearch };
}

/** Accepts only a query string, so state can never redirect the editor somewhere else. */
export function readListSearch(state: unknown): string {
  if (typeof state !== 'object' || state === null || !('listSearch' in state)) {
    return '';
  }
  const { listSearch } = state;
  return typeof listSearch === 'string' && (listSearch === '' || listSearch.startsWith('?'))
    ? listSearch
    : '';
}
