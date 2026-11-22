import type { GroceryItem } from '../services/groceryListApi';

function GroceryPurchaseSuggestion({ item }: { item: GroceryItem }) {
  const hasSuggestion = item.state === 'outstanding' && item.dimension !== 'count'
    && item.contributions.some((contribution) =>
      contribution.origin === 'recipe' && contribution.state === 'outstanding');
  if (!hasSuggestion) return null;
  const hasManualDemand = item.contributions.some((contribution) =>
    contribution.origin === 'manual' && contribution.state === 'outstanding');

  return (
    <p>
      Includes 1 unverified Recipe suggestion; check package size and quantity.
      {hasManualDemand && ' The remaining quantity is manual.'}
    </p>
  );
}

export default GroceryPurchaseSuggestion;
