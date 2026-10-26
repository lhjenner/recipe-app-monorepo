import { render, screen } from '@testing-library/react';
import { expect, test } from 'vitest';
import IngredientCategory from '../../src/components/IngredientCategory';

test('shows the category name in title case', () => {
  render(<IngredientCategory categoryName="baking aisle" excluded={false} />);

  expect(screen.getByText('Baking Aisle')).toBeVisible();
});

test('keeps the category name for an excluded ingredient that has one', () => {
  render(<IngredientCategory categoryName="drinks" excluded />);

  expect(screen.getByText('Drinks')).toBeVisible();
  expect(screen.queryByText('Excluded')).not.toBeInTheDocument();
});

test('warns that the category is missing on an active ingredient', () => {
  render(<IngredientCategory categoryName={null} excluded={false} />);

  expect(screen.getByText('Missing')).toHaveClass('ingredient-missing');
});

test('shows muted Excluded rather than a warning when an excluded ingredient has no category', () => {
  render(<IngredientCategory categoryName={null} excluded />);

  expect(screen.getByText('Excluded')).toHaveClass('ingredient-excluded');
  expect(screen.queryByText('Missing')).not.toBeInTheDocument();
});
