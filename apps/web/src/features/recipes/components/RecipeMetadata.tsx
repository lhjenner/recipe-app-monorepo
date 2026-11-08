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
            View original recipe source
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
  const entries: Array<[string, string, string | null]> = [
    ['source', 'Source', recipe.sourceText],
    ['servings', 'Servings', recipe.servingsText],
    ['prep', 'Prep time', recipe.prepTimeText],
    ['cook', 'Cook time', recipe.cookTimeText],
    ['date', 'Date added', addedDate],
  ];

  return (
    <dl className="recipe-metadata">
      {entries
        .filter((entry): entry is [string, string, string] => entry[2] !== null && entry[2] !== '')
        .map(([key, label, value]) => (
          <div key={key} className={`recipe-metadata-${key}`}>
            <dt>{label}</dt>
            <dd>{value}</dd>
          </div>
        ))}
    </dl>
  );
}
