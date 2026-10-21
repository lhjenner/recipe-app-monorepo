import type { IngredientMappingState as MappingState } from '../services/ingredientsApi';

interface IngredientMappingStateProps {
  state: MappingState;
  departmentName: string | null;
}

function IngredientMappingState({ state, departmentName }: IngredientMappingStateProps) {
  if (state === 'mapped' && departmentName) {
    return <span>{departmentName}</span>;
  }

  if (state === 'notStocked') {
    return <span className="ingredient-not-stocked">Not stocked</span>;
  }

  return <span className="ingredient-missing">Missing</span>;
}

export default IngredientMappingState;
