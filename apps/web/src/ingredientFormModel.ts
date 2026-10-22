import type {
  IngredientDetail,
  IngredientWriteRequest,
  SupermarketChain,
  SupermarketStore,
} from './services/ingredientsApi';

export const nutritionFields = [
  ['energyKcal', 'Energy (kcal)'],
  ['proteinG', 'Protein (g)'],
  ['carbohydrateG', 'Carbohydrate (g)'],
  ['sugarG', 'Sugar (g)'],
  ['fatG', 'Fat (g)'],
  ['saturatedFatG', 'Saturated fat (g)'],
  ['fibreG', 'Fibre (g)'],
  ['sodiumMg', 'Sodium (mg)'],
] as const;

export type NutritionField = (typeof nutritionFields)[number][0];

export interface IngredientEditorState {
  name: string;
  categoryId: string | null;
  categoryName: string;
  excludeFromGroceryList: boolean;
  nutrition: Record<NutritionField, string>;
  nutritionSource: string;
  stores: Record<string, { departmentId: string | null; departmentName: string; isNotStocked: boolean }>;
  productLinks: Array<{
    chainId: string;
    url: string;
    variantLabel: string;
    note: string;
    supportsBothPricingMethods: boolean;
    isPreferred: boolean;
  }>;
  version: number;
}

export function createEmptyIngredient(stores: SupermarketStore[]): IngredientEditorState {
  return {
    name: '',
    categoryId: null,
    categoryName: '',
    excludeFromGroceryList: false,
    nutrition: Object.fromEntries(nutritionFields.map(([field]) => [field, ''])) as Record<NutritionField, string>,
    nutritionSource: '',
    stores: Object.fromEntries(stores.map((store) => [
      store.id,
      { departmentId: null, departmentName: '', isNotStocked: false },
    ])),
    productLinks: [],
    version: 0,
  };
}

export function ingredientToEditorState(
  ingredient: IngredientDetail,
  stores: SupermarketStore[],
  chains: SupermarketChain[],
): IngredientEditorState {
  const state = createEmptyIngredient(stores);
  for (const mapping of ingredient.storeMappings) {
    state.stores[mapping.storeId] = {
      departmentId: mapping.departmentId,
      departmentName: mapping.departmentName ?? '',
      isNotStocked: mapping.state === 'notStocked',
    };
  }
  return {
    ...state,
    name: ingredient.name,
    categoryId: ingredient.categoryId,
    categoryName: ingredient.categoryName ?? '',
    excludeFromGroceryList: ingredient.excludeFromGroceryList,
    nutrition: Object.fromEntries(nutritionFields.map(([field]) => [
      field,
      ingredient.nutrition?.[field] === null || ingredient.nutrition?.[field] === undefined
        ? ''
        : String(ingredient.nutrition[field]),
    ])) as Record<NutritionField, string>,
    nutritionSource: ingredient.nutritionSource ?? '',
    productLinks: ingredient.productLinks.map((link) => ({
      chainId: findChainId(link.chainCode, chains),
      url: link.url,
      variantLabel: link.variantLabel ?? '',
      note: link.note ?? '',
      supportsBothPricingMethods: link.supportsBothPricingMethods,
      isPreferred: link.isPreferred,
    })),
    version: ingredient.version,
  };
}

function findChainId(code: string, chains: SupermarketChain[]): string {
  return chains.find((chain) => chain.code === code)?.id ?? '';
}

export function ingredientWriteRequest(
  state: IngredientEditorState,
  stores: SupermarketStore[],
  chains: SupermarketChain[],
): { request: IngredientWriteRequest; errors: Record<string, string[]> } {
  const errors: Record<string, string[]> = {};
  const nutrition = Object.fromEntries(nutritionFields.map(([field]) => {
    const value = state.nutrition[field].trim();
    if (!value) {
      return [field, null];
    }
    const numberValue = Number(value);
    if (!Number.isFinite(numberValue)) {
      errors[`Nutrition.${field}`] = ['Enter a valid number.'];
      return [field, null];
    }
    return [field, numberValue];
  })) as IngredientWriteRequest['nutrition'];

  return {
    errors,
    request: {
      name: state.name,
      categoryId: state.categoryId,
      excludeFromGroceryList: state.excludeFromGroceryList,
      nutrition,
      nutritionSource: state.nutritionSource || null,
      storeMappings: stores.map((store) => ({
        storeId: store.id,
        departmentId: state.stores[store.id]?.departmentId ?? null,
        isNotStocked: state.stores[store.id]?.isNotStocked ?? false,
      })),
      productLinks: state.productLinks.map((link) => ({
        chainId: link.chainId || chains[0]?.id || '',
        url: link.url,
        variantLabel: link.variantLabel || null,
        note: link.note || null,
        supportsBothPricingMethods: link.supportsBothPricingMethods,
        isPreferred: link.isPreferred,
      })),
      version: state.version,
    },
  };
}
