import { Link, useLocation, useParams } from 'react-router-dom';
import PageTitle from './PageTitle';
import { useRecipe, useRecipeImage } from './hooks/useRecipeData';
import { readListSearch } from './ingredientListLocation';
import { isRecipeId, type RecipeDetail } from './services/recipesApi';
import { toTitleCase } from './titleCase';
import { RecipeMetadata, RecipeSource } from './components/RecipeMetadata';

function RecipeStepItem({
  kind,
  text,
  ingredientName,
  quantityText,
  unitText,
  notesText,
}: RecipeDetail['steps'][number]['items'][number]) {
  const displayName = kind === 'ingredient' ? toTitleCase(ingredientName ?? text) : text;
  const quantityAndUnit = [quantityText, unitText].filter(Boolean).join(' ');

  if (kind === 'heading') {
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

  if (kind === 'note') {
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
  const name = item.kind === 'ingredient'
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

function RecipeImage({
  recipeName,
  image,
}: {
  recipeName: string;
  image: NonNullable<RecipeDetail['image']>;
}) {
  const imageState = useRecipeImage(image.url);

  return (
    <div
      className="recipe-image-frame"
      style={{ aspectRatio: `${image.width} / ${image.height}` }}
      aria-busy={imageState.isLoading}
    >
      {imageState.isLoading && (
        <p className="recipe-image-status" role="status">Loading recipe image…</p>
      )}
      {imageState.error && (
        <div className="recipe-image-error" role="alert">
          <p>Recipe image unavailable. {imageState.error}</p>
          <button
            className="btn btn-secondary"
            type="button"
            onClick={imageState.reload}
          >
            Retry image
          </button>
        </div>
      )}
      {imageState.url && (
        <img
          className="recipe-image"
          src={imageState.url}
          alt={`${recipeName} recipe`}
          width={image.width}
          height={image.height}
        />
      )}
    </div>
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
              <Link className="recipe-back-link" to={backToRecipes}>Back to recipes</Link>
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
              <div className="recipe-overview">
                <div className="recipe-heading">
                  <Link className="recipe-back-link" to={backToRecipes}>Back to recipes</Link>
                  <h1 id="recipe-detail-heading">{toTitleCase(details.name)}</h1>
                  <RecipeSource value={details.sourceUrl} />

                  {details.tags.length > 0 && (
                    <ul className="recipe-tags" aria-label="Recipe tags">
                      {[...details.tags]
                        .sort((first, second) => first.position - second.position)
                        .map((tag) => <li key={tag.position}>{tag.text}</li>)}
                    </ul>
                  )}
                </div>

                <RecipeMetadata recipe={details} />

                {details.image ? (
                  <RecipeImage
                    recipeName={toTitleCase(details.name)}
                    image={details.image}
                  />
                ) : (
                  <div className="recipe-image-frame" style={{ aspectRatio: '5 / 4' }}>
                    <div className="recipe-image-placeholder" role="img" aria-label="No recipe image">
                      <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true">
                        <rect x="3" y="3" width="18" height="18" rx="2" />
                        <circle cx="8" cy="8" r="1.5" />
                        <path d="m3 17 6-6 4 4 3-3 5 5" />
                      </svg>
                      <span>No recipe image</span>
                    </div>
                  </div>
                )}
              </div>

              <ol className="recipe-steps" aria-label="Recipe steps" role="list">
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
