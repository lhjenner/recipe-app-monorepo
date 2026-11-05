import { act, renderHook } from '@testing-library/react';
import { expect, test } from 'vitest';
import { useRecipeStepCompletion } from '../../src/hooks/useRecipeStepCompletion';

test('keeps done steps across same-recipe rerenders and resets for a different recipe or unmount', () => {
  const { result, rerender, unmount } = renderHook(
    ({ id }) => useRecipeStepCompletion(id),
    { initialProps: { id: 'recipe-one' } },
  );
  act(() => result.current.toggleStep(0));
  act(() => result.current.toggleStep(2));
  expect([...result.current.completedSteps]).toEqual([0, 2]);
  rerender({ id: 'recipe-one' });
  expect([...result.current.completedSteps]).toEqual([0, 2]);
  act(() => result.current.toggleStep(0));
  expect([...result.current.completedSteps]).toEqual([2]);
  rerender({ id: 'recipe-two' });
  expect(result.current.completedSteps.size).toBe(0);
  rerender({ id: 'recipe-one' });
  expect(result.current.completedSteps.size).toBe(0);
  unmount();
  const fresh = renderHook(() => useRecipeStepCompletion('recipe-one'));
  expect(fresh.result.current.completedSteps.size).toBe(0);
});
