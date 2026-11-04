import { z } from 'zod';
import { apiFetch } from './apiClient';

const recipeIdSchema = z.uuid();

export function isRecipeId(value: string): boolean {
  return recipeIdSchema.safeParse(value).success;
}

const recipeListSchema = z.object({
  items: z.array(z.object({
    id: recipeIdSchema,
    name: z.string(),
  })),
  totalCount: z.number().int().nonnegative(),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
});

const recipeStepItemKindSchema = z.enum(['ingredient', 'heading', 'note']);

const recipeDetailSchema = z.object({
  id: recipeIdSchema,
  name: z.string(),
  sourceText: z.string().nullable(),
  sourceUrl: z.string().nullable(),
  servingsText: z.string().nullable(),
  prepTimeText: z.string().nullable(),
  cookTimeText: z.string().nullable(),
  dateAddedAtUtc: z.iso.datetime({ offset: true }).nullable(),
  tags: z.array(z.object({
    position: z.number().int().nonnegative(),
    text: z.string(),
  })),
  steps: z.array(z.object({
    position: z.number().int().nonnegative(),
    items: z.array(z.object({
      position: z.number().int().nonnegative(),
      kind: recipeStepItemKindSchema,
      text: z.string(),
      ingredientName: z.string().nullable(),
      quantityText: z.string().nullable(),
      unitText: z.string().nullable(),
      notesText: z.string().nullable(),
    })),
    instructions: z.string(),
  })),
  image: z.object({
    url: z.string(),
    width: z.number().int().positive(),
    height: z.number().int().positive(),
    sha256: z.string().regex(/^[0-9a-f]{64}$/i),
  }).nullable(),
}).superRefine((recipe, context) => {
  if (recipe.image && recipe.image.url !== `/api/recipes/${recipe.id}/image`) {
    context.addIssue({
      code: 'custom',
      path: ['image', 'url'],
      message: 'Recipe image URL must be its authenticated application endpoint.',
    });
  }
});

const problemDetailsSchema = z.object({
  title: z.string().optional(),
  detail: z.string().optional(),
  status: z.number().optional(),
  errors: z.record(z.string(), z.array(z.string())).optional(),
});

export type RecipeList = z.infer<typeof recipeListSchema>;
export type RecipeSummary = RecipeList['items'][number];
export type RecipeStepItemKind = z.infer<typeof recipeStepItemKindSchema>;
export type RecipeDetail = z.infer<typeof recipeDetailSchema>;
export type RecipeImageMetadata = NonNullable<RecipeDetail['image']>;

export class RecipeApiError extends Error {
  readonly status: number;
  readonly fieldErrors: Record<string, string[]>;

  constructor(message: string, status: number, fieldErrors: Record<string, string[]> = {}) {
    super(message);
    this.name = 'RecipeApiError';
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

function invalidResponse(status: number): RecipeApiError {
  return new RecipeApiError('The server returned an invalid recipe response.', status);
}

async function responseProblem(response: Response): Promise<z.infer<typeof problemDetailsSchema> | undefined> {
  const payload: unknown = await response.json().catch(() => undefined);
  const parsed = problemDetailsSchema.safeParse(payload);
  return parsed.success ? parsed.data : undefined;
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
    throw new RecipeApiError('Unable to reach the recipe service. Please try again.', 0);
  }

  if (!response.ok) {
    const problem = await responseProblem(response);
    throw new RecipeApiError(
      problem?.detail ?? problem?.title ?? 'The recipe request failed. Please try again.',
      response.status,
      problem?.errors,
    );
  }

  const payload: unknown = await response.json().catch(() => undefined);
  const parsed = schema.safeParse(payload);
  if (!parsed.success) {
    throw invalidResponse(response.status);
  }
  return parsed.data;
}

export function getRecipeList(
  parameters: { search: string; page: number; pageSize: number },
  signal?: AbortSignal,
): Promise<RecipeList> {
  const query = new URLSearchParams({
    page: String(parameters.page),
    pageSize: String(parameters.pageSize),
  });
  if (parameters.search.trim()) {
    query.set('search', parameters.search.trim());
  }
  return getJson(`/api/recipes?${query}`, recipeListSchema, signal);
}

export function getRecipe(id: string, signal?: AbortSignal): Promise<RecipeDetail> {
  if (!isRecipeId(id)) {
    return Promise.reject(new RecipeApiError('Recipe ID must be a UUID.', 0));
  }
  return getJson(`/api/recipes/${encodeURIComponent(id)}`, recipeDetailSchema, signal);
}

const recipeImagePathSchema = z.string().regex(
  /^\/api\/recipes\/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\/image$/i,
);
const imageContentTypes = new Set(['image/png', 'image/jpeg', 'image/gif', 'image/webp']);

export async function getRecipeImage(path: string, signal?: AbortSignal): Promise<Blob> {
  if (!recipeImagePathSchema.safeParse(path).success) {
    throw new RecipeApiError('Recipe image URL must be a same-origin application endpoint.', 0);
  }

  let response: Response;
  try {
    response = await apiFetch(path, { method: 'GET', signal, requiresSession: true });
  } catch (error) {
    if (signal?.aborted) {
      throw error;
    }
    throw new RecipeApiError('Unable to reach the recipe image service. Please try again.', 0);
  }

  if (!response.ok) {
    const problem = await responseProblem(response);
    throw new RecipeApiError(
      problem?.detail ?? problem?.title ?? 'The recipe image request failed. Please try again.',
      response.status,
    );
  }

  const contentType = response.headers.get('Content-Type')?.split(';', 1)[0]?.trim().toLowerCase();
  if (!contentType || !imageContentTypes.has(contentType)) {
    throw invalidResponse(response.status);
  }

  const blob = await response.blob();
  if (blob.size === 0) {
    throw invalidResponse(response.status);
  }
  return blob;
}
