import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { expect, test } from 'vitest';
import GroceryItemList from '../../../src/features/grocery-list/components/GroceryItemList';
import type { GroceryItem } from '../../../src/features/grocery-list/services/groceryListApi';

test.each([
  { name: 'manual-only', origins: ['manual'] as const, amount: 2, unit: 'onion' },
  { name: 'one generic item', origins: ['manual'] as const, amount: 1, unit: 'item' },
  { name: 'unitless recipe counts', origins: ['recipe', 'recipe'] as const,
    amount: 4, unit: 'item', sourceUnits: ['', ''], sourceTotal: '4 item(s)' },
  { name: 'hidden parenthesized cooking notes', origins: ['recipe', 'recipe'] as const,
    amount: 4, unit: 'item', sourceUnits: ['', ''], sourceNotes: '(cubed)', sourceTotal: '4 item(s)' },
  { name: 'hidden plain cooking notes', origins: ['recipe', 'recipe'] as const,
    amount: 4, unit: 'item', sourceUnits: ['', ''], sourceNotes: 'cubed', sourceTotal: '4 item(s)' },
  { name: 'Recipe-only', origins: ['recipe'] as const, amount: 2, unit: 'onion' },
  { name: 'mixed manual and Recipe', origins: ['manual', 'recipe'] as const, amount: 4, unit: 'onion' },
  { name: 'manual without amount', origins: ['manual'] as const, amount: null, unit: 'onion' },
  { name: 'Recipe without unit', origins: ['recipe'] as const, amount: 2, unit: null },
  { name: 'Recipe without a numeric total', origins: ['recipe'] as const, amount: null, unit: null },
  { name: 'same-unit recipe steps and repeat additions', origins: ['recipe', 'recipe', 'recipe'] as const,
    amount: 30, unit: 'ml', sourceUnits: ['tsp', 'tsp', 'tsp'], sourceTotal: '6 tsp' },
  { name: 'two maple syrup steps using the existing API response', origins: ['recipe', 'recipe'] as const,
    amount: 20, unit: 'ml', sourceUnits: ['tsp', 'tsp'], sourceTotal: '4 tsp' },
  { name: 'fractional same-unit recipe steps', origins: ['recipe', 'recipe'] as const,
    amount: 20, unit: 'ml', sourceUnits: ['tsp', 'tsp'], sourceAmounts: ['1/2', '3 1/2'], sourceTotal: '4 tsp' },
  { name: 'mixed-unit recipe steps', origins: ['recipe', 'recipe'] as const,
    amount: 40, unit: 'ml', sourceUnits: ['tsp', 'tbsp'], sourceTotal: '40 ml' },
])('shows useful requirements and Recipe Sources for $name on desktop and mobile', ({ origins, amount, unit, sourceUnits, sourceTotal, sourceAmounts, sourceNotes }) => {
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
    contributions: origins.map((origin, index) => ({
      id: `${origin}-${index}`,
      origin,
      state: 'outstanding',
      originalAmountText: sourceAmounts?.[index] ?? '2',
      originalUnitText: sourceUnits?.[index] ?? 'onion',
      originalNotesText: sourceNotes ?? null,
      interpretedAmount: unit === 'item' ? 2 : sourceUnits?.[index] === 'tbsp' ? 30 : sourceUnits ? 10 : 2,
      canonicalUnit: unit === 'item' ? 'item' : sourceUnits ? 'ml' : 'onion',
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
        onQuantityChange={() => {}}
        disabled={false}
      />
    </MemoryRouter>,
  );

  const requirements = screen.queryAllByText('Requirement:');
  expect(requirements).toHaveLength(amount !== null && unit !== null ? 2 : 0);
  for (const requirement of requirements) {
    expect(requirement.parentElement).toHaveTextContent(`Requirement: ${amount} ${unit === 'item' ? 'item(s)' : unit}`);
  }
  expect(screen.queryByText('No numeric total')).not.toBeInTheDocument();
  expect(screen.getAllByText('Choose firm onions.')).toHaveLength(2);
  if (sourceNotes) {
    expect(screen.queryByText(/cubed/)).not.toBeInTheDocument();
  }
  expect(screen.queryByText('Recipe requirement:')).not.toBeInTheDocument();
  const hasRecipe = origins.some((origin) => origin === 'recipe');
  const hasTotal = amount !== null && unit !== null;
  for (const requirement of requirements) {
    expect(requirement.parentElement?.tagName).toBe(hasRecipe ? 'SUMMARY' : 'P');
  }
  expect(screen.queryAllByText(`Sources (${origins.length})`))
    .toHaveLength(hasRecipe && !hasTotal ? 2 : 0);
  expect(screen.queryAllByText(/Manual addition/))
    .toHaveLength(hasRecipe && origins.some((origin) => origin === 'manual') ? 2 : 0);
  expect(screen.queryAllByText('Onion Soup')).toHaveLength(hasRecipe ? 2 : 0);
  if (sourceTotal) {
    for (const recipe of screen.getAllByText('Onion Soup')) {
      expect(recipe.parentElement).toHaveTextContent(`Onion Soup — ${sourceTotal}`);
    }
  }
});

test('shows distinct outstanding manual addition notes without a Sources accordion', () => {
  const manual = (id: string, note: string, state: 'outstanding' | 'cancelled' = 'outstanding') => ({
    id, origin: 'manual' as const, state, originalAmountText: '1', originalUnitText: 'item',
    originalNotesText: note, interpretedAmount: null, canonicalUnit: 'item', unresolvedReason: null,
    recipeId: null, recipeStepId: null, recipeStepItemId: null, recipeNameSnapshot: null,
  });
  const item: GroceryItem = {
    id: 'item', ingredientId: null, name: 'oats', state: 'outstanding', note: 'First note',
    dimension: 'count', countLabel: 'item', requirementAmount: null, requirementUnit: null,
    outstandingPurchaseQuantity: 2, purchaseUnit: 'item', ingredientVersion: null,
    adHocStoreId: null, adHocDepartmentId: null, adHocDepartmentName: null, storeMappings: [],
    contributions: [manual('1', 'First note'), manual('2', 'Second note'),
      manual('3', 'Second note'), manual('4', 'Cancelled note', 'cancelled')],
  };
  render(<MemoryRouter><GroceryItemList groups={[{ name: 'Uncategorised', items: [item] }]}
    onEdit={() => {}} onRemove={() => {}} onQuantityChange={() => {}} disabled={false} /></MemoryRouter>);
  expect(screen.getAllByText('First note')).toHaveLength(2);
  expect(screen.getAllByText('Second note')).toHaveLength(2);
  expect(screen.queryByText('Cancelled note')).not.toBeInTheDocument();
  expect(screen.queryByText(/Sources/)).not.toBeInTheDocument();
});
