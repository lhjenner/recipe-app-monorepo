export const SESSION_EXPIRED_EVENT = 'recipe-app:session-expired';

interface ApiRequestOptions extends RequestInit {
  requiresSession?: boolean;
}

export async function apiFetch(
  path: string,
  { requiresSession = true, ...options }: ApiRequestOptions = {},
): Promise<Response> {
  const response = await fetch(path, { credentials: 'same-origin', ...options });

  if (requiresSession && response.status === 401) {
    window.dispatchEvent(new Event(SESSION_EXPIRED_EVENT));
  }

  return response;
}
