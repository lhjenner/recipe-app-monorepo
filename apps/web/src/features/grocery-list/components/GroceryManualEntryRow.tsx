import { useId } from 'react';
import { toTitleCase } from '../../../shared/utils/titleCase';
import LookupTypeahead from '../../ingredients/components/LookupTypeahead';
import type {
  LookupValue,
  SupermarketChain,
} from '../../ingredients/services/ingredientsApi';
import { useGroceryIngredientOptions } from '../hooks/useGroceryIngredientOptions';
import GroceryQuantityStepper from './GroceryQuantityStepper';

export interface GroceryManualEntryDraft {
  name: string;
  note: string;
  purchaseQuantity: string;
  ingredientId: string | null;
  saveAsIngredient: boolean;
  categoryId: string;
  storeDepartments: Record<string, string>;
}

interface GroceryManualEntryRowProps {
  index: number;
  value: GroceryManualEntryDraft;
  categories: LookupValue[];
  departments: LookupValue[];
  stores: SupermarketChain['stores'];
  disabled: boolean;
  onChange: (value: GroceryManualEntryDraft) => void;
  onRemove: () => void;
  canRemove: boolean;
}

function GroceryManualEntryRow({
  index,
  value,
  categories,
  departments,
  stores,
  disabled,
  onChange,
  onRemove,
  canRemove,
}: GroceryManualEntryRowProps) {
  const quantityId = useId();
  const noteId = useId();
  const categoryId = useId();
  const ingredientOptions = useGroceryIngredientOptions(value.name);

  function update(changes: Partial<GroceryManualEntryDraft>) {
    onChange({ ...value, ...changes });
  }

  return (
    <fieldset className="grocery-entry-row">
      <legend>Item {index + 1}</legend>
      <div className={`grocery-entry-name${value.ingredientId ? ' is-linked' : ''}`}>
        <div className="grocery-form-field">
          <LookupTypeahead
            label="Name"
            accessibleLabel="Name"
            options={ingredientOptions.items}
            value={value.name}
            selectedId={value.ingredientId}
            maxLength={200}
            disabled={disabled}
            onChange={(ingredientId, name) => update({
              name,
              ingredientId,
              ...(ingredientId ? {
                saveAsIngredient: false,
                categoryId: '',
                storeDepartments: {},
              } : {}),
            })}
          />
        </div>
        {value.ingredientId && (
          <span
            className="grocery-linked-ingredient"
            role="img"
            aria-label="Linked to existing Ingredient"
            title="Linked to existing Ingredient"
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" aria-hidden="true">
              <path d="m5 12 4 4L19 6" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </span>
        )}
      </div>
      {ingredientOptions.error && <p className="grocery-inline-error" role="alert">{ingredientOptions.error}</p>}
      <div className="grocery-entry-quantity-note">
        <div className="grocery-form-field">
          <label htmlFor={quantityId}>Quantity</label>
          <GroceryQuantityStepper
            id={quantityId}
            value={value.purchaseQuantity}
            disabled={disabled}
            onChange={(purchaseQuantity) => update({ purchaseQuantity })}
          />
        </div>
        <label className="grocery-form-field" htmlFor={noteId}>
          <span>Note (optional)</span>
          <input
            id={noteId}
            value={value.note}
            maxLength={250}
            disabled={disabled}
            onChange={(event) => update({ note: event.currentTarget.value })}
          />
        </label>
      </div>
      {!value.ingredientId && (
        <label className="grocery-checkbox">
          <input
            type="checkbox"
            checked={value.saveAsIngredient}
            disabled={disabled}
            onChange={(event) => update({ saveAsIngredient: event.currentTarget.checked })}
          />
          <span>Save to master Ingredient list</span>
        </label>
      )}
      {!value.ingredientId && value.saveAsIngredient && (
        <div className="grocery-new-ingredient-fields">
          <label className="grocery-form-field" htmlFor={categoryId}>
            <span>Category (optional)</span>
            <select
              id={categoryId}
              value={value.categoryId}
              disabled={disabled}
              onChange={(event) => update({ categoryId: event.currentTarget.value })}
            >
              <option value="">No category</option>
              {categories.map((category) => (
                <option value={category.id} key={category.id}>{toTitleCase(category.name)}</option>
              ))}
            </select>
          </label>
          {stores.map((store) => (
            <label className="grocery-form-field" key={store.id}>
              <span>{store.name} department (optional)</span>
              <select
                value={value.storeDepartments[store.id] ?? ''}
                disabled={disabled}
                onChange={(event) => update({
                  storeDepartments: { ...value.storeDepartments, [store.id]: event.currentTarget.value },
                })}
              >
                <option value="">No department</option>
                {departments.map((department) => (
                  <option value={department.id} key={department.id}>{toTitleCase(department.name)}</option>
                ))}
              </select>
            </label>
          ))}
        </div>
      )}
      {canRemove && (
        <button className="btn btn-danger-secondary" type="button" disabled={disabled} onClick={onRemove}>
          Remove row
        </button>
      )}
    </fieldset>
  );
}

export default GroceryManualEntryRow;
