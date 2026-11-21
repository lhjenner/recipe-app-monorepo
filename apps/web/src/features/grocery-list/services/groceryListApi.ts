import { z } from 'zod';
import { apiFetch } from '../../../shared/services/apiClient.js';

const itemStateSchema = z.enum(['outstanding', 'completed', 'cancelled']);
const contributionOriginSchema = z.enum(['manual', 'recipe']);
const contributionStateSchema = z.enum(['outstanding', 'completed', 'cancelled']);
const eventKindSchema = z.enum(['purchase', 'reversal']);
const purchaseOriginSchema = z.enum(['manual', 'onlineCart']);
const interpretationStatusSchema = z.enum(['known', 'unresolved']);
const quantityDimensionSchema = z.enum(['count', 'mass', 'volume']).nullable();

const storeMappingSchema = z.object({
  storeId: z.uuid(),
  state: z.enum(['mapped', 'notStocked', 'unmapped']),
  departmentId: z.uuid().nullable(),
  departmentName: z.string().nullable(),
});

const contributionSchema = z.object({
  id: z.uuid(),
  origin: contributionOriginSchema,
  state: contributionStateSchema,
  originalAmountText: z.string().nullable(),
  originalUnitText: z.string().nullable(),
  originalNotesText: z.string().nullable(),
  interpretedAmount: z.number().nullable(),
  canonicalUnit: z.string().nullable(),
  unresolvedReason: z.string().nullable(),
  recipeId: z.uuid().nullable(),
  recipeStepId: z.uuid().nullable(),
  recipeStepItemId: z.uuid().nullable(),
  recipeNameSnapshot: z.string().nullable(),
});

const groceryItemSchema = z.object({
  id: z.uuid(),
  ingredientId: z.uuid().nullable(),
  name: z.string(),
  state: itemStateSchema,
  note: z.string().nullable(),
  dimension: quantityDimensionSchema,
  countLabel: z.string().nullable(),
  requirementAmount: z.number().nullable(),
  requirementUnit: z.string().nullable(),
  outstandingPurchaseQuantity: z.number(),
  purchaseUnit: z.string(),
  ingredientVersion: z.number().int().nonnegative().nullable(),
  adHocStoreId: z.uuid().nullable(),
  adHocDepartmentId: z.uuid().nullable(),
  adHocDepartmentName: z.string().nullable(),
  storeMappings: z.array(storeMappingSchema),
  contributions: z.array(contributionSchema),
});

const groceryListSchema = z.object({
  id: z.uuid().nullable(),
  version: z.number().int().nonnegative(),
  completedItemCount: z.number().int().nonnegative(),
  items: z.array(groceryItemSchema),
});

const historyEventSchema = z.object({
  id: z.uuid(),
  groceryItemId: z.uuid(),
  kind: eventKindSchema,
  quantity: z.number(),
  purchaseUnit: z.string(),
  occurredAtUtc: z.iso.datetime({ offset: true }),
  itemNameSnapshot: z.string(),
  noteSnapshot: z.string().nullable(),
  storeId: z.uuid().nullable(),
  storeName: z.string().nullable(),
  origin: purchaseOriginSchema,
  reversesEventId: z.uuid().nullable(),
  isReversed: z.boolean(),
});

const groceryHistorySchema = z.object({
  items: z.array(historyEventSchema),
  totalCount: z.number().int().nonnegative(),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
});

