import { z } from 'zod';
import { apiFetch } from '../../../shared/services/apiClient.js';

const itemStateSchema = z.enum(['outstanding', 'completed', 'cancelled']);
const contributionOriginSchema = z.enum(['manual', 'recipe']);
const contributionStateSchema = z.enum(['outstanding', 'completed', 'cancelled']);
const eventKindSchema = z.enum(['purchase', 'reversal']);
const purchaseOriginSchema = z.enum(['manual', 'onlineCart']);
const quantityDimensionSchema = z.enum(['count', 'mass', 'volume']).nullable();

const storeMappingSchema = z.object({
  storeId: z.uuid(),
  state: z.enum(['mapped', 'notStocked', 'unmapped']),
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
  adHocStoreId: z.uuid().nullable(),
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
});

export type GroceryItem = z.infer<typeof groceryItemSchema>;
export type GroceryList = z.infer<typeof groceryListSchema>;
export type GroceryHistory = z.infer<typeof groceryHistorySchema>;
export type GroceryHistoryEvent = z.infer<typeof historyEventSchema>;

export class GroceryListApiError extends Error {
  readonly status: number;
  readonly fieldErrors: Record<string, string[]>;

  constructor(message: string, status: number, fieldErrors: Record<string, string[]> = {}) {
    super(message);
    this.name = 'GroceryListApiError';
    this.status = status;
    this.fieldErrors = fieldErrors;
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
