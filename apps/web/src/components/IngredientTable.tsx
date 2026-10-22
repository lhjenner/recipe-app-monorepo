import type {
  IngredientSummary,
  SupermarketStore,
} from '../services/ingredientsApi';
import IngredientMappingState from './IngredientMappingState';
import { Link } from 'react-router-dom';

interface IngredientTableProps {
  ingredients: IngredientSummary[];
  stores: SupermarketStore[];
  archived: boolean;
}

function IngredientTable({ ingredients, stores, archived }: IngredientTableProps) {
  return (
    <div className="ingredient-table-wrap">
      <table className="ingredient-table" aria-label="Ingredients">
        <thead>
          <tr>
            <th scope="col">Ingredient</th>
            <th scope="col">Category</th>
            {stores.map((store) => (
              <th key={store.code} scope="col">{store.name}</th>
            ))}
            <th scope="col">Actions</th>
          </tr>
        </thead>
        <tbody>
          {ingredients.map((ingredient) => {
            const storeStates = new Map(
              ingredient.stores.map((store) => [store.storeCode, store]),
            );
            return (
              <tr key={ingredient.id}>
                <th scope="row">
                  <span>{ingredient.name}</span>
                  {ingredient.excludeFromGroceryList && (
                    <span className="ingredient-exclusion-badge">Excluded from grocery list</span>
                  )}
                </th>
                <td>
                  {ingredient.categoryName
                    ? ingredient.categoryName
                    : <span className="ingredient-missing">Missing</span>}
                </td>
                {stores.map((store) => {
                  const mapping = storeStates.get(store.code);
                  return (
                    <td key={store.code}>
                      <IngredientMappingState
                        state={mapping?.state ?? 'unmapped'}
                        departmentName={mapping?.departmentName ?? null}
                      />
                    </td>
                  );
                })}
                <td className="ingredient-actions">
                  {archived ? (
                    <button type="button" disabled aria-label={`Restore ${ingredient.name}`}>
                      Restore
                    </button>
                  ) : (
                    <>
                      <Link to={`/ingredient-maintenance/${ingredient.id}`} aria-label={`Edit ${ingredient.name}`}>
                        Edit
                      </Link>
                      <button type="button" disabled aria-label={`Archive ${ingredient.name}`}>
                        Archive
                      </button>
                    </>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

export default IngredientTable;