const problemDetailsSchema = z.object({
  title: z.string().optional(),
  detail: z.string().optional(),
  errors: z.record(z.string(), z.array(z.string())).optional(),
  duplicateReviews: z.array(z.object({
    entryIndex: z.number().int().nonnegative(),
    targetItemId: z.uuid().nullable(),
    targetEntryIndex: z.number().int().nonnegative().nullable(),
    existingQuantity: z.number(),
    increment: z.number(),
    resultingQuantity: z.number(),
    itemName: z.string(),
    purchaseUnit: z.string(),
  })).optional(),
});
const commandReceiptSchema = z.object({
  commandId: z.uuid(),
  resultListVersion: z.number().int().nonnegative(),
  resultIds: z.array(z.uuid()),
});
const recipePreviewSchema = z.object({
  recipeId: z.uuid(),
  recipeName: z.string(),
  recipeVersion: z.number().int().nonnegative(),
  groceryListId: z.uuid().nullable(),
  listVersion: z.number().int().nonnegative(),
  hasOutstandingAddition: z.boolean(),
  included: z.array(z.object({
    recipeStepId: z.uuid(),
    stepPosition: z.number().int().nonnegative(),
    recipeStepItemId: z.uuid(),
    itemPosition: z.number().int().nonnegative(),
    ingredientId: z.uuid(),
    ingredientName: z.string(),
    ingredientIsArchived: z.boolean(),
    originalNameText: z.string(),
    originalAmountText: z.string().nullable(),
    originalUnitText: z.string().nullable(),
    originalNotesText: z.string().nullable(),
    interpretationStatus: interpretationStatusSchema,
    interpretedAmount: z.number().nullable(),
    dimension: quantityDimensionSchema,
    canonicalUnit: z.string().nullable(),
    countLabel: z.string().nullable(),
    unresolvedReason: z.string().nullable(),
  })),
  excluded: z.array(z.object({
    stepPosition: z.number().int().nonnegative(),
    itemPosition: z.number().int().nonnegative(),
    text: z.string(),
    reason: z.string(),
  })),
});

export type GroceryItem = z.infer<typeof groceryItemSchema>;
export type GroceryList = z.infer<typeof groceryListSchema>;
export type GroceryHistory = z.infer<typeof groceryHistorySchema>;
export type GroceryHistoryEvent = z.infer<typeof historyEventSchema>;
export type GroceryDuplicateReview =
  NonNullable<z.infer<typeof problemDetailsSchema>['duplicateReviews']>[number];
export type GroceryCommandReceipt = z.infer<typeof commandReceiptSchema>;
export type GroceryRecipePreview = z.infer<typeof recipePreviewSchema>;

export interface GroceryRecipeAddCommand {
  commandId: string;
  expectedListVersion: number;
  expectedRecipeVersion: number;
  confirmRepeat: boolean;
}

export interface GroceryRecipeRemovalCommand {
  commandId: string;
  expectedListVersion: number;
}

export interface GroceryManualEntryCommand {
  commandId: string;
  expectedListVersion: number;
  items: Array<{
    name: string;
    note: string | null;
    purchaseQuantity: number;
    purchaseUnit: string;
    ingredientId: string | null;
    saveAsIngredient: boolean;
    categoryId: string | null;
    storeMappings: Array<{
      storeId: string;
      departmentId: string;
        isNotStocked: boolean;
    }>;
  }>;
  reviewedDuplicates: GroceryDuplicateReview[];
}

export interface GroceryItemUpdateRequest {
  commandId: string;
  expectedListVersion: number;
  outstandingPurchaseQuantity: number;
  purchaseUnit: string;
  note: string | null;
  departmentUpdate: {
    storeId: string;
    departmentId: string | null;
    expectedIngredientVersion: number;
  } | null;
  adHocStoreId: string | null;
  adHocDepartmentId: string | null;
}

export class GroceryListApiError extends Error {
  readonly status: number;
  readonly fieldErrors: Record<string, string[]>;
  readonly duplicateReviews: GroceryDuplicateReview[];

  constructor(
    message: string,
    status: number,
    fieldErrors: Record<string, string[]> = {},
    duplicateReviews: GroceryDuplicateReview[] = [],
  ) {
    super(message);
    this.name = 'GroceryListApiError';
    this.status = status;
    this.fieldErrors = fieldErrors;
    this.duplicateReviews = duplicateReviews;
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
    throw new GroceryListApiError('Unable to reach the grocery service. Please try again.', 0);
  }

  if (!response.ok) {
    const parsedProblem = problemDetailsSchema.safeParse(await response.json().catch(() => null));
    const problem = parsedProblem.success ? parsedProblem.data : undefined;
    throw new GroceryListApiError(
      problem?.detail ?? problem?.title ?? 'The grocery request failed. Please try again.',
      response.status,
      problem?.errors,
    );
  }

  const parsedResponse = schema.safeParse(await response.json() as unknown);
  if (!parsedResponse.success) {
    throw new GroceryListApiError('The server returned an invalid grocery response.', response.status);
  }
  return parsedResponse.data;
}

