import { toTitleCase } from '../titleCase';

interface IngredientCategoryProps {
  categoryName: string | null;
  excluded: boolean;
}

function IngredientCategory({ categoryName, excluded }: IngredientCategoryProps) {
  if (categoryName) {
    return <>{toTitleCase(categoryName)}</>;
  }

  // A missing category is not worth warning about when the ingredient never reaches a grocery list.
  if (excluded) {
    return <span className="ingredient-excluded" title="Excluded from grocery list">Excluded</span>;
  }

  return <span className="ingredient-missing">Missing</span>;
}

export default IngredientCategory;
