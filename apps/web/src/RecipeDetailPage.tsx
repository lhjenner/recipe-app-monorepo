import { Link, useLocation, useParams } from 'react-router-dom';
import PageTitle from './PageTitle';
import { useRecipe } from './hooks/useRecipeData';
import { readListSearch } from './ingredientListLocation';
import { isRecipeId, type RecipeDetail } from './services/recipesApi';
import { toTitleCase } from './titleCase';

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

function RecipeStepItem({
  kind,
  text,
  ingredientName,
  quantityText,
  unitText,
  notesText,
}: RecipeDetail['steps'][number]['items'][number]) {
  const displayName = kind === 'Ingredient' ? toTitleCase(ingredientName ?? text) : text;
  const quantityAndUnit = [quantityText, unitText].filter(Boolean).join(' ');

  if (kind === 'Heading') {
    return (
      <div className="recipe-step-item recipe-step-heading">
        <h3>{text}</h3>
        {(quantityAndUnit || notesText) && (
          <p className="recipe-step-item-extra">
            {[quantityAndUnit, notesText].filter(Boolean).join(' · ')}
          </p>
        )}
      </div>
    );
  }

  if (kind === 'Note') {
    return (
      <div className="recipe-step-item recipe-step-note">
        <p>{text}</p>
        {(quantityAndUnit || notesText) && (
          <p className="recipe-step-item-extra">
            {[quantityAndUnit, notesText].filter(Boolean).join(' · ')}
          </p>
        )}
      </div>
    );
  }

  return (
    <p className="recipe-step-item recipe-step-ingredient">
      {quantityAndUnit && <span className="recipe-step-quantity">{quantityAndUnit} </span>}
      <span>{displayName}</span>
      {notesText && <span className="recipe-step-notes"> ({notesText})</span>}
    </p>
  );
}

function hasRenderableContent(item: RecipeDetail['steps'][number]['items'][number]): boolean {
  const name = item.kind === 'Ingredient'
    ? item.ingredientName ?? item.text
    : item.text;
  return Boolean(
    name.trim()
    || item.quantityText?.trim()
    || item.unitText?.trim()
    || item.notesText?.trim(),
  );
}

function RecipeStep({
  position,
  items,
  instructions,
}: RecipeDetail['steps'][number]) {
  const visibleItems = items.filter(hasRenderableContent);
  return (
    <li className="recipe-step">
      <h2 className="recipe-step-number">Step {position + 1}</h2>
      {visibleItems.length > 0 && (
        <div className="recipe-step-items">
          {visibleItems.map((item) => (
            <RecipeStepItem key={item.position} {...item} />
          ))}
        </div>
      )}
      {instructions.trim() && <p className="recipe-step-instructions">{instructions}</p>}
    </li>
  );
}

function metadataEntries(recipe: RecipeDetail): Array<[string, string | null]> {
  const addedDate = recipe.dateAddedAtUtc
    ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeZone: 'UTC' })
      .format(new Date(recipe.dateAddedAtUtc))
    : null;
  return [
    ['Source', recipe.sourceText],
    ['Source URL', recipe.sourceUrl],
    ['Servings', recipe.servingsText],
    ['Prep time', recipe.prepTimeText],
    ['Cook time', recipe.cookTimeText],
    ['Date added', addedDate],
  ];
}

function RecipeMetadata({ recipe }: { recipe: RecipeDetail }) {
  const sourceUrl = recipe.sourceUrl ? safeSourceUrl(recipe.sourceUrl) : null;
  const entries = metadataEntries(recipe)
    .filter((entry): entry is [string, string] => entry[1] !== null && entry[1] !== '');

  return (
    <dl className="recipe-metadata">
      {entries.map(([label, value]) => (
        <div key={label}>
          <dt>{label}</dt>
          <dd>
            {label !== 'Source URL'
              ? value
              : sourceUrl
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
          </dd>
        </div>
      ))}
    </dl>
  );
}

function RecipeDetailPage() {
  const { id } = useParams();
  const location = useLocation();
  const recipe = useRecipe(id && isRecipeId(id) ? id : null);
  const details = recipe.data;
  const backToRecipes = `/recipes${readListSearch(location.state)}`;
  const isNotFound = !id || !isRecipeId(id) || recipe.status === 404;
  const pageTitle = details
    ? toTitleCase(details.name)
    : isNotFound
      ? 'Recipe Not Found'
      : 'Recipe';

  return (
    <>
      <PageTitle title={pageTitle} />
      <main className="app-main">
        <section className="app-page recipe-detail-page" aria-labelledby="recipe-detail-heading">
          {recipe.isLoading && (
            <p className="recipe-detail-status" role="status">Loading recipe…</p>
          )}

          {isNotFound && (
            <>
              <h1 id="recipe-detail-heading">Recipe not found</h1>
              <Link to={backToRecipes}>Back to recipes</Link>
            </>
          )}

          {recipe.error && !isNotFound && (
            <div className="recipe-detail-error" role="alert">
              <p>{recipe.error}</p>
              <button className="btn btn-secondary" type="button" onClick={recipe.reload}>
                Retry recipe
              </button>
            </div>
          )}

          {details && (
            <>
              <Link className="recipe-back-link" to={backToRecipes}>Back to recipes</Link>
              <h1 id="recipe-detail-heading">{toTitleCase(details.name)}</h1>

              {details.tags.length > 0 && (
                <ul className="recipe-tags" aria-label="Recipe tags">
                  {[...details.tags]
                    .sort((first, second) => first.position - second.position)
                    .map((tag) => <li key={tag.position}>{tag.text}</li>)}
                </ul>
              )}

              <RecipeMetadata recipe={details} />

              <ol className="recipe-steps" aria-label="Recipe steps">
                {[...details.steps]
                  .sort((first, second) => first.position - second.position)
                  .map((step) => <RecipeStep key={step.position} {...step} />)}
              </ol>
            </>
          )}

          {!recipe.isLoading && !recipe.error && !details && !isNotFound && (
            <p className="recipe-detail-status" role="status">Recipe unavailable.</p>
          )}
        </section>
      </main>
    </>
  );
}

export default RecipeDetailPage;
