import { z } from 'zod';
import { apiFetch } from './apiClient';

const mappingStateSchema = z.enum(['mapped', 'notStocked', 'unmapped']);

const storeSummarySchema = z.object({
  storeCode: z.string(),
  state: mappingStateSchema,
  departmentName: z.string().nullable(),
});

const ingredientSummarySchema = z.object({
  id: z.uuid(),
  name: z.string(),
  categoryName: z.string().nullable(),
  excludeFromGroceryList: z.boolean(),
  archived: z.boolean(),
  stores: z.array(storeSummarySchema),
});

const ingredientListSchema = z.object({
  items: z.array(ingredientSummarySchema),
  totalCount: z.number().int().nonnegative(),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
});

const storeDetailSchema = z.object({
  storeId: z.uuid(),
  storeCode: z.string(),
  storeName: z.string(),
  state: mappingStateSchema,
  departmentId: z.uuid().nullable(),
  departmentName: z.string().nullable(),
});

const productLinkSchema = z.object({
  id: z.uuid(),
  chainCode: z.string(),
  chainName: z.string(),
  url: z.url(),
  variantLabel: z.string().nullable(),
  note: z.string().nullable(),
  supportsBothPricingMethods: z.boolean(),
  isPreferred: z.boolean(),
  position: z.number().int(),
});

const nutritionSchema = z.object({
  energyKcal: z.number().nullable(),
  proteinG: z.number().nullable(),
  carbohydrateG: z.number().nullable(),
  sugarG: z.number().nullable(),
  fatG: z.number().nullable(),
  saturatedFatG: z.number().nullable(),
  fibreG: z.number().nullable(),
  sodiumMg: z.number().nullable(),
});

const ingredientDetailSchema = z.object({
  id: z.uuid(),
  name: z.string(),
  categoryId: z.uuid().nullable(),
  categoryName: z.string().nullable(),
  excludeFromGroceryList: z.boolean(),
  nutrition: nutritionSchema.nullable(),
  nutritionSource: z.string().nullable(),
  nutritionUpdatedAtUtc: z.iso.datetime({ offset: true }).nullable(),
  createdAtUtc: z.iso.datetime({ offset: true }),
  updatedAtUtc: z.iso.datetime({ offset: true }),
  archivedAtUtc: z.iso.datetime({ offset: true }).nullable(),
  version: z.number().int().nonnegative(),
  storeMappings: z.array(storeDetailSchema),
  productLinks: z.array(productLinkSchema),
});

const supermarketStoreSchema = z.object({
  code: z.string(),
  name: z.string(),
  displayOrder: z.number().int(),
});

const supermarketChainSchema = z.object({
  code: z.string(),
  name: z.string(),
  stores: z.array(supermarketStoreSchema),
});

const supermarketsSchema = z.array(supermarketChainSchema);

const problemDetailsSchema = z.object({
  title: z.string().optional(),
  detail: z.string().optional(),
  status: z.number().optional(),
});

export type IngredientMappingState = z.infer<typeof mappingStateSchema>;
export type IngredientSummary = z.infer<typeof ingredientSummarySchema>;
export type IngredientList = z.infer<typeof ingredientListSchema>;
export type IngredientDetail = z.infer<typeof ingredientDetailSchema>;
export type SupermarketChain = z.infer<typeof supermarketChainSchema>;
export type SupermarketStore = z.infer<typeof supermarketStoreSchema>;

export class IngredientsApiError extends Error {
  readonly status: number;

  constructor(
    message: string,
    status: number,
  ) {
    super(message);
    this.name = 'IngredientsApiError';
    this.status = status;
  }
}

async function getJson<T>(
  path: string,
  schema: z.ZodType<T>,
  signal?: AbortSignal,
): Promise<T> {
  let response: Response;
  try {
    response = await apiFetch(path, { method: 'GET', signal, requiresSession: true });
  } catch (error) {
    if (signal?.aborted) {
      throw error;
    }
    throw new IngredientsApiError('Unable to reach the ingredient service. Please try again.', 0);
  }

  if (!response.ok) {
    const parsedProblem = problemDetailsSchema.safeParse(await response.json().catch(() => null));
    const problem = parsedProblem.success ? parsedProblem.data : undefined;
    throw new IngredientsApiError(
      problem?.detail ?? problem?.title ?? 'The ingredient request failed. Please try again.',
      response.status,
    );
  }

  const payload: unknown = await response.json();
  const parsedResponse = schema.safeParse(payload);
  if (!parsedResponse.success) {
    throw new IngredientsApiError('The server returned an invalid ingredient response.', response.status);
  }

  return parsedResponse.data;
}

export function getIngredientList(
  parameters: {
    search: string;
    page: number;
    pageSize: number;
    archived: boolean;
  },
  signal?: AbortSignal,
): Promise<IngredientList> {
  const query = new URLSearchParams({
    page: String(parameters.page),
    pageSize: String(parameters.pageSize),
    archived: String(parameters.archived),
  });
  if (parameters.search.trim()) {
    query.set('search', parameters.search.trim());
  }
  return getJson(`/api/ingredients?${query}`, ingredientListSchema, signal);
}

export function getIngredient(id: string, signal?: AbortSignal): Promise<IngredientDetail> {
  return getJson(`/api/ingredients/${encodeURIComponent(id)}`, ingredientDetailSchema, signal);
}

export function getSupermarkets(signal?: AbortSignal): Promise<SupermarketChain[]> {
  return getJson('/api/supermarkets', supermarketsSchema, signal);
}
