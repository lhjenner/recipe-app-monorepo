import { toTitleCase } from '../../../shared/utils/titleCase';
import type { GroceryItem } from '../services/groceryListApi';

export interface GroceryItemGroup {
  name: string;
  items: GroceryItem[];
}

export function groupGroceryItems(
  items: GroceryItem[],
  selectedStoreId: string | null,
): GroceryItemGroup[] {
  const groupedItems = new Map<string, GroceryItem[]>();

  items
    .filter((item) => item.state === 'outstanding')
    .forEach((item) => {
      const groupName = getGroupName(item, selectedStoreId);
      const groupItems = groupedItems.get(groupName) ?? [];
      groupItems.push(item);
      groupedItems.set(groupName, groupItems);
    });

  return [...groupedItems.entries()]
    .sort(([first], [second]) => first.localeCompare(second))
    .map(([name, groupItems]) => ({
      name,
      items: groupItems.sort((first, second) =>
        toTitleCase(first.name).localeCompare(toTitleCase(second.name))
        || first.id.localeCompare(second.id)),
    }));
}

function getGroupName(item: GroceryItem, selectedStoreId: string | null): string {
  if (selectedStoreId === null) {
    return 'Uncategorised';
  }

  const mapping = item.storeMappings.find((entry) => entry.storeId === selectedStoreId);
  if (mapping?.state === 'notStocked') {
    return 'Not stocked here';
  }
  if (mapping?.state === 'mapped' && mapping.departmentName) {
    return toTitleCase(mapping.departmentName);
  }
  if (item.ingredientId === null
    && item.adHocStoreId === selectedStoreId
    && item.adHocDepartmentName) {
    return toTitleCase(item.adHocDepartmentName);
  }
  return 'Uncategorised';
}
