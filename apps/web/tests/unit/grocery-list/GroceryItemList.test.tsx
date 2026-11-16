import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { expect, test } from 'vitest';
import GroceryItemList from '../../../src/features/grocery-list/components/GroceryItemList';
import type { GroceryItem } from '../../../src/features/grocery-list/services/groceryListApi';

test.each([
  { name: 'manual-only', origins: ['manual'] as const, amount: 2, unit: 'onion' },
  { name: 'Recipe-only', origins: ['recipe'] as const, amount: 2, unit: 'onion' },
  { name: 'mixed manual and Recipe', origins: ['manual', 'recipe'] as const, amount: 4, unit: 'onion' },
  { name: 'manual without amount', origins: ['manual'] as const, amount: null, unit: 'onion' },
  { name: 'Recipe without unit', origins: ['recipe'] as const, amount: 2, unit: null },
  { name: 'Recipe without a numeric total', origins: ['recipe'] as const, amount: null, unit: null },
])('shows useful requirements and Recipe Sources for $name on desktop and mobile', ({ origins, amount, unit }) => {
  const item: GroceryItem = {
    id: 'item',
    ingredientId: 'ingredient',
    name: 'onions',
    state: 'outstanding',
    note: 'Choose firm onions.',
    dimension: 'count',
    countLabel: 'onion',
    requirementAmount: amount,
    requirementUnit: unit,
    outstandingPurchaseQuantity: origins.length * 2,
    purchaseUnit: 'onion',
    ingredientVersion: null,
    adHocStoreId: null,
    adHocDepartmentId: null,
    adHocDepartmentName: null,
    storeMappings: [],
    contributions: origins.map((origin) => ({
      id: origin,
      origin,
      state: 'outstanding',
      originalAmountText: '2',
      originalUnitText: 'onion',
      originalNotesText: null,
      interpretedAmount: 2,
      canonicalUnit: 'onion',
      unresolvedReason: null,
      recipeId: origin === 'recipe' ? 'recipe' : null,
      recipeStepId: null,
      recipeStepItemId: null,
      recipeNameSnapshot: origin === 'recipe' ? 'onion soup' : null,
    })),
  };
  render(
    <MemoryRouter>
      <GroceryItemList
        groups={[{ name: 'Uncategorised', items: [item] }]}
        onEdit={() => {}}
        onRemove={() => {}}
      />
    </MemoryRouter>,
  );

  const requirements = screen.queryAllByText('Requirement:');
  expect(requirements).toHaveLength(amount !== null && unit !== null ? 2 : 0);
  for (const requirement of requirements) {
    expect(requirement.parentElement).toHaveTextContent(`Requirement: ${amount} ${unit}`);
  }
  expect(screen.queryByText('No numeric total')).not.toBeInTheDocument();
  expect(screen.getAllByText('Choose firm onions.')).toHaveLength(2);
  expect(screen.queryByText('Recipe requirement:')).not.toBeInTheDocument();
  const hasRecipe = origins.some((origin) => origin === 'recipe');
  expect(screen.queryAllByText(`Sources (${origins.length})`)).toHaveLength(hasRecipe ? 2 : 0);
  expect(screen.queryAllByText(/Manual addition/))
    .toHaveLength(hasRecipe && origins.some((origin) => origin === 'manual') ? 2 : 0);
  expect(screen.queryAllByText('Onion Soup')).toHaveLength(hasRecipe ? 2 : 0);
});
