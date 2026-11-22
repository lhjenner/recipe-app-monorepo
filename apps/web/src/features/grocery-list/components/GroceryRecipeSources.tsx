import { Link } from 'react-router-dom';
import { toTitleCase } from '../../../shared/utils/titleCase';
import type { RecipeSource } from '../hooks/useGroceryRecipeRemoval';
import type { GroceryItem } from '../services/groceryListApi';

interface GroceryRecipeSourcesProps {
  items: GroceryItem[];
  disabled: boolean;
  onRemove: (recipe: RecipeSource) => void;
}

function GroceryRecipeSources({ items, disabled, onRemove }: GroceryRecipeSourcesProps) {
  const sources = new Map<string, RecipeSource>();
  let hasMissingId = false;
  for (const item of items) {
    if (item.state !== 'outstanding') continue;
    for (const contribution of item.contributions) {
      if (contribution.origin !== 'recipe' || contribution.state !== 'outstanding') continue;
      if (!contribution.recipeId) {
        hasMissingId = true;
        continue;
      }
      if (!sources.has(contribution.recipeId)) {
        sources.set(contribution.recipeId, {
          id: contribution.recipeId,
          name: contribution.recipeNameSnapshot?.trim() || 'Recipe name unavailable',
        });
      }
    }
  }
  const recipes = [...sources.values()].sort((first, second) => first.name.localeCompare(second.name));
  if (recipes.length === 0 && !hasMissingId) return null;

  return (
    <details className="grocery-recipe-sources">
      <summary>Recipes on Grocery List</summary>
      <ul>
        {recipes.map((recipe) => (
          <li key={recipe.id}>
            <Link to={`/recipes/${recipe.id}`}>{toTitleCase(recipe.name)}</Link>
            <button className="btn btn-danger-secondary" type="button" disabled={disabled}
              aria-label={`Remove ${toTitleCase(recipe.name)} from Grocery List`}
              onClick={() => onRemove(recipe)}>
              Remove
            </button>
          </li>
        ))}
      </ul>
      {hasMissingId && <p role="status">Some Recipe sources lack an identifier and cannot be removed here.</p>}
    </details>
  );
}

export default GroceryRecipeSources;
