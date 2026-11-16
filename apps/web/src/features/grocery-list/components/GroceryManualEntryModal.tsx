import { useRef, useState } from 'react';
import { useGroceryEntryLookups } from '../hooks/useGroceryEntryLookups';
import {
  addGroceryItems,
  GroceryListApiError,
  type GroceryDuplicateReview,
  type GroceryManualEntryCommand,
} from '../services/groceryListApi';
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
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [duplicateReviews, setDuplicateReviews] = useState<GroceryDuplicateReview[]>([]);
  const [isSaving, setIsSaving] = useState(false);
  const commandId = useRef(crypto.randomUUID());

  function changeRows(next: GroceryManualEntryDraft[]) {
    setRows(next);
    setDuplicateReviews([]);
    setFieldErrors({});
    setError(null);
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

  async function save(reviews: GroceryDuplicateReview[] = []) {
    setError(null);
    setFieldErrors({});
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

    setIsSaving(true);
    try {
      await addGroceryItems(command);
      onAdded();
      onClose();
    } catch (requestError: unknown) {
      if (requestError instanceof GroceryListApiError) {
        setError(requestError.message);
        setFieldErrors(requestError.fieldErrors);
        if (requestError.duplicateReviews.length > 0) {
          setDuplicateReviews(requestError.duplicateReviews);
        } else if (requestError.status > 0) {
          setDuplicateReviews([]);
        }
      } else {
        setError(requestError instanceof Error
          ? requestError.message
          : 'Unable to add grocery items. Please try again.');
      }
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <div className="grocery-modal-backdrop">
      <section
        className="grocery-entry-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="grocery-entry-heading"
      >
        <header className="grocery-entry-heading">
          <div>
            <h2 id="grocery-entry-heading">Add items to Grocery List</h2>
            <p>Add one or more items. You can optionally save new rows as Ingredients.</p>
          </div>
          <button className="btn btn-secondary" type="button" onClick={onClose} disabled={isSaving}>
            Close
          </button>
        </header>

        {lookups.error && <p className="grocery-inline-error" role="alert">{lookups.error}</p>}
        {error && <p className="grocery-inline-error" role="alert">{error}</p>}
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
            void save();
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
                disabled={isSaving}
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
              disabled={isSaving || rows.length >= 10}
              onClick={addRow}
            >
              Add row
            </button>
            {!duplicateReviews.length ? (
              <button className="btn btn-primary" type="submit" disabled={isSaving}>
                {isSaving ? 'Adding…' : 'Add to Grocery List'}
              </button>
            ) : (
              <button
                className="btn btn-primary"
                type="button"
                disabled={isSaving}
                onClick={() => void save(duplicateReviews)}
              >
                {isSaving ? 'Adding…' : 'Confirm reviewed increments'}
              </button>
            )}
          </div>
        </form>

        <GroceryDuplicateReviewPanel reviews={duplicateReviews} />
      </section>
    </div>
  );
}

export default GroceryManualEntryModal;
