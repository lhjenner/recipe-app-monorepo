const validationOrigin = 'https://recipe-app.invalid';

export function getSafeReturnTo(destination: string | null): string {
  if (!destination?.startsWith('/') || destination.startsWith('//') || destination.includes('\\')
    || Array.from(destination).some((character) => character.charCodeAt(0) <= 32 || character.charCodeAt(0) === 127)) {
    return '/home';
  }

  const url = new URL(destination, validationOrigin);
  if (url.origin !== validationOrigin || url.pathname.startsWith('//')) {
    return '/home';
  }

  return `${url.pathname}${url.search}${url.hash}`;
}
