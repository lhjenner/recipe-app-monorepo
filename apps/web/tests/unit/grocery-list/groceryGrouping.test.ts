import { expect, test } from 'vitest';
import { groupGroceryItems } from '../../../src/features/grocery-list/models/groceryGrouping';
import type { GroceryItem } from '../../../src/features/grocery-list/services/groceryListApi';

const storeId = '00000000-0000-7000-8000-000000000001';

function item(
  id: string,
  name: string,
  storeMappings: GroceryItem['storeMappings'] = [],
): GroceryItem {
  return {
    id,
    ingredientId: null,
    name,
    note: null,
    state: 'outstanding',
    dimension: null,
    countLabel: null,
    requirementAmount: null,
    requirementUnit: null,
    outstandingPurchaseQuantity: 1,
    purchaseUnit: 'item',
    storeMappings,
    adHocStoreId: null,
    adHocDepartmentName: null,
    contributions: [],
  };
}

test('groups mapped, not-stocked, and unmapped items for the selected store', () => {
  const grouped = groupGroceryItems([
    item('00000000-0000-7000-8000-000000000003', 'zucchini', [
      { storeId, state: 'mapped', departmentName: 'Produce' },
    ]),
    item('00000000-0000-7000-8000-000000000002', 'pear', [
      { storeId, state: 'notStocked', departmentName: null },
    ]),
    item('00000000-0000-7000-8000-000000000001', 'apples'),
  ], storeId);

  expect(grouped.map((group) => group.name)).toEqual([
    'Not stocked here',
    'Produce',
    'Uncategorised',
  ]);
  expect(grouped[1].items.map((groceryItem) => groceryItem.name)).toEqual(['zucchini']);
});

test('uses stable item IDs to break case-insensitive name ties', () => {
  const grouped = groupGroceryItems([
    item('00000000-0000-7000-8000-000000000002', 'apples'),
    item('00000000-0000-7000-8000-000000000001', 'Apples'),
  ], null);

  expect(grouped[0].items.map((groceryItem) => groceryItem.id)).toEqual([
    '00000000-0000-7000-8000-000000000001',
    '00000000-0000-7000-8000-000000000002',
  ]);
});