export function getGroceryList(signal?: AbortSignal): Promise<GroceryList> {
  return getJson('/api/grocery-list', groceryListSchema, signal);
}

async function writeJson<T>(
  path: string,
  method: 'POST' | 'PUT' | 'DELETE',
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
    throw new GroceryListApiError('Unable to reach the grocery service. Please try again.', 0);
  }

  if (!response.ok) {
    const parsedProblem = problemDetailsSchema.safeParse(await response.json().catch(() => null));
    const problem = parsedProblem.success ? parsedProblem.data : undefined;
    throw new GroceryListApiError(
      problem?.detail ?? problem?.title ?? 'The grocery request failed. Please try again.',
      response.status,
      problem?.errors,
      problem?.duplicateReviews,
    );
  }

  const parsedResponse = schema.safeParse(await response.json() as unknown);
  if (!parsedResponse.success) {
    throw new GroceryListApiError('The server returned an invalid grocery response.', response.status);
  }
  return parsedResponse.data;
}

export function addGroceryItems(
  command: GroceryManualEntryCommand,
): Promise<GroceryCommandReceipt> {
  return writeJson('/api/grocery-list/items', 'POST', command, commandReceiptSchema);
}

export function getRecipeGroceryPreview(
  recipeId: string,
  signal?: AbortSignal,
): Promise<GroceryRecipePreview> {
  if (!z.uuid().safeParse(recipeId).success) {
    return Promise.reject(new GroceryListApiError('Recipe ID must be a UUID.', 0));
  }
  return getJson(
    `/api/grocery-list/recipes/${encodeURIComponent(recipeId)}/preview`,
    recipePreviewSchema,
    signal,
  );
}

export function addRecipeToGroceryList(
  recipeId: string,
  command: GroceryRecipeAddCommand,
): Promise<GroceryCommandReceipt> {
  return writeJson(
    `/api/grocery-list/recipes/${encodeURIComponent(recipeId)}`,
    'POST',
    command,
    commandReceiptSchema,
  );
}

export function removeRecipeGroceryRequirements(
  recipeId: string,
  command: GroceryRecipeRemovalCommand,
): Promise<GroceryCommandReceipt> {
  return writeJson(
    `/api/grocery-list/recipes/${encodeURIComponent(recipeId)}/contributions`,
    'DELETE',
    command,
    commandReceiptSchema,
  );
}

export function updateGroceryItem(
  itemId: string,
  request: GroceryItemUpdateRequest,
): Promise<GroceryCommandReceipt> {
  return writeJson(
    `/api/grocery-list/items/${encodeURIComponent(itemId)}`,
    'PUT',
    request,
    commandReceiptSchema,
  );
}

export async function cancelGroceryItem(
  itemId: string,
  commandId: string,
  expectedListVersion: number,
): Promise<void> {
  let response: Response;
  try {
    response = await apiFetch(`/api/grocery-list/items/${encodeURIComponent(itemId)}`, {
      method: 'DELETE',
      headers: {
        'X-Command-Id': commandId,
        'X-Expected-List-Version': String(expectedListVersion),
      },
      requiresSession: true,
    });
  } catch {
    throw new GroceryListApiError('Unable to reach the grocery service. Please try again.', 0);
  }

  if (response.status === 204) {
    return;
  }

  const parsedProblem = problemDetailsSchema.safeParse(await response.json().catch(() => null));
  const problem = parsedProblem.success ? parsedProblem.data : undefined;
  throw new GroceryListApiError(
    problem?.detail ?? problem?.title ?? 'The grocery request failed. Please try again.',
    response.status,
    problem?.errors,
    problem?.duplicateReviews,
  );
}

export interface GroceryHistoryQuery {
  allDates: boolean;
  from: string;
  to: string;
  page: number;
  pageSize: number;
}

export function getGroceryHistory(
  query: GroceryHistoryQuery,
  signal?: AbortSignal,
): Promise<GroceryHistory> {
  const parameters = new URLSearchParams({
    allDates: String(query.allDates),
    page: String(query.page),
    pageSize: String(query.pageSize),
  });
  if (!query.allDates) {
    parameters.set('from', query.from);
    parameters.set('to', query.to);
  }
  return getJson(`/api/grocery-list/purchase-history?${parameters}`, groceryHistorySchema, signal);
}
