import { useMemo } from 'react';
import { useMatch, useParams } from 'react-router-dom';
import IngredientEditor from './components/IngredientEditor';
import ConfirmDialog from './components/ConfirmDialog';
import PageTitle from './PageTitle';
import { useIngredient } from './hooks/useIngredient';
import { useConfirmDialog } from './hooks/useConfirmDialog';
import { useIngredientEditorSession } from './hooks/useIngredientEditorSession';
import { useIngredientLookups } from './hooks/useIngredientLookups';
import { useSupermarkets } from './hooks/useSupermarkets';
import {
  createCategory,
  createDepartment,
  type IngredientDetail,
  type SupermarketChain,
  type SupermarketStore,
} from './services/ingredientsApi';

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
  const session = useIngredientEditorSession({ id, ingredient, stores, chains });
  const { confirm, confirmDialog } = useConfirmDialog();
  const { blocker, error, archivedIngredientId } = session;

  async function confirmRestore() {
    if (await confirm({
      title: 'Restore archived ingredient?',
      message: 'The archived ingredient with this name will return to your active list. Changes on this form will not be saved.',
      confirmLabel: 'Restore',
    })) {
      await session.restoreArchivedIngredient();
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
            <button type="button" onClick={() => void session.reloadLatest()}>Reload latest</button>
          )}
          {archivedIngredientId && (
            <button
              type="button"
              disabled={session.isSaving}
              onClick={() => void confirmRestore()}
            >
              Restore
            </button>
          )}
        </div>
      )}
      <IngredientEditor
        value={session.form}
        stores={stores}
        chains={chains}
        categories={categories}
        departments={departments}
        errors={session.fieldErrors}
        isSaving={session.isSaving}
        onChange={session.updateForm}
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
        onCancel={session.cancel}
        onSubmit={(event) => void session.submit(event)}
      />
      <ConfirmDialog
        open={blocker.state === 'blocked'}
        title="Discard unsaved changes?"
        message="Your changes to this ingredient will be lost."
        confirmLabel="Discard changes"
        cancelLabel="Keep editing"
        tone="danger"
        onConfirm={() => blocker.proceed?.()}
        onCancel={() => blocker.reset?.()}
      />
      {confirmDialog}
    </>
  );
}

export default IngredientEditorPage;
