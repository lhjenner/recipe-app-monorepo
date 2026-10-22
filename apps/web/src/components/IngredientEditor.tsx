import type { FormEvent } from 'react';
import LookupTypeahead from './LookupTypeahead';
import IngredientProductLinksEditor from './IngredientProductLinksEditor';
import {
  nutritionFields,
  type IngredientEditorState,
  type NutritionField,
} from '../ingredientFormModel';
import type {
  LookupValue,
  SupermarketChain,
  SupermarketStore,
} from '../services/ingredientsApi';
import { toTitleCase } from '../titleCase';

interface IngredientEditorProps {
  value: IngredientEditorState;
  stores: SupermarketStore[];
  chains: SupermarketChain[];
  categories: LookupValue[];
  departments: LookupValue[];
  errors: Record<string, string[]>;
  isSaving: boolean;
  onChange: (value: IngredientEditorState) => void;
  onCreateCategory: (name: string) => Promise<LookupValue>;
  onCreateDepartment: (name: string) => Promise<LookupValue>;
  onCancel: () => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
}

function IngredientEditor({
  value,
  stores,
  chains,
  categories,
  departments,
  errors,
  isSaving,
  onChange,
  onCreateCategory,
  onCreateDepartment,
  onCancel,
  onSubmit,
}: IngredientEditorProps) {
  function errorFor(field: string) {
    const key = Object.keys(errors).find((name) => name.toLowerCase() === field.toLowerCase());
    return key ? errors[key]?.[0] : undefined;
  }

  function updateStore(
    storeId: string,
    changes: Partial<IngredientEditorState['stores'][string]>,
  ) {
    onChange({ ...value, stores: { ...value.stores, [storeId]: { ...value.stores[storeId], ...changes } } });
  }

  function updateNutrition(field: NutritionField, next: string) {
    onChange({ ...value, nutrition: { ...value.nutrition, [field]: next } });
  }

  return (
    <form className="ingredient-editor" onSubmit={onSubmit} noValidate>
      <fieldset>
        <legend>Ingredient details</legend>
        <label className="ingredient-form-field">
          <span>Name</span>
          <input
            autoFocus
            aria-label="Name"
            aria-invalid={Boolean(errorFor('Name'))}
            value={value.name}
            maxLength={200}
            onChange={(event) => onChange({ ...value, name: event.currentTarget.value })}
            onBlur={() => {
              const name = toTitleCase(value.name);
              if (name !== value.name) {
                onChange({ ...value, name });
              }
            }}
          />
          {errorFor('Name') && <span className="ingredient-field-error">{errorFor('Name')}</span>}
        </label>
        <LookupTypeahead
          label="Category"
          accessibleLabel="Category"
          options={categories}
          value={value.categoryName}
          onChange={(categoryId, categoryName) => onChange({ ...value, categoryId, categoryName })}
          onCreate={onCreateCategory}
        />
        {errorFor('CategoryId') && <span className="ingredient-field-error">{errorFor('CategoryId')}</span>}
        <label className="ingredient-checkbox">
          <input
            type="checkbox"
            checked={value.excludeFromGroceryList}
            onChange={(event) => onChange({
              ...value,
              excludeFromGroceryList: event.currentTarget.checked,
            })}
          />
          <span>Exclude from grocery list</span>
        </label>
      </fieldset>

      <fieldset>
        <legend>Store departments</legend>
        {stores.map((store) => {
          const mapping = value.stores[store.id] ?? {
            departmentId: null,
            departmentName: '',
            isNotStocked: false,
          };
          return (
            <div className="ingredient-store-editor" key={store.id}>
              <h3>{store.name}</h3>
              <LookupTypeahead
                accessibleLabel={`${store.name} department`}
                options={departments}
                value={mapping.departmentName}
                disabled={mapping.isNotStocked}
                onChange={(departmentId, departmentName) =>
                  updateStore(store.id, { departmentId, departmentName })}
                onCreate={onCreateDepartment}
              />
              <label className="ingredient-checkbox">
                <input
                  type="checkbox"
                  checked={mapping.isNotStocked}
                  onChange={(event) => updateStore(store.id, {
                    isNotStocked: event.currentTarget.checked,
                    departmentId: null,
                    departmentName: '',
                  })}
                />
                <span>Not stocked</span>
              </label>
            </div>
          );
        })}
        {errorFor('StoreMappings') && <span className="ingredient-field-error">{errorFor('StoreMappings')}</span>}
      </fieldset>

      <IngredientProductLinksEditor
        links={value.productLinks}
        chains={chains}
        errorFor={errorFor}
        onChange={(productLinks) => onChange({ ...value, productLinks })}
      />

      <fieldset>
        <legend>Nutrition per 100 g</legend>
        <div className="ingredient-nutrition-grid">
          {nutritionFields.map(([field, label]) => (
            <label className="ingredient-form-field" key={field}>
              <span>{label}</span>
              <input
                type="number"
                inputMode="decimal"
                step="any"
                value={value.nutrition[field]}
                onChange={(event) => updateNutrition(field, event.currentTarget.value)}
              />
              {errorFor(`Nutrition.${field}`) && (
                <span className="ingredient-field-error">{errorFor(`Nutrition.${field}`)}</span>
              )}
            </label>
          ))}
        </div>
        <label className="ingredient-form-field">
          <span>Nutrition source (optional)</span>
          <input
            value={value.nutritionSource}
            onChange={(event) => onChange({ ...value, nutritionSource: event.currentTarget.value })}
          />
        </label>
      </fieldset>

      <div className="ingredient-editor-actions">
        <button className="btn btn-primary" type="submit" disabled={isSaving}>
          {isSaving ? 'Saving…' : 'Save ingredient'}
        </button>
        <button type="button" onClick={onCancel} disabled={isSaving}>Cancel</button>
      </div>
    </form>
  );
}

export default IngredientEditor;
