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
  id: z.uuid(),
  code: z.string(),
  name: z.string(),
  displayOrder: z.number().int(),
});

const supermarketChainSchema = z.object({
  id: z.uuid(),
  code: z.string(),
  name: z.string(),
  stores: z.array(supermarketStoreSchema),
});

const supermarketsSchema = z.array(supermarketChainSchema);

const problemDetailsSchema = z.object({
  title: z.string().optional(),
  detail: z.string().optional(),
  status: z.number().optional(),
  errors: z.record(z.string(), z.array(z.string())).optional(),
  archivedIngredientId: z.uuid().optional(),
});
const lookupSchema = z.object({ id: z.uuid(), name: z.string() });

export type IngredientMappingState = z.infer<typeof mappingStateSchema>;
export type IngredientSummary = z.infer<typeof ingredientSummarySchema>;
export type IngredientList = z.infer<typeof ingredientListSchema>;
export type IngredientDetail = z.infer<typeof ingredientDetailSchema>;
export type SupermarketChain = z.infer<typeof supermarketChainSchema>;
export type SupermarketStore = z.infer<typeof supermarketStoreSchema>;
export type LookupValue = z.infer<typeof lookupSchema>;
export interface IngredientWriteRequest {
  name: string;
  categoryId: string | null;
  excludeFromGroceryList: boolean;
  nutrition: IngredientDetail['nutrition'];
  nutritionSource: string | null;
  storeMappings: Array<{
    storeId: string;
    departmentId: string | null;
    isNotStocked: boolean;
  }>;
  productLinks: Array<{
    chainId: string;
    url: string;
    variantLabel: string | null;
    note: string | null;
    supportsBothPricingMethods: boolean;
    isPreferred: boolean;
  }>;
  version?: number;
}

export class IngredientsApiError extends Error {
  readonly status: number;
  readonly fieldErrors: Record<string, string[]>;
  readonly archivedIngredientId: string | undefined;

  constructor(
    message: string,
    status: number,
    fieldErrors: Record<string, string[]> = {},
    archivedIngredientId?: string,
  ) {
    super(message);
    this.name = 'IngredientsApiError';
    this.status = status;
    this.fieldErrors = fieldErrors;
    this.archivedIngredientId = archivedIngredientId;
  }
}

async function writeJson<T>(
  path: string,
  method: 'POST' | 'PUT',
  body: unknown,
  schema: z.ZodType<T>,
): Promise<T> {
  let response: Response;
  try {
    response = await apiFetch(path, {
      method,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
      requiresSession: true,
    });
  } catch {
    throw new IngredientsApiError('Unable to reach the ingredient service. Please try again.', 0);
  }

  if (!response.ok) {
    const parsedProblem = problemDetailsSchema.safeParse(await response.json().catch(() => null));
    const problem = parsedProblem.success ? parsedProblem.data : undefined;
    throw new IngredientsApiError(
      problem?.detail ?? problem?.title ?? 'The ingredient request failed. Please try again.',
      response.status,
      problem?.errors,
      problem?.archivedIngredientId,
    );
  }

  const payload: unknown = await response.json();
  const parsedResponse = schema.safeParse(payload);
  if (!parsedResponse.success) {
    throw new IngredientsApiError('The server returned an invalid ingredient response.', response.status);
  }
  return parsedResponse.data;
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

export function getCategories(signal?: AbortSignal): Promise<LookupValue[]> {
  return getJson('/api/categories', z.array(lookupSchema), signal);
}

export function getDepartments(signal?: AbortSignal): Promise<LookupValue[]> {
  return getJson('/api/departments', z.array(lookupSchema), signal);
}

export function createCategory(name: string): Promise<LookupValue> {
  return writeJson('/api/categories', 'POST', { name }, lookupSchema);
}

export function createDepartment(name: string): Promise<LookupValue> {
  return writeJson('/api/departments', 'POST', { name }, lookupSchema);
}

export function createIngredient(request: IngredientWriteRequest): Promise<IngredientDetail> {
  return writeJson('/api/ingredients', 'POST', request, ingredientDetailSchema);
}

export function updateIngredient(
  id: string,
  request: IngredientWriteRequest,
): Promise<IngredientDetail> {
  return writeJson(
    `/api/ingredients/${encodeURIComponent(id)}`,
    'PUT',
    request,
    ingredientDetailSchema,
  );
}
