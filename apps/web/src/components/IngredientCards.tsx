import type {
  IngredientSummary,
  SupermarketStore,
} from '../services/ingredientsApi';
import IngredientCategory from './IngredientCategory';
import IngredientMappingState from './IngredientMappingState';
import { Link } from 'react-router-dom';
import { toTitleCase } from '../titleCase';

interface IngredientCardsProps {
  ingredients: IngredientSummary[];
  stores: SupermarketStore[];
  archived: boolean;
  pendingIngredientId: string | null;
  onArchive: (id: string, name: string) => void;
  onRestore: (id: string, name: string) => void;
}

function IngredientCards({
  ingredients,
  stores,
  archived,
  pendingIngredientId,
  onArchive,
  onRestore,
}: IngredientCardsProps) {
  return (
    <div className="ingredient-cards">
      {ingredients.map((ingredient) => {
        const storeStates = new Map(
          ingredient.stores.map((store) => [store.storeCode, store]),
        );
        const name = toTitleCase(ingredient.name);
        return (
          <article className="ingredient-card" key={ingredient.id} aria-label={name}>
            <h2>{name}</h2>
            <dl className="ingredient-card-details">
              <div>
                <dt>Category</dt>
                <dd>
                  <IngredientCategory categoryName={ingredient.categoryName} excluded={ingredient.excludeFromGroceryList} />
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
                        excluded={ingredient.excludeFromGroceryList}
                      />
                    </dd>
                  </div>
                );
              })}
            </dl>
            <div className="ingredient-card-actions">
              {archived ? (
                <button
                  type="button"
                  disabled={pendingIngredientId === ingredient.id}
                  aria-label={`Restore ${name}`}
                  onClick={() => onRestore(ingredient.id, name)}
                >
                  Restore
                </button>
              ) : (
                <>
                  <Link className="btn btn-secondary" to={`/ingredient-maintenance/${ingredient.id}`} aria-label={`Edit ${name}`}>
                    Edit
                  </Link>
                  <button
                    className="btn btn-danger-secondary"
                    type="button"
                    disabled={pendingIngredientId === ingredient.id}
                    aria-label={`Archive ${name}`}
                    onClick={() => onArchive(ingredient.id, name)}
                  >
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
