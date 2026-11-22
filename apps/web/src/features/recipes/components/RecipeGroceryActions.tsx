import { Link } from 'react-router-dom';
import ModalContainer from '../../../shared/components/ModalContainer';
import { toTitleCase } from '../../../shared/utils/titleCase';
import { useRecipeGroceryActions } from '../hooks/useRecipeGroceryActions';

interface RecipeGroceryActionsProps {
  recipeId: string;
  hasOutstandingAddition?: boolean;
  onChanged?: () => void;
}

function RecipeGroceryActions({ recipeId, hasOutstandingAddition = false, onChanged }: RecipeGroceryActionsProps) {
  const {
    isOpen,
    preview,
    isLoading,
    isSubmitting,
    error,
    notice,
    canRetry,
    needsFreshReview,
    mode,
    locked,
    loadPreview,
    openReview,
    addRecipe,
    removeRecipe,
    retryCommand,
    closeReview,
  } = useRecipeGroceryActions(recipeId, onChanged);
  const isRemoval = mode === 'remove';
  const isRepeat = !isRemoval && preview?.hasOutstandingAddition;
  const showConfirmationActions = Boolean(preview && !isLoading);

  return (
    <>
      <div className="recipe-grocery-actions">
        <button className="btn btn-primary" type="button" onClick={() => openReview()} disabled={locked}>
          Add to Grocery List
        </button>
        {hasOutstandingAddition && (
          <button className="btn btn-danger-secondary" type="button" onClick={() => openReview('remove')} disabled={locked}>
            Remove from Grocery List
          </button>
        )}
        {notice && (
          <p role="status">
            {notice} <Link to="/grocery-list">View Grocery List</Link>
          </p>
        )}
      </div>
      {isOpen && (
        <ModalContainer
          labelledBy="recipe-grocery-review-heading"
          className="grocery-entry-dialog"
          initialFocusSelector="button"
          locked={locked}
          onClose={closeReview}
        >
          <header className="grocery-entry-heading">
            <div>
              <h2 id="recipe-grocery-review-heading">
                {isRemoval ? 'Remove from Grocery List' : isRepeat ? 'Add Recipe again?' : 'Review Recipe grocery items'}
              </h2>
              <p>Recipe content and cooking progress will not be changed.</p>
            </div>
            {!showConfirmationActions && (
              <button className="btn btn-secondary" type="button" onClick={closeReview} disabled={locked}>
                {isRemoval ? 'Cancel' : 'Close'}
              </button>
            )}
          </header>

          {isLoading && <p role="status">Reviewing Recipe…</p>}
          {error && <p role="alert">{error}</p>}
          {canRetry && (
            <p role="status">
              The outcome is unknown. Retry the original command before changing this review.
              <button className="btn btn-secondary" type="button" onClick={retryCommand} disabled={isSubmitting}>
                Retry original command
              </button>
            </p>
          )}
          {needsFreshReview && (
            <button className="btn btn-secondary" type="button" onClick={() => void loadPreview()}>
              Review latest Recipe and Grocery List
            </button>
          )}
          {error && !preview && !needsFreshReview && (
            <button className="btn btn-secondary" type="button" onClick={() => void loadPreview()}>
              Retry Recipe review
            </button>
          )}
          {preview && !isLoading && (
            <>
              <h3>{toTitleCase(preview.recipeName)}</h3>
              {!isRemoval && !isRepeat && (preview.included.length === 0
                ? <p role="status">This Recipe has no eligible Ingredients. Nothing will be added.</p>
                : (
                  <section aria-label="Included Recipe Ingredients">
                    <details>
                      <summary><strong>Included ({preview.included.length})</strong></summary>
                      <ul>
                        {preview.included.map((entry) => (
                          <li key={entry.recipeStepItemId}>
                            <strong>{toTitleCase(entry.ingredientName)}</strong>
                            {entry.originalAmountText || entry.originalUnitText
                              ? ` — ${[entry.originalAmountText, entry.originalUnitText].filter(Boolean).join(' ')}`
                              : ' — quantity not specified'}
                            {entry.ingredientIsArchived && ' — archived Ingredient reference'}
                            {entry.interpretationStatus === 'unresolved'
                              && ` — Needs review: ${entry.unresolvedReason}`}
                          </li>
                        ))}
                      </ul>
                    </details>
                  </section>
                ))}
              {!isRemoval && !isRepeat && preview.excluded.length > 0 && (
                <section aria-label="Excluded Recipe items">
                  <h4>Not included ({preview.excluded.length})</h4>
                  <ul>
                    {preview.excluded.map((entry, index) => (
                      <li key={`${entry.stepPosition}-${entry.itemPosition}-${index}`}>
                        {entry.text} — {entry.reason}
                      </li>
                    ))}
                  </ul>
                </section>
              )}
              {isRepeat && (
                <p role="status">
                  This Recipe is already on your Grocery List. Would you like to add another Recipe&apos;s worth?
                </p>
              )}
              {isRemoval && (
                <p role="alert">
                  {preview.hasOutstandingAddition
                    ? 'Remove this Recipe from your Grocery List? This cancels only outstanding Recipe demand. Manual additions and purchase history will remain.'
                    : 'This Recipe no longer has outstanding grocery requirements. Nothing needs removing.'}
                </p>
              )}
              <div className="grocery-entry-actions confirmation-actions">
                {!needsFreshReview && !isRemoval && preview.included.length > 0 && (
                  <button className="btn btn-primary" type="button" onClick={addRecipe} disabled={locked}>
                    {isSubmitting ? 'Adding…' : 'Confirm'}
                  </button>
                )}
                {!needsFreshReview && isRemoval && preview.hasOutstandingAddition && (
                  <button className="btn btn-danger" type="button" onClick={removeRecipe} disabled={locked}>
                    {isSubmitting ? 'Removing…' : 'Confirm'}
                  </button>
                )}
                <button className="btn btn-danger-secondary" type="button" onClick={closeReview} disabled={locked} autoFocus>
                  Cancel
                </button>
              </div>
            </>
          )}
        </ModalContainer>
      )}
    </>
  );
}

export default RecipeGroceryActions;
