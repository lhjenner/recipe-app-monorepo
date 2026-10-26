import { useMemo, useState } from 'react';
import PageTitle from './PageTitle';
import IngredientCards from './components/IngredientCards';
import IngredientTable from './components/IngredientTable';
import { useIngredientList } from './hooks/useIngredientList';
import { useSupermarkets } from './hooks/useSupermarkets';
import { Link } from 'react-router-dom';
import {
  archiveIngredient,
  restoreIngredient,
} from './services/ingredientsApi';

const PAGE_SIZE = 25;

function IngredientMaintenancePage() {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [archived, setArchived] = useState(false);
  const [pendingIngredientId, setPendingIngredientId] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [failedAction, setFailedAction] = useState<{
    id: string;
    archived: boolean;
  } | null>(null);
  const ingredientList = useIngredientList({ search, page, pageSize: PAGE_SIZE, archived });
  const supermarkets = useSupermarkets();
  const stores = useMemo(() => supermarkets.data
    ?.flatMap((chain) => chain.stores)
    .sort((first, second) => first.displayOrder - second.displayOrder) ?? [],
  [supermarkets.data]);
  const ingredients = ingredientList.data?.items ?? [];
  const totalCount = ingredientList.data?.totalCount ?? 0;
  const pageCount = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));
  const canShowList = !ingredientList.isLoading && !ingredientList.error
    && !supermarkets.isLoading && !supermarkets.error;

  async function performAction(id: string, shouldBeArchived: boolean) {
    setPendingIngredientId(id);
    setActionError(null);
    setFailedAction(null);
    try {
      if (shouldBeArchived) {
        await archiveIngredient(id);
      } else {
        await restoreIngredient(id);
      }
      ingredientList.reload();
    } catch (requestError: unknown) {
      setActionError(requestError instanceof Error
        ? requestError.message
        : 'Unable to update this ingredient. Please try again.');
      setFailedAction({ id, archived: shouldBeArchived });
    } finally {
      setPendingIngredientId(null);
    }
  }

  function confirmAction(id: string, name: string, shouldBeArchived: boolean) {
    const action = shouldBeArchived ? 'archive' : 'restore';
    if (window.confirm(`${action === 'archive' ? 'Archive' : 'Restore'} ${name}?`)) {
      void performAction(id, shouldBeArchived);
    }
  }

  return (
    <>
      <PageTitle title="Ingredient Maintenance" />
      <main className="app-main">
        <section className="app-page ingredient-maintenance" aria-labelledby="ingredient-page-heading">
          <div className="ingredient-page-heading">
            <h1 id="ingredient-page-heading">Ingredient Maintenance</h1>
            <Link className="btn btn-primary" to="/ingredient-maintenance/new">
              Add ingredient
            </Link>
          </div>

          <div className="ingredient-list-controls">
            <label className="ingredient-search">
              <span>Search ingredients</span>
              <input
                type="search"
                aria-label="Search ingredients"
                value={search}
                onChange={(event) => {
                  setSearch(event.currentTarget.value);
                  setPage(1);
                }}
              />
            </label>
            <label className="ingredient-archived-toggle">
              <input
                type="checkbox"
                checked={archived}
                onChange={(event) => {
                  setArchived(event.currentTarget.checked);
                  setPage(1);
                }}
              />
              <span>Show archived</span>
            </label>
          </div>

          {(ingredientList.error || supermarkets.error) && (
            <div className="ingredient-error" role="alert">
              <p>{ingredientList.error ?? supermarkets.error}</p>
              {ingredientList.error && (
                <button type="button" onClick={ingredientList.reload}>Retry ingredients</button>
              )}
              {supermarkets.error && (
                <button type="button" onClick={supermarkets.reload}>Retry supermarkets</button>
              )}
            </div>
          )}

          {actionError && (
            <div className="ingredient-error" role="alert">
              <p>{actionError}</p>
              {failedAction && (
                <button
                  type="button"
                  disabled={pendingIngredientId === failedAction.id}
                  onClick={() => void performAction(failedAction.id, failedAction.archived)}
                >
                  Retry
                </button>
              )}
            </div>
          )}

          {(ingredientList.isLoading || supermarkets.isLoading) && (
            <p className="ingredient-loading" role="status">Loading ingredients…</p>
          )}

          {canShowList && ingredients.length === 0 && (
            <p className="ingredient-empty-state" role="status">
              {search.trim()
                ? `No ingredients match '${search.trim()}'`
                : archived
                  ? 'No archived ingredients.'
                  : 'No ingredients yet.'}
            </p>
          )}

          {canShowList && ingredients.length > 0 && (
            <>
              <p className="ingredient-result-count" role="status">
                Showing {((page - 1) * PAGE_SIZE) + 1}–{Math.min(page * PAGE_SIZE, totalCount)} of {totalCount}
              </p>
              <IngredientTable
                ingredients={ingredients}
                stores={stores}
                archived={archived}
                pendingIngredientId={pendingIngredientId}
                onArchive={(id, name) => confirmAction(id, name, true)}
                onRestore={(id, name) => confirmAction(id, name, false)}
              />
              <IngredientCards
                ingredients={ingredients}
                stores={stores}
                archived={archived}
                pendingIngredientId={pendingIngredientId}
                onArchive={(id, name) => confirmAction(id, name, true)}
                onRestore={(id, name) => confirmAction(id, name, false)}
              />
              <nav className="ingredient-pagination" aria-label="Ingredient pages">
                <button
                  className="btn btn-secondary"
                  type="button"
                  onClick={() => setPage((current) => current - 1)}
                  disabled={page <= 1}
                  aria-label="Previous page"
                >
                  Previous
                </button>
                <span>Page {page} of {pageCount}</span>
                <button
                  className="btn btn-secondary"
                  type="button"
                  onClick={() => setPage((current) => current + 1)}
                  disabled={page >= pageCount}
                  aria-label="Next page"
                >
                  Next
                </button>
              </nav>
            </>
          )}
        </section>
      </main>
    </>
  );
}

export default IngredientMaintenancePage;
