import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { useBlocker, useMatch, useNavigate, useParams } from 'react-router-dom';
import IngredientEditor from './components/IngredientEditor';
import PageTitle from './PageTitle';
import { useIngredient } from './hooks/useIngredient';
import { useIngredientLookups } from './hooks/useIngredientLookups';
import { useSupermarkets } from './hooks/useSupermarkets';
import {
  createCategory,
  createDepartment,
  createIngredient,
  getIngredient,
  IngredientsApiError,
  restoreIngredient,
  updateIngredient,
  type IngredientDetail,
  type SupermarketChain,
  type SupermarketStore,
} from './services/ingredientsApi';
import {
  createEmptyIngredient,
  ingredientToEditorState,
  ingredientWriteRequest,
} from './ingredientFormModel';

function IngredientEditorPage() {
  const { id } = useParams<{ id: string }>();
  const isNew = useMatch('/ingredient-maintenance/new') !== null;
  const ingredient = useIngredient(isNew ? null : id ?? null);
  const supermarkets = useSupermarkets();
  const lookups = useIngredientLookups();
  const stores = useMemo(() => supermarkets.data
    ?.flatMap((chain) => chain.stores)
    .sort((first, second) => first.displayOrder - second.displayOrder) ?? [],
  [supermarkets.data]);
  const chains = supermarkets.data ?? [];
  const loading = supermarkets.isLoading || lookups.isLoading || (!isNew && ingredient.isLoading);
  const loadError = supermarkets.error ?? lookups.error ?? ingredient.error;
  const title = isNew ? 'Add Ingredient' : 'Edit Ingredient';

  return (
    <>
      <PageTitle title={title} />
      <main className="app-main">
        <section className="app-page ingredient-maintenance ingredient-editor-page" aria-labelledby="ingredient-editor-heading">
          <h1 id="ingredient-editor-heading">{title}</h1>
          {loading && <p role="status">Loading ingredient form…</p>}
          {loadError && <div className="ingredient-error" role="alert"><p>{loadError}</p></div>}
          {!loading && !loadError && !isNew && !ingredient.data && (
            <p role="alert">Ingredient not found.</p>
          )}
          {!loading && !loadError && (isNew || ingredient.data) && (
            <IngredientEditorSession
              key={id ?? 'new'}
              id={isNew ? null : id ?? null}
              ingredient={ingredient.data}
              stores={stores}
              chains={chains}
              categories={lookups.categories}
              departments={lookups.departments}
              addCategory={lookups.addCategory}
              addDepartment={lookups.addDepartment}
            />
          )}
        </section>
      </main>
    </>
  );
}

interface IngredientEditorSessionProps {
  id: string | null;
  ingredient: IngredientDetail | null;
  stores: SupermarketStore[];
  chains: SupermarketChain[];
  categories: Array<{ id: string; name: string }>;
  departments: Array<{ id: string; name: string }>;
  addCategory: (value: { id: string; name: string }) => void;
  addDepartment: (value: { id: string; name: string }) => void;
}

function IngredientEditorSession({
  id,
  ingredient,
  stores,
  chains,
  categories,
  departments,
  addCategory,
  addDepartment,
}: IngredientEditorSessionProps) {
  const navigate = useNavigate();
  const [form, setForm] = useState(() => ingredient
    ? ingredientToEditorState(ingredient, stores, chains)
    : createEmptyIngredient(stores));
  const [dirty, setDirty] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [archivedIngredientId, setArchivedIngredientId] = useState<string | undefined>();
  const allowNavigation = useRef(false);
  const blocker = useBlocker(useCallback(
    () => dirty && !allowNavigation.current,
    [dirty],
  ));

  useEffect(() => {
    if (blocker.state === 'blocked') {
      if (window.confirm('Discard unsaved changes?')) {
        blocker.proceed();
      } else {
        blocker.reset();
      }
    }
  }, [blocker, blocker.state, blocker.proceed, blocker.reset]);

  useEffect(() => {
    if (!dirty) {
      return;
    }
    const confirmUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', confirmUnload);
    return () => {
      window.removeEventListener('beforeunload', confirmUnload);
    };
  }, [dirty]);

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
      setDirty(false);
      allowNavigation.current = true;
      navigate('/ingredient-maintenance');
    } catch (requestError: unknown) {
      if (requestError instanceof IngredientsApiError) {
        setFieldErrors(requestError.fieldErrors);
        setArchivedIngredientId(requestError.archivedIngredientId);
        setError(requestError.message);
      } else {
        setError(requestError instanceof Error ? requestError.message : 'Unable to save this ingredient.');
      }
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
      setError(requestError instanceof Error ? requestError.message : 'Unable to reload ingredient.');
    }
  }

  async function restoreArchivedIngredient() {
    if (!archivedIngredientId
      || !window.confirm('Restore the archived ingredient with this name?')) {
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      await restoreIngredient(archivedIngredientId);
      setDirty(false);
      allowNavigation.current = true;
      navigate('/ingredient-maintenance');
    } catch (requestError: unknown) {
      setError(requestError instanceof Error
        ? requestError.message
        : 'Unable to restore this ingredient.');
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <>
      {(error || archivedIngredientId) && (
        <div className="ingredient-error" role="alert">
          {archivedIngredientId && (
            <p>An archived ingredient with this name exists.</p>
          )}
          {error && <p>{error}</p>}
          {error?.includes('changed elsewhere') && (
            <button type="button" onClick={() => void reloadLatest()}>Reload latest</button>
          )}
          {archivedIngredientId && (
            <button
              type="button"
              disabled={isSaving}
              onClick={() => void restoreArchivedIngredient()}
            >
              Restore
            </button>
          )}
        </div>
      )}
      <IngredientEditor
        value={form}
        stores={stores}
        chains={chains}
        categories={categories}
        departments={departments}
        errors={fieldErrors}
        isSaving={isSaving}
        onChange={(next) => {
          setForm(next);
          setDirty(true);
        }}
        onCreateCategory={async (name) => {
          const category = await createCategory(name);
          addCategory(category);
          return category;
        }}
        onCreateDepartment={async (name) => {
          const department = await createDepartment(name);
          addDepartment(department);
          return department;
        }}
        onCancel={() => {
          navigate('/ingredient-maintenance');
        }}
        onSubmit={(event) => void submit(event)}
      />
    </>
  );
}

export default IngredientEditorPage;
