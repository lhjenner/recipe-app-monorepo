import { toTitleCase } from '../../../shared/utils/titleCase';
import type { LookupValue } from '../../ingredients/services/ingredientsApi';

interface GroceryDepartmentEditorProps {
  storeName: string;
  departmentId: string;
  departments: LookupValue[];
  disabled: boolean;
  showConfirmation: boolean;
  isSaving: boolean;
  canApprove: boolean;
  onDepartmentChange: (departmentId: string) => void;
  onApprove: () => void;
  onDiscard: () => void;
  onCancel: () => void;
}

function GroceryDepartmentEditor({
  storeName,
  departmentId,
  departments,
  disabled,
  showConfirmation,
  isSaving,
  canApprove,
  onDepartmentChange,
  onApprove,
  onDiscard,
  onCancel,
}: GroceryDepartmentEditorProps) {
  return (
    <>
      <label className="grocery-form-field">
        <span>{storeName} department</span>
        <select
          value={departmentId}
          disabled={disabled || departments.length === 0}
          onChange={(event) => onDepartmentChange(event.currentTarget.value)}
        >
          <option value="">Uncategorised</option>
          {departments.map((department) => (
            <option value={department.id} key={department.id}>{toTitleCase(department.name)}</option>
          ))}
        </select>
      </label>
      {showConfirmation && (
        <section className="grocery-department-confirmation" aria-labelledby="grocery-department-confirm-heading">
          <h3 id="grocery-department-confirm-heading">Update {storeName} department?</h3>
          <p>
            Updating the master Ingredient changes its department for this supermarket everywhere it is used.
          </p>
          <div className="grocery-entry-actions">
            <button
              className="btn btn-primary"
              type="button"
              disabled={isSaving || !canApprove}
              onClick={onApprove}
            >
              Update master Ingredient
            </button>
            <button
              className="btn btn-secondary"
              type="button"
              disabled={isSaving}
              onClick={onDiscard}
            >
              Discard department change and save shopping changes
            </button>
            <button
              className="btn btn-danger-secondary"
              type="button"
              disabled={isSaving}
              onClick={onCancel}
            >
              Cancel
            </button>
          </div>
        </section>
      )}
    </>
  );
}

export default GroceryDepartmentEditor;
