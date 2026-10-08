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
export type RegistrationField = 'email' | 'password';
export type RegistrationFieldErrors = Partial<Record<RegistrationField, string>>;

export class RegistrationApiError extends Error {
  readonly status: number;
  readonly fieldErrors?: RegistrationFieldErrors;

  constructor(
    message: string,
    status: number,
    fieldErrors?: RegistrationFieldErrors,
  ) {
    super(message);
    this.name = 'RegistrationApiError';
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

function getFieldErrors(errors: Record<string, string[]> | undefined): RegistrationFieldErrors | undefined {
  if (!errors) {
    return undefined;
  }

  const emailError = errors.Email?.[0] ?? errors.email?.[0];
  const passwordError = errors.Password?.[0] ?? errors.password?.[0];
  const fieldErrors: RegistrationFieldErrors = {};

  if (emailError) {
    fieldErrors.email = emailError;
  }
  if (passwordError) {
    fieldErrors.password = passwordError;
  }

  return Object.keys(fieldErrors).length > 0 ? fieldErrors : undefined;
}

export async function registerAccount(email: string, password: string): Promise<UserDto> {
  let response: Response;

  try {
    response = await fetch('/api/auth/register', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    });
  } catch {
    throw new RegistrationApiError(
      'The registration service is unavailable. Please try again.',
      0,
    );
  }

  if (!response.ok) {
    const problemResult = problemDetailsSchema.safeParse(await response.json().catch(() => null));
    if (!problemResult.success) {
      throw new RegistrationApiError('Registration failed. Please try again.', response.status);
    }

    const problem = problemResult.data;
    throw new RegistrationApiError(
      problem.title ?? problem.detail ?? 'Registration failed. Please try again.',
      problem.status ?? response.status,
      getFieldErrors(problem.errors),
    );
  }

  const userResult = userDtoSchema.safeParse(await response.json().catch(() => null));
  if (!userResult.success) {
    throw new RegistrationApiError('The server returned an invalid registration response.', response.status);
  }

  return userResult.data;
}
