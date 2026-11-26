import ConfirmDialog from '../../../shared/components/ConfirmDialog';
import { toTitleCase } from '../../../shared/utils/titleCase';
import { useGroceryClearAll } from '../hooks/useGroceryClearAll';
import { useGroceryItemRemoval } from '../hooks/useGroceryItemRemoval';
import { useGroceryPurchaseActions } from '../hooks/useGroceryPurchaseActions';
import { useGroceryQuantityUpdate } from '../hooks/useGroceryQuantityUpdate';
import { useGroceryRecipeRemoval } from '../hooks/useGroceryRecipeRemoval';

interface GroceryListOverlaysProps {
  removal: ReturnType<typeof useGroceryItemRemoval>;
  recipeRemoval: ReturnType<typeof useGroceryRecipeRemoval>;
  quantityUpdate: ReturnType<typeof useGroceryQuantityUpdate>;
  clearAll: ReturnType<typeof useGroceryClearAll>;
  purchases: ReturnType<typeof useGroceryPurchaseActions>;
  disabled: boolean;
}

function GroceryListOverlays({
  removal,
  recipeRemoval,
  quantityUpdate,
  clearAll,
  purchases,
  disabled,
}: GroceryListOverlaysProps) {
  const purchaseNotice = purchases.purchaseNotice;
  return (
    <>
      {removal.error && (
        <p className="grocery-inline-error" role="alert">
          {removal.error}
          {removal.canRetry && (
            <button className="btn btn-secondary" type="button" onClick={removal.retry}>Retry removal</button>
          )}
          {removal.canRetry && removal.retryName && ` for ${toTitleCase(removal.retryName)}`}
        </p>
      )}
      {recipeRemoval.error && (
        <p className="grocery-inline-error" role="alert">
          {recipeRemoval.error}
          {recipeRemoval.canRetry && (
            <button className="btn btn-secondary" type="button" onClick={recipeRemoval.retry}>
              Retry Recipe removal
            </button>
          )}
          {recipeRemoval.canRetry && recipeRemoval.retryName && ` for ${toTitleCase(recipeRemoval.retryName)}`}
        </p>
      )}
      {quantityUpdate.error && (
        <p className="grocery-inline-error" role="alert">
          {quantityUpdate.error}
          {quantityUpdate.canRetry && (
            <button className="btn btn-secondary" type="button" onClick={quantityUpdate.retry}>
              Retry quantity update
            </button>
          )}
        </p>
      )}
      {clearAll.error && (
        <p className="grocery-inline-error" role="alert">
          {clearAll.error}
          {clearAll.canRetry && (
            <button className="btn btn-secondary" type="button" onClick={clearAll.retry}>Retry clear</button>
          )}
        </p>
      )}
      {purchases.error && (
        <p className="grocery-inline-error" role="alert">
          {purchases.error}
          {purchases.canRetry && (
            <button className="btn btn-secondary" type="button" onClick={purchases.retry}>
              {purchases.retryLabel}
            </button>
          )}
        </p>
      )}
      {purchases.status && <p className="grocery-update-notice" role="status">{purchases.status}</p>}
      {purchaseNotice && (
        <p className="grocery-update-notice" role="status">
          {toTitleCase(purchaseNotice.itemName)} purchased.
          <button
            className="btn btn-secondary"
            type="button"
            disabled={disabled}
            onClick={() => purchases.requestUndo(purchaseNotice.eventId, purchaseNotice.itemName)}
          >
            Undo purchase
          </button>
        </p>
      )}
      <ConfirmDialog
        open={recipeRemoval.recipe !== null}
        title="Remove Recipe from Grocery List?"
        message={`Remove ${recipeRemoval.recipe ? toTitleCase(recipeRemoval.recipe.name) : 'this Recipe'} and its outstanding requirements? Manual additions, other Recipes, and purchase history will remain.`}
        confirmLabel="Remove Recipe"
        tone="danger"
        confirmFirst
        onCancel={recipeRemoval.cancel}
        onConfirm={recipeRemoval.confirm}
      />
      <ConfirmDialog
        open={removal.item !== null}
        title="Remove grocery item?"
        message={`Remove ${removal.item ? toTitleCase(removal.item.name) : 'this item'} and cancel its outstanding demand, including any Recipe contributions? Purchase history will be kept.`}
        confirmLabel="Remove item"
        tone="danger"
        confirmFirst
        onCancel={removal.cancel}
        onConfirm={removal.confirm}
      />
      <ConfirmDialog
        open={clearAll.isConfirming}
        title="Clear outstanding grocery list?"
        message="This cancels all outstanding demand only. Completed purchases and purchase history will remain."
        confirmLabel="Clear outstanding list"
        tone="danger"
        confirmFirst
        onCancel={clearAll.cancel}
        onConfirm={clearAll.confirm}
      />
      <ConfirmDialog
        open={purchases.purchaseItem !== null}
        title="Mark item purchased?"
        message={purchases.purchaseItem
          ? `Record the whole outstanding quantity of ${formatAmount(purchases.purchaseItem.outstandingPurchaseQuantity)} ${purchases.purchaseItem.purchaseUnit} of ${toTitleCase(purchases.purchaseItem.name)} as purchased?`
          : undefined}
        confirmLabel="Mark purchased"
        onCancel={purchases.cancelPurchase}
        onConfirm={purchases.confirmPurchase}
      />
      <ConfirmDialog
        open={purchases.undoEventId !== null}
        title="Undo purchase?"
        message={`Restore the recorded quantity for ${purchases.undoItemName ? toTitleCase(purchases.undoItemName) : 'this item'} as outstanding demand? The original purchase will remain in history.`}
        confirmLabel="Undo purchase"
        onCancel={purchases.cancelUndo}
        onConfirm={purchases.confirmUndo}
      />
    </>
  );
}

function formatAmount(value: number): string {
  return new Intl.NumberFormat('en-NZ', { maximumFractionDigits: 9 }).format(value);
}

export default GroceryListOverlays;
