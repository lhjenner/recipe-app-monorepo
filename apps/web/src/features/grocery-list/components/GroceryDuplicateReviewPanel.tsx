import ModalContainer from '../../../shared/components/ModalContainer';
import { toTitleCase } from '../../../shared/utils/titleCase';
import type { GroceryDuplicateReview } from '../services/groceryListApi';

interface GroceryDuplicateReviewPanelProps {
  reviews: GroceryDuplicateReview[];
  otherCount: number;
  removedCount: number;
  locked: boolean;
  error: string | null;
  canRetry: boolean;
  onRemove: (entryIndex: number) => void;
  onConfirm: () => void;
  onSkip: () => void;
  onCancel: () => void;
  onBack: () => void;
  onRetry: () => void;
}

function GroceryDuplicateReviewPanel({
  reviews, otherCount, removedCount, locked, error, canRetry,
  onRemove, onConfirm, onSkip, onCancel, onBack, onRetry,
}: GroceryDuplicateReviewPanelProps) {
  return (
    <ModalContainer
      labelledBy="grocery-duplicate-heading"
      className="confirm-dialog grocery-duplicate-dialog"
      initialFocusSelector="[data-review-back]"
      locked={locked}
      closeOnBackdrop
      onClose={onBack}
    >
      <div className="confirm-dialog-body">
        <h2 className="confirm-dialog-title" id="grocery-duplicate-heading">Review matching grocery items</h2>
        <p>These items are already on your Grocery List. Confirm the increments or skip them.</p>
        {otherCount > 0 && (
          <p>{otherCount} other {otherCount === 1 ? 'item will' : 'items will'} be added.</p>
        )}
        {removedCount > 0 && (
          <p role="status">{removedCount} removed {removedCount === 1 ? 'item' : 'items'} will not be added.</p>
        )}
        <ul className="grocery-duplicate-review-list">
          {reviews.map((review) => {
            const unit = review.purchaseUnit === 'item' ? 'item(s)' : review.purchaseUnit;
            const name = toTitleCase(review.itemName);
            return (
              <li key={review.entryIndex}>
                <div>
                  <div className="grocery-duplicate-review-name">{name}</div>
                  <div>New Total: {review.resultingQuantity} {unit}</div>
                </div>
                {reviews.length > 1 && (
                  <button className="btn btn-danger-secondary" type="button" disabled={locked}
                    aria-label={`Remove ${name} from review`} onClick={() => onRemove(review.entryIndex)}>
                    Remove
                  </button>
                )}
              </li>
            );
          })}
        </ul>
        {error && <p className="grocery-inline-error" role="alert">{error}</p>}
        {canRetry && (
          <button className="btn btn-secondary" type="button" onClick={onRetry}>Retry addition</button>
        )}
        <div className="confirm-dialog-actions confirmation-actions">
          <button className="btn btn-primary" type="button" disabled={locked || Boolean(error)} onClick={onConfirm}>
            Confirm reviewed increments
          </button>
          <button className="btn btn-danger-secondary" type="button" data-review-back="" disabled={locked} onClick={onBack}>
            Back to editing
          </button>
          {otherCount > 0 ? (
            <button className="btn btn-secondary" type="button" disabled={locked || Boolean(error)} onClick={onSkip}>
              Skip duplicates and add other items
            </button>
          ) : (
            <button className="btn btn-danger-secondary" type="button" disabled={locked} onClick={onCancel}>
              Cancel
            </button>
          )}
        </div>
      </div>
    </ModalContainer>
  );
}

export default GroceryDuplicateReviewPanel;
