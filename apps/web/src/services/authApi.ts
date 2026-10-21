import { z } from 'zod';
import { apiFetch } from './apiClient';

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

const serviceUnavailableMessage = 'The authentication service is unavailable. Please try again.';

export type UserDto = z.infer<typeof userDtoSchema>;
export type AuthField = 'email' | 'password' | 'newPassword';
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
  const newPasswordError = errors.NewPassword?.[0] ?? errors.newPassword?.[0];
  const fieldErrors: AuthFieldErrors = {};

  if (newPasswordError) {
    fieldErrors.newPassword = newPasswordError;
  }
  if (emailError) {
    fieldErrors.email = emailError;
  }
  if (passwordError) {
    fieldErrors.password = passwordError;
  }

  return Object.keys(fieldErrors).length > 0 ? fieldErrors : undefined;
}

async function sendJson(
  path: string,
  method: 'GET' | 'POST',
  { body, requiresSession = false }: { body?: object; requiresSession?: boolean } = {},
): Promise<Response> {
  let response: Response;

  try {
    response = await apiFetch(path, {
      method,
      requiresSession,
      ...(body === undefined
        ? {}
        : {
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
          }),
    });
  } catch {
    throw new AuthApiError(serviceUnavailableMessage, 0);
  }

  return response;
}

async function readApiError(response: Response, fallback: string): Promise<AuthApiError> {
  if (response.status >= 500) {
    return new AuthApiError(serviceUnavailableMessage, response.status);
  }

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
  const response = await sendJson('/api/auth/register', 'POST', { body: { email, password } });
  if (!response.ok) {
    throw await readApiError(response, 'Registration failed. Please try again.');
  }

  return readUser(response);
}

export async function loginAccount(email: string, password: string): Promise<UserDto> {
  const response = await sendJson('/api/auth/login', 'POST', { body: { email, password } });
  if (!response.ok) {
    throw await readApiError(response, 'Invalid email or password');
  }

  return readUser(response);
}

export async function getCurrentUser(): Promise<UserDto | null> {
  const response = await sendJson('/api/auth/me', 'GET', { requiresSession: true });
  if (response.status === 401) {
    return null;
  }
  if (!response.ok) {
    throw await readApiError(response, 'Unable to check the current session.');
  }

  return readUser(response);
}

export async function logout(): Promise<void> {
  const response = await sendJson('/api/auth/logout', 'POST', { requiresSession: true });
  if (!response.ok) {
    throw await readApiError(response, 'Unable to log out. Please try again.');
  }
}

export async function requestPasswordReset(email: string): Promise<void> {
  const response = await sendJson('/api/auth/forgot-password', 'POST', { body: { email } });
  if (!response.ok) {
    throw await readApiError(response, 'Unable to send the reset link. Please try again.');
  }
}

export async function resetPassword(token: string, newPassword: string): Promise<void> {
  const response = await sendJson('/api/auth/reset-password', 'POST', { body: { token, newPassword } });
  if (!response.ok) {
    throw await readApiError(response, 'This reset link is invalid or has expired.');
  }
}
