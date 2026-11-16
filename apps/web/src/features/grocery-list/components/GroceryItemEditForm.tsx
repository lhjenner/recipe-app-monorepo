import type { LookupValue, SupermarketStore } from '../../ingredients/services/ingredientsApi';
import type { GroceryItem } from '../services/groceryListApi';
import GroceryDepartmentEditor from './GroceryDepartmentEditor';

interface GroceryItemEditFormProps {
  item: GroceryItem;
  quantity: string;
  note: string;
  selectedStore: SupermarketStore | undefined;
  departmentId: string;
  departments: LookupValue[];
  isSaving: boolean;
  locked: boolean;
  showConfirmation: boolean;
  canApprove: boolean;
  needsReview: boolean;
  onSubmit: () => void;
  onQuantityChange: (value: string) => void;
  onNoteChange: (value: string) => void;
  onDepartmentChange: (departmentId: string) => void;
  onApprove: () => void;
  onDiscard: () => void;
  onCancelConfirmation: () => void;
  onClose: () => void;
}

function GroceryItemEditForm({
  item,
  quantity,
  note,
  selectedStore,
  departmentId,
  departments,
  isSaving,
  locked,
  showConfirmation,
  canApprove,
  needsReview,
  onSubmit,
  onQuantityChange,
  onNoteChange,
  onDepartmentChange,
  onApprove,
  onDiscard,
  onCancelConfirmation,
  onClose,
}: GroceryItemEditFormProps) {
  return (
    <form
      className="grocery-edit-form"
      onSubmit={(event) => {
        event.preventDefault();
        onSubmit();
      }}
      noValidate
    >
      <label className="grocery-form-field">
        <span>Quantity</span>
        <input
          type="number"
          inputMode={item.purchaseUnit === 'item' ? 'numeric' : 'decimal'}
          min={item.purchaseUnit === 'item' ? '1' : '0.000000001'}
          max="9999"
          step={item.purchaseUnit === 'item' || item.dimension === 'count' ? '1' : 'any'}
          value={quantity}
          disabled={locked}
          onChange={(event) => onQuantityChange(event.currentTarget.value)}
        />
      </label>
      <label className="grocery-form-field">
        <span>Note (optional)</span>
        <input
          value={note}
          maxLength={250}
          disabled={locked}
          onChange={(event) => onNoteChange(event.currentTarget.value)}
        />
      </label>
      {selectedStore && (
        <GroceryDepartmentEditor
          storeName={selectedStore.name}
          departmentId={departmentId}
          departments={departments}
          disabled={locked}
          showConfirmation={showConfirmation}
          isSaving={locked}
          canApprove={canApprove}
          onDepartmentChange={onDepartmentChange}
          onApprove={onApprove}
          onDiscard={onDiscard}
          onCancel={onCancelConfirmation}
        />
      )}
      {!showConfirmation && (
        <div className="grocery-entry-actions">
          <button
            className="btn btn-primary"
            type="submit"
            disabled={locked || needsReview}
          >
            {isSaving ? 'Saving…' : 'Save changes'}
          </button>
          <button className="btn btn-secondary" type="button" onClick={onClose} disabled={locked}>
            Cancel
          </button>
        </div>
      )}
    </form>
  );
}

export default GroceryItemEditForm;
