import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { expect, test } from 'vitest';
import GroceryItemList from '../../../src/features/grocery-list/components/GroceryItemList';
import type { GroceryItem } from '../../../src/features/grocery-list/services/groceryListApi';

test.each([
  { name: 'manual-only', origins: ['manual'] as const },
  { name: 'mixed manual and Recipe', origins: ['manual', 'recipe'] as const },
])('uses a neutral requirement label for $name demand on desktop and mobile', ({ origins }) => {
  const item: GroceryItem = {
    id: 'item',
    ingredientId: null,
    name: 'onions',
    state: 'outstanding',
    note: null,
    dimension: 'count',
    countLabel: 'onion',
    requirementAmount: origins.length * 2,
    requirementUnit: 'onion',
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

  const requirements = screen.getAllByText('Requirement:');
  expect(requirements).toHaveLength(2);
  for (const requirement of requirements) {
    expect(requirement.parentElement).toHaveTextContent(`Requirement: ${origins.length * 2} onion`);
  }
  expect(screen.queryByText('Recipe requirement:')).not.toBeInTheDocument();
  expect(screen.getAllByText(/Manual addition/)).toHaveLength(2);
});
