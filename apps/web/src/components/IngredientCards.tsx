import type {
  IngredientSummary,
  SupermarketStore,
} from '../services/ingredientsApi';
import IngredientMappingState from './IngredientMappingState';

interface IngredientCardsProps {
  ingredients: IngredientSummary[];
  stores: SupermarketStore[];
  archived: boolean;
}

function IngredientCards({ ingredients, stores, archived }: IngredientCardsProps) {
  return (
    <div className="ingredient-cards">
      {ingredients.map((ingredient) => {
        const storeStates = new Map(
          ingredient.stores.map((store) => [store.storeCode, store]),
        );
        return (
          <article className="ingredient-card" key={ingredient.id} aria-label={ingredient.name}>
            <div className="ingredient-card-heading">
              <h2>{ingredient.name}</h2>
              {ingredient.excludeFromGroceryList && (
                <span className="ingredient-exclusion-badge">Excluded from grocery list</span>
              )}
            </div>
            <dl className="ingredient-card-details">
              <div>
                <dt>Category</dt>
                <dd>
                  {ingredient.categoryName
                    ? ingredient.categoryName
                    : <span className="ingredient-missing">Missing</span>}
                </dd>
              </div>
              {stores.map((store) => {
                const mapping = storeStates.get(store.code);
                return (
                  <div key={store.code}>
                    <dt>{store.name}</dt>
                    <dd>
                      <IngredientMappingState
                        state={mapping?.state ?? 'unmapped'}
                        departmentName={mapping?.departmentName ?? null}
                      />
                    </dd>
                  </div>
                );
              })}
            </dl>
            <div className="ingredient-card-actions">
              {archived ? (
                <button type="button" disabled aria-label={`Restore ${ingredient.name}`}>
                  Restore
                </button>
              ) : (
                <>
                  <button type="button" disabled aria-label={`Edit ${ingredient.name}`}>
                    Edit
                  </button>
                  <button type="button" disabled aria-label={`Archive ${ingredient.name}`}>
                    Archive
                  </button>
                </>
              )}
            </div>
          </article>
        );
      })}
    </div>
  );
}

export default IngredientCards;
