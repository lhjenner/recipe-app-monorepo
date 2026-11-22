import { useEffect, useState } from 'react';
import { getGroceryList, type GroceryList } from '../../grocery-list/services/groceryListApi';

export function useRecipeGroceryStatus(recipeId: string | null) {
  const [reloadToken, setReloadToken] = useState(0);
  const [result, setResult] = useState<{
    key: string;
    data: GroceryList | null;
    error: string | null;
  } | null>(null);
  const key = JSON.stringify([recipeId, reloadToken]);

  useEffect(() => {
    if (!recipeId) return;
    const controller = new AbortController();
    void getGroceryList(controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setResult({ key, data, error: null });
      })
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setResult({
            key, data: null,
            error: error instanceof Error ? error.message : 'Unable to check Grocery List items.',
          });
        }
      });
    const refresh = () => setReloadToken((token) => token + 1);
    window.addEventListener('focus', refresh);
    return () => {
      controller.abort();
      window.removeEventListener('focus', refresh);
    };
  }, [recipeId, key]);

  const current = result?.key === key ? result : null;
  const items = current?.data?.items.filter((item) => item.state === 'outstanding') ?? [];
  return {
    ingredientIds: new Set(items.flatMap((item) => item.ingredientId ? [item.ingredientId] : [])),
    hasOutstandingAddition: items.some((item) => item.contributions.some((contribution) =>
      contribution.origin === 'recipe' && contribution.state === 'outstanding'
      && contribution.recipeId === recipeId)),
    error: current?.error ?? null,
    reload: () => setReloadToken((token) => token + 1),
  };
}
