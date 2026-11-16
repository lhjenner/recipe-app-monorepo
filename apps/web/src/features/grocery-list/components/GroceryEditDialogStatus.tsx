interface GroceryEditDialogStatusProps {
  itemName: string;
  isSaving: boolean;
  latestListNeedsReview: boolean;
  storeSelectionNeedsReview: boolean;
  ingredientNeedsReview: boolean;
  selectedStoreName: string | null;
  lookupError: string | null;
  error: string | null;
  fieldErrors: Record<string, string[]>;
  onClose: () => void;
  onReviewLatest: () => void;
  onReviewStore: () => void;
}

function GroceryEditDialogStatus({
  itemName,
  isSaving,
  latestListNeedsReview,
  storeSelectionNeedsReview,
  ingredientNeedsReview,
  selectedStoreName,
  lookupError,
  error,
  fieldErrors,
  onClose,
  onReviewLatest,
  onReviewStore,
}: GroceryEditDialogStatusProps) {
  return (
    <>
      <header className="grocery-entry-heading">
        <div>
          <h2 id="grocery-edit-heading">Edit {itemName}</h2>
          <p>Changes apply to this outstanding grocery line.</p>
        </div>
        <button className="btn btn-secondary" type="button" onClick={onClose} disabled={isSaving}>
          Close
        </button>
      </header>
      {latestListNeedsReview && (
        <p className="grocery-stale-notice" role="alert">
          The list changed while this draft was open.
          <button className="btn btn-secondary" type="button" onClick={onReviewLatest}>
            Review latest list
          </button>
        </p>
      )}
      {storeSelectionNeedsReview && (
        <p className="grocery-stale-notice" role="alert">
          The selected supermarket changed. Review the new store before editing its department.
          <button className="btn btn-secondary" type="button" onClick={onReviewStore}>
            Review {selectedStoreName ?? 'selected store'}
          </button>
        </p>
      )}
      {ingredientNeedsReview && (
        <p className="grocery-stale-notice" role="alert">
          The linked Ingredient changed while this draft was open.
          <button className="btn btn-secondary" type="button" onClick={onReviewLatest}>
            Review latest Ingredient
          </button>
        </p>
      )}
      {lookupError && <p className="grocery-inline-error" role="alert">{lookupError}</p>}
      {error && <p className="grocery-inline-error" role="alert">{error}</p>}
      {Object.entries(fieldErrors).flatMap(([field, messages]) =>
        messages.map((message) => (
          <p className="grocery-inline-error" role="alert" key={`${field}-${message}`}>
            {field}: {message}
          </p>
        )))}
    </>
  );
}

export default GroceryEditDialogStatus;
