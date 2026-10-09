import { z } from 'zod';

const userDtoSchema = z.object({
  id: z.uuid(),
  email: z.string().email(),
  createdAtUtc: z.iso.datetime(),
});

const problemDetailsSchema = z.object({
  title: z.string().optional(),
  detail: z.string().optional(),
  status: z.number().optional(),
  errors: z.record(z.string(), z.array(z.string())).optional(),
});

export type UserDto = z.infer<typeof userDtoSchema>;
export type AuthField = 'email' | 'password';
export type AuthFieldErrors = Partial<Record<AuthField, string>>;

export class AuthApiError extends Error {
  readonly status: number;
  readonly fieldErrors?: AuthFieldErrors;

  constructor(
    message: string,
    status: number,
    fieldErrors?: AuthFieldErrors,
  ) {
    super(message);
    this.name = 'AuthApiError';
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

function getFieldErrors(errors: Record<string, string[]> | undefined): AuthFieldErrors | undefined {
  if (!errors) {
    return undefined;
  }

  const emailError = errors.Email?.[0] ?? errors.email?.[0];
  const passwordError = errors.Password?.[0] ?? errors.password?.[0];
  const fieldErrors: AuthFieldErrors = {};

  if (emailError) {
    fieldErrors.email = emailError;
  }
  if (passwordError) {
    fieldErrors.password = passwordError;
  }

  return Object.keys(fieldErrors).length > 0 ? fieldErrors : undefined;
}

async function sendJson(path: string, method: 'GET' | 'POST', body?: object): Promise<Response> {
  let response: Response;

  try {
    response = await fetch(path, {
      method,
      credentials: 'same-origin',
      ...(body === undefined
        ? {}
        : {
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
          }),
    });
  } catch {
    throw new AuthApiError('The authentication service is unavailable. Please try again.', 0);
  }

  return response;
}

async function readApiError(response: Response, fallback: string): Promise<AuthApiError> {
  const problemResult = problemDetailsSchema.safeParse(await response.json().catch(() => null));
  if (!problemResult.success) {
    return new AuthApiError(fallback, response.status);
  }

  const problem = problemResult.data;
  return new AuthApiError(
    problem.title ?? problem.detail ?? fallback,
    problem.status ?? response.status,
    getFieldErrors(problem.errors),
  );
}

async function readUser(response: Response): Promise<UserDto> {
  const userResult = userDtoSchema.safeParse(await response.json().catch(() => null));
  if (!userResult.success) {
    throw new AuthApiError('The server returned an invalid user response.', response.status);
  }

  return userResult.data;
}

export async function registerAccount(email: string, password: string): Promise<UserDto> {
  const response = await sendJson('/api/auth/register', 'POST', { email, password });
  if (!response.ok) {
    throw await readApiError(response, 'Registration failed. Please try again.');
  }

  return readUser(response);
}

export async function loginAccount(email: string, password: string): Promise<UserDto> {
  const response = await sendJson('/api/auth/login', 'POST', { email, password });
  if (!response.ok) {
    throw await readApiError(response, 'Invalid email or password');
  }

  return readUser(response);
}

export async function getCurrentUser(): Promise<UserDto | null> {
  const response = await sendJson('/api/auth/me', 'GET');
  if (response.status === 401) {
    return null;
  }
  if (!response.ok) {
    throw await readApiError(response, 'Unable to check the current session.');
  }

  return readUser(response);
}

export async function logout(): Promise<void> {
  const response = await sendJson('/api/auth/logout', 'POST');
  if (!response.ok) {
    throw await readApiError(response, 'Unable to log out. Please try again.');
  }
}
