import { afterEach, expect, test, vi } from 'vitest';
import { apiFetch, SESSION_EXPIRED_EVENT } from '../../src/services/apiClient';
import { getCurrentUser, loginAccount, logout } from '../../src/services/authApi';

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

test('announces a 401 from any protected API and preserves the response', async () => {
  const response = new Response(null, { status: 401 });
  const fetchMock = vi.fn().mockResolvedValue(response);
  vi.stubGlobal('fetch', fetchMock);
  const dispatch = vi.spyOn(window, 'dispatchEvent');

  expect(await apiFetch('/api/ingredients')).toBe(response);

  expect(dispatch).toHaveBeenCalledWith(expect.objectContaining({ type: SESSION_EXPIRED_EVENT }));
  expect(fetchMock).toHaveBeenCalledWith('/api/ingredients', { credentials: 'same-origin' });
});

test.each([200, 400, 403, 500])('does not announce session expiry for HTTP %s', async (status) => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status })));
  const dispatch = vi.spyOn(window, 'dispatchEvent');

  await apiFetch('/api/ingredients');

  expect(dispatch).not.toHaveBeenCalled();
});

test('passes network failures to the caller without announcing expiry', async () => {
  const error = new TypeError('Network unavailable');
  vi.stubGlobal('fetch', vi.fn().mockRejectedValue(error));
  const dispatch = vi.spyOn(window, 'dispatchEvent');

  await expect(apiFetch('/api/ingredients')).rejects.toBe(error);

  expect(dispatch).not.toHaveBeenCalled();
});

test('keeps invalid login credentials as an inline auth error, not session expiry', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 401 })));
  const dispatch = vi.spyOn(window, 'dispatchEvent');

  await expect(loginAccount('person@example.com', 'wrong-password')).rejects.toMatchObject({
    status: 401,
    message: 'Invalid email or password',
  });

  expect(dispatch).not.toHaveBeenCalled();
});

test('session checks and logout use centralized unauthorized handling', async () => {
  vi.stubGlobal('fetch', vi.fn().mockImplementation(() => Promise.resolve(new Response(null, { status: 401 }))));
  const dispatch = vi.spyOn(window, 'dispatchEvent');

  expect(await getCurrentUser()).toBeNull();
  await expect(logout()).rejects.toMatchObject({ status: 401 });

  expect(dispatch).toHaveBeenCalledTimes(2);
});
