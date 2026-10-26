import type {
  IngredientSummary,
  SupermarketStore,
} from '../services/ingredientsApi';
import IngredientCategory from './IngredientCategory';
import IngredientMappingState from './IngredientMappingState';
import { Link } from 'react-router-dom';
import { toTitleCase } from '../titleCase';

interface IngredientTableProps {
  ingredients: IngredientSummary[];
  stores: SupermarketStore[];
  archived: boolean;
  pendingIngredientId: string | null;
  onArchive: (id: string, name: string) => void;
  onRestore: (id: string, name: string) => void;
}

function IngredientTable({
  ingredients,
  stores,
  archived,
  pendingIngredientId,
  onArchive,
  onRestore,
}: IngredientTableProps) {
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
            const name = toTitleCase(ingredient.name);
            return (
              <tr key={ingredient.id}>
                <th scope="row">{name}</th>
                <td>
                  <IngredientCategory categoryName={ingredient.categoryName} excluded={ingredient.excludeFromGroceryList} />
                </td>
                {stores.map((store) => {
                  const mapping = storeStates.get(store.code);
                  return (
                    <td key={store.code}>
                      <IngredientMappingState
                        state={mapping?.state ?? 'unmapped'}
                        departmentName={mapping?.departmentName ?? null}
                        excluded={ingredient.excludeFromGroceryList}
                      />
                    </td>
                  );
                })}
                <td className="ingredient-actions">
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
