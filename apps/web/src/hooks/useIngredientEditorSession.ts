import { useState, type FormEvent, type SetStateAction } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import {
  createIngredient,
  getIngredient,
  IngredientsApiError,
  restoreIngredient,
  updateIngredient,
  type IngredientDetail,
  type SupermarketChain,
  type SupermarketStore,
} from '../services/ingredientsApi';
import {
  createEmptyIngredient,
  ingredientToEditorState,
  ingredientWriteRequest,
  type IngredientEditorState,
} from '../ingredientFormModel';
import { INGREDIENT_LIST_PATH, readListSearch } from '../ingredientListLocation';
import { useUnsavedChangesGuard } from './useUnsavedChangesGuard';

interface IngredientEditorSessionOptions {
  id: string | null;
  ingredient: IngredientDetail | null;
  stores: SupermarketStore[];
  chains: SupermarketChain[];
}

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

/** Owns the editor's form state and its save, reload-latest and restore-archived workflows. */
export function useIngredientEditorSession({
  id,
  ingredient,
  stores,
  chains,
}: IngredientEditorSessionOptions) {
  const navigate = useNavigate();
  const listPath = INGREDIENT_LIST_PATH + readListSearch(useLocation().state);
  const [form, setForm] = useState(() => ingredient
    ? ingredientToEditorState(ingredient, stores, chains)
    : createEmptyIngredient(stores));
  const [dirty, setDirty] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [archivedIngredientId, setArchivedIngredientId] = useState<string | undefined>();
  const { blocker, allowNavigation } = useUnsavedChangesGuard(dirty);

  function updateForm(next: SetStateAction<IngredientEditorState>) {
    setForm(next);
    setDirty(true);
  }

  function leaveToList() {
    setDirty(false);
    allowNavigation();
    navigate(listPath);
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const prepared = ingredientWriteRequest(form, stores, chains);
    if (Object.keys(prepared.errors).length > 0) {
      setFieldErrors(prepared.errors);
      return;
    }

    setIsSaving(true);
    setError(null);
    setFieldErrors({});
    setArchivedIngredientId(undefined);
    try {
      if (id) {
        await updateIngredient(id, prepared.request);
      } else {
        await createIngredient(prepared.request);
      }
      leaveToList();
    } catch (requestError: unknown) {
      if (requestError instanceof IngredientsApiError) {
        setFieldErrors(requestError.fieldErrors);
        setArchivedIngredientId(requestError.archivedIngredientId);
      }
      setError(errorMessage(requestError, 'Unable to save this ingredient.'));
    } finally {
      setIsSaving(false);
    }
  }

  async function reloadLatest() {
    if (!id) {
      return;
    }
    try {
      const latest = await getIngredient(id);
      setForm(ingredientToEditorState(latest, stores, chains));
      setFieldErrors({});
      setError(null);
      setDirty(false);
    } catch (requestError: unknown) {
      setError(errorMessage(requestError, 'Unable to reload ingredient.'));
    }
  }

  async function restoreArchivedIngredient() {
    if (!archivedIngredientId) {
      return;
    }
    setIsSaving(true);
    setError(null);
    try {
      await restoreIngredient(archivedIngredientId);
      leaveToList();
    } catch (requestError: unknown) {
      setError(errorMessage(requestError, 'Unable to restore this ingredient.'));
    } finally {
      setIsSaving(false);
    }
  }

  return {
    form,
    updateForm,
    isSaving,
    error,
    fieldErrors,
    archivedIngredientId,
    blocker,
    submit,
    reloadLatest,
    restoreArchivedIngredient,
    cancel: () => navigate(listPath),
  };
}
