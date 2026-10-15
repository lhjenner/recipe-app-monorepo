import type { Page, Route } from '@playwright/test';

export const testUser = {
  id: '3f2b8c1e-5d4a-4b6e-9c7d-1a2b3c4d5e6f',
  email: 'person@example.com',
  createdAtUtc: '2026-01-01T00:00:00Z',
};

export const validCredentials = { email: testUser.email, password: 'password123' };

// Resolves to true when the response should start a session.
export type LoginHandler = (route: Route) => Promise<boolean>;
// Resolves to true when the response should end a session.
export type LogoutHandler = (route: Route) => Promise<boolean>;

export interface AuthApiMock {
  loginRequests: { email: string; password: string }[];
  logoutRequestCount: number;
  setSession: (active: boolean) => void;
  setLoginHandler: (handler: LoginHandler) => void;
  setLogoutHandler: (handler: LogoutHandler) => void;
}

const problem = (status: number, title?: string) => ({
  status,
  contentType: 'application/problem+json',
  body: JSON.stringify({ status, ...(title ? { title } : {}) }),
});

export const loginSucceeds: LoginHandler = async (route) => {
  await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(testUser) });
  return true;
};

export const loginRejectsCredentials: LoginHandler = async (route) => {
  await route.fulfill(problem(401, 'Invalid email or password'));
  return false;
};

export const logoutSucceeds: LogoutHandler = async (route) => {
  await route.fulfill({ status: 204 });
  return true;
};

// A stateful, hermetic stand-in for /api/auth/* so browser journeys need neither the API nor a database.
export async function mockAuthApi(page: Page, options: { hasSession?: boolean } = {}): Promise<AuthApiMock> {
  let hasSession = options.hasSession ?? false;
  let loginHandler: LoginHandler = loginSucceeds;
  let logoutHandler: LogoutHandler = logoutSucceeds;

  const mock: AuthApiMock = {
    loginRequests: [],
    logoutRequestCount: 0,
    setSession: (active) => {
      hasSession = active;
    },
    setLoginHandler: (handler) => {
      loginHandler = handler;
    },
    setLogoutHandler: (handler) => {
      logoutHandler = handler;
    },
  };

  await page.route('**/api/auth/me', (route) =>
    hasSession
      ? route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(testUser) })
      : route.fulfill({ status: 401 }));

  await page.route('**/api/auth/login', async (route) => {
    mock.loginRequests.push(route.request().postDataJSON() as { email: string; password: string });
    if (await loginHandler(route)) {
      hasSession = true;
    }
  });

  await page.route('**/api/auth/logout', async (route) => {
    mock.logoutRequestCount += 1;
    if (await logoutHandler(route)) {
      hasSession = false;
    }
  });

  return mock;
}
