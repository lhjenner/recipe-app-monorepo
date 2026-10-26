import { render, screen } from '@testing-library/react';
import { expect, test } from 'vitest';
import IngredientMappingState from '../../src/components/IngredientMappingState';

test('shows the mapped department name', () => {
  render(<IngredientMappingState state="mapped" departmentName="Produce" />);

  expect(screen.getByText('Produce')).toBeVisible();
});

test('labels an explicitly unavailable store', () => {
  render(<IngredientMappingState state="notStocked" departmentName={null} />);

  expect(screen.getByText('Not stocked')).toBeVisible();
});

test('labels an unmapped store with visible missing text', () => {
  render(<IngredientMappingState state="unmapped" departmentName={null} />);

  const missing = screen.getByText('Missing');
  expect(missing).toBeVisible();
  expect(missing).toHaveClass('ingredient-missing');
});

test.each([
  ['mapped', 'Produce'],
  ['notStocked', null],
  ['unmapped', null],
] as const)('shows muted Excluded instead of the %s state for an excluded ingredient', (state, departmentName) => {
  render(<IngredientMappingState state={state} departmentName={departmentName} excluded />);

  const excluded = screen.getByText('Excluded');
  expect(excluded).toHaveClass('ingredient-excluded');
  expect(excluded).toHaveAttribute('title', 'Excluded from grocery list');
  expect(screen.queryByText(/Missing|Not stocked|Produce/)).not.toBeInTheDocument();
});
