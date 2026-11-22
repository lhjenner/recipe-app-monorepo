import type { GroceryDuplicateReview } from '../services/groceryListApi';

interface GroceryDuplicateReviewPanelProps {
  reviews: GroceryDuplicateReview[];
}

function GroceryDuplicateReviewPanel({ reviews }: GroceryDuplicateReviewPanelProps) {
  if (reviews.length === 0) {
    return null;
  }

  return (
    <section className="grocery-duplicate-review" aria-labelledby="grocery-duplicate-heading">
      <h3 id="grocery-duplicate-heading">Review matching grocery items</h3>
      <p>Confirm each increment to update the existing compatible line. Nothing has been saved yet.</p>
      <ul>
        {reviews.map((review) => {
          const unit = review.purchaseUnit === 'item' ? 'item(s)' : review.purchaseUnit;
          return (
            <li key={review.entryIndex}>
              {review.itemName}: {review.existingQuantity} {unit}
              {' + '}{review.increment} {unit}
              {' = '}{review.resultingQuantity} {unit}
            </li>
          );
        })}
      </ul>
    </section>
  );
}

export default GroceryDuplicateReviewPanel;
