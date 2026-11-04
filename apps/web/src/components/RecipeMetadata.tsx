import type { RecipeDetail } from '../services/recipesApi';

function safeSourceUrl(value: string): string | null {
  if (value !== value.trim()) {
    return null;
  }

  try {
    const url = new URL(value);
    if ((url.protocol !== 'http:' && url.protocol !== 'https:')
      || !url.hostname
      || url.username
      || url.password) {
      return null;
    }
    return url.href;
  } catch {
    return null;
  }
}

export function RecipeSource({ value }: { value: string | null }) {
  if (!value) {
    return null;
  }
  const sourceUrl = safeSourceUrl(value);

  return (
    <p className="recipe-source">
      {sourceUrl
        ? (
          <a href={sourceUrl} target="_blank" rel="noopener noreferrer">
            {value}
          </a>
        )
        : (
          <>
            <span>{value}</span>
            <span className="recipe-source-unavailable"> (Link unavailable)</span>
          </>
        )}
    </p>
  );
}

export function RecipeMetadata({ recipe }: { recipe: RecipeDetail }) {
  const addedDate = recipe.dateAddedAtUtc
    ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeZone: 'UTC' })
      .format(new Date(recipe.dateAddedAtUtc))
    : null;
  const entries: Array<[string, string | null]> = [
    ['Source', recipe.sourceText],
    ['Servings', recipe.servingsText],
    ['Prep time', recipe.prepTimeText],
    ['Cook time', recipe.cookTimeText],
    ['Date added', addedDate],
  ];

  return (
    <dl className="recipe-metadata">
      {entries
        .filter((entry): entry is [string, string] => entry[1] !== null && entry[1] !== '')
        .map(([label, value]) => (
          <div key={label}>
            <dt>{label}</dt>
            <dd>{value}</dd>
          </div>
        ))}
    </dl>
  );
}
