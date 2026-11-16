import { useId } from 'react';
import { toTitleCase } from '../../../shared/utils/titleCase';
import type {
  LookupValue,
  SupermarketChain,
} from '../../ingredients/services/ingredientsApi';
import { useGroceryIngredientOptions } from '../hooks/useGroceryIngredientOptions';

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
  const nameId = useId();
  const quantityId = useId();
  const noteId = useId();
  const ingredientId = useId();
  const categoryId = useId();
  const ingredientOptions = useGroceryIngredientOptions(value.name);

  function update(changes: Partial<GroceryManualEntryDraft>) {
    onChange({ ...value, ...changes });
  }

  return (
    <fieldset className="grocery-entry-row">
      <legend>Item {index + 1}</legend>
      <label className="grocery-form-field" htmlFor={nameId}>
        <span>Name</span>
        <input
          id={nameId}
          value={value.name}
          maxLength={200}
          autoComplete="off"
          disabled={disabled}
          onChange={(event) => update({ name: event.currentTarget.value, ingredientId: null })}
          onBlur={() => {
            const name = toTitleCase(value.name);
            if (name !== value.name) {
              update({ name });
            }
          }}
        />
      </label>
      {ingredientOptions.items.length > 0 && (
        <label className="grocery-form-field" htmlFor={ingredientId}>
          <span>Link an existing Ingredient (optional)</span>
          <select
            id={ingredientId}
            value={value.ingredientId ?? ''}
            disabled={disabled}
            onChange={(event) => {
              const selected = ingredientOptions.items.find((item) => item.id === event.currentTarget.value);
              update({
                ingredientId: selected?.id ?? null,
                name: selected?.name ?? value.name,
                saveAsIngredient: false,
                categoryId: '',
                storeDepartments: {},
              });
            }}
          >
            <option value="">Keep as a separate grocery item</option>
            {ingredientOptions.items.map((ingredient) => (
              <option value={ingredient.id} key={ingredient.id}>{toTitleCase(ingredient.name)}</option>
            ))}
          </select>
        </label>
      )}
      {ingredientOptions.error && <p className="grocery-inline-error" role="alert">{ingredientOptions.error}</p>}
      {value.ingredientId && (
        <p className="grocery-linked-ingredient" role="status">
          Linked to an existing Ingredient.
          <button
            className="btn btn-secondary"
            type="button"
            disabled={disabled}
            onClick={() => update({ ingredientId: null })}
          >
            Use as an ad-hoc item
          </button>
        </p>
      )}
      <label className="grocery-form-field" htmlFor={quantityId}>
        <span>Quantity</span>
        <input
          id={quantityId}
          type="number"
          inputMode="numeric"
          min="1"
          max="9999"
          step="1"
          value={value.purchaseQuantity}
          disabled={disabled}
          onChange={(event) => update({ purchaseQuantity: event.currentTarget.value })}
        />
      </label>
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
