import type { IngredientMappingState as MappingState } from '../services/ingredientsApi';
import { toTitleCase } from '../titleCase';

interface IngredientMappingStateProps {
  state: MappingState;
  departmentName: string | null;
  excluded?: boolean;
}

function IngredientMappingState({ state, departmentName, excluded = false }: IngredientMappingStateProps) {
  // Store mappings are irrelevant when the ingredient never goes on a grocery list.
  if (excluded) {
    return <span className="ingredient-excluded" title="Excluded from grocery list">Excluded</span>;
  }

  if (state === 'mapped' && departmentName) {
    return <span>{toTitleCase(departmentName)}</span>;
  }

  if (state === 'notStocked') {
    return <span className="ingredient-not-stocked">Not stocked</span>;
  }

  return <span className="ingredient-missing">Missing</span>;
}

export default IngredientMappingState;
