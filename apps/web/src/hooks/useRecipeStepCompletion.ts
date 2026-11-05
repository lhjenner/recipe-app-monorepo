import { useState } from 'react';

export function useRecipeStepCompletion(recipeId: string | undefined) {
  const [state, setState] = useState(() => ({
    recipeId,
    completedSteps: new Set<number>(),
  }));

  if (state.recipeId !== recipeId) {
    setState({ recipeId, completedSteps: new Set<number>() });
  }

  function toggleStep(position: number) {
    setState((current) => {
      const completedSteps = new Set(current.completedSteps);
      if (completedSteps.has(position)) {
        completedSteps.delete(position);
      } else {
        completedSteps.add(position);
      }
      return { recipeId, completedSteps };
    });
  }

  return { completedSteps: state.completedSteps, toggleStep };
}
