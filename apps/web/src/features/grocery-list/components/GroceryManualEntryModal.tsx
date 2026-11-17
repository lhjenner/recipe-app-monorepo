import { useRef, useState } from 'react';
import { useGroceryEntryLookups } from '../hooks/useGroceryEntryLookups';
import {
  addGroceryItems,
  type GroceryDuplicateReview,
  type GroceryManualEntryCommand,
} from '../services/groceryListApi';
import { useGroceryCommand } from '../hooks/useGroceryCommand';
import ModalContainer from '../../../shared/components/ModalContainer';
import GroceryDuplicateReviewPanel from './GroceryDuplicateReviewPanel';
import GroceryManualEntryRow, { type GroceryManualEntryDraft } from './GroceryManualEntryRow';

interface GroceryManualEntryModalProps {
  expectedListVersion: number;
  onClose: () => void;
  onAdded: () => void;
}

const emptyRow = (): GroceryManualEntryDraft => ({
  name: '',
  note: '',
  purchaseQuantity: '1',
  ingredientId: null,
  saveAsIngredient: false,
  categoryId: '',
  storeDepartments: {},
});

function GroceryManualEntryModal({
  expectedListVersion,
  onClose,
  onAdded,
}: GroceryManualEntryModalProps) {
  const [rows, setRows] = useState<GroceryManualEntryDraft[]>([emptyRow()]);
  const lookups = useGroceryEntryLookups();
  const [error, setError] = useState<string | null>(null);
  const [duplicateReviews, setDuplicateReviews] = useState<GroceryDuplicateReview[]>([]);
  const commandId = useRef(crypto.randomUUID());
  const mutation = useGroceryCommand(addGroceryItems, () => {
    onAdded();
    onClose();
  }, (failure) => setDuplicateReviews(failure.duplicateReviews));
  const fieldErrors = mutation.error?.fieldErrors ?? {};
  const isSaving = mutation.isSaving;

  function changeRows(next: GroceryManualEntryDraft[]) {
    if (mutation.locked) {
      return;
    }
    setRows(next);
    setDuplicateReviews([]);
    setError(null);
    mutation.clearError();
    commandId.current = crypto.randomUUID();
  }

  function updateRow(index: number, value: GroceryManualEntryDraft) {
    changeRows(rows.map((row, rowIndex) => rowIndex === index ? value : row));
  }

  function addRow() {
    if (rows.length < 10) {
      changeRows([...rows, emptyRow()]);
    }
  }

  function removeRow(index: number) {
    if (rows.length > 1) {
      changeRows(rows.filter((_, rowIndex) => rowIndex !== index));
    }
  }

  function save(reviews: GroceryDuplicateReview[] = []) {
    if (mutation.locked) {
      return;
    }
    setError(null);
    if (rows.some((row) => row.name.trim().length === 0
      || !Number.isFinite(Number(row.purchaseQuantity)))) {
      setError('Enter a name and a valid purchase quantity for every row.');
      return;
    }

    const command: GroceryManualEntryCommand = {
      commandId: commandId.current,
      expectedListVersion,
      items: rows.map((row) => ({
        name: row.name,
        note: row.note || null,
        purchaseQuantity: Number(row.purchaseQuantity),
        purchaseUnit: 'item',
        ingredientId: row.ingredientId,
        saveAsIngredient: row.ingredientId ? false : row.saveAsIngredient,
        categoryId: row.saveAsIngredient ? row.categoryId || null : null,
        storeMappings: row.saveAsIngredient
          ? Object.entries(row.storeDepartments)
            .filter(([, departmentId]) => departmentId.length > 0)
            .map(([storeId, departmentId]) => ({ storeId, departmentId, isNotStocked: false }))
          : [],
      })),
      reviewedDuplicates: reviews,
    };

    mutation.submit(command);
  }

  return (
    <ModalContainer labelledBy="grocery-entry-heading" className="grocery-entry-dialog"
      initialFocusSelector="input" locked={mutation.locked} onClose={onClose}>
      <header className="grocery-entry-heading">
        <div>
          <h2 id="grocery-entry-heading">Add items to Grocery List</h2>
          <p>Add one or more items. You can optionally save new rows as Ingredients.</p>
        </div>
        <button className="btn btn-secondary" type="button" onClick={onClose} disabled={mutation.locked}>
          Close
        </button>
      </header>

      {lookups.error && <p className="grocery-inline-error" role="alert">{lookups.error}</p>}
      {error && <p className="grocery-inline-error" role="alert">{error}</p>}
      {mutation.error && <p className="grocery-inline-error" role="alert">{mutation.error.message}</p>}
      {mutation.canRetry && (
        <p role="status">
          The outcome is unknown. Retry the original request before changing this draft.
          <button className="btn btn-secondary" type="button" onClick={mutation.retry}>Retry addition</button>
        </p>
      )}
      {Object.keys(fieldErrors).length > 0 && (
        <ul className="grocery-inline-error" role="alert">
          {Object.entries(fieldErrors).flatMap(([field, messages]) =>
            messages.map((message) => <li key={`${field}-${message}`}>{field}: {message}</li>))}
        </ul>
      )}

      <form
        className="grocery-entry-form"
        onSubmit={(event) => {
          event.preventDefault();
          save();
        }}
        noValidate
      >
        <div className="grocery-entry-rows">
          {rows.map((row, index) => (
            <GroceryManualEntryRow
              key={index}
              index={index}
              value={row}
              categories={lookups.categories}
              departments={lookups.departments}
              stores={lookups.stores.flatMap((chain) => chain.stores)}
              disabled={mutation.locked}
              onChange={(value) => updateRow(index, value)}
              onRemove={() => removeRow(index)}
              canRemove={rows.length > 1}
            />
          ))}
        </div>
        <div className="grocery-entry-actions">
          <button
            className="btn btn-secondary"
            type="button"
            disabled={mutation.locked || rows.length >= 10}
            onClick={addRow}
          >
            Add row
          </button>
          {!duplicateReviews.length ? (
            <button className="btn btn-primary" type="submit" disabled={mutation.locked}>
              {isSaving ? 'Adding…' : 'Add to Grocery List'}
            </button>
          ) : (
            <button
              className="btn btn-primary"
              type="button"
              disabled={mutation.locked}
              onClick={() => save(duplicateReviews)}
            >
              {isSaving ? 'Adding…' : 'Confirm reviewed increments'}
            </button>
          )}
          {duplicateReviews.length > 0 && (
            <button
              className="btn btn-secondary"
              type="button"
              disabled={mutation.locked}
              onClick={() => {
                setDuplicateReviews([]);
                mutation.clearError();
              }}
            >
              Cancel review
            </button>
          )}
        </div>
      </form>

      <GroceryDuplicateReviewPanel reviews={duplicateReviews} />
    </ModalContainer>
  );
}

export default GroceryManualEntryModal;
