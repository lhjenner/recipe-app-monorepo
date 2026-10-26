import { useMemo, useState } from 'react';
import PageTitle from './PageTitle';
import IngredientCards from './components/IngredientCards';
import IngredientTable from './components/IngredientTable';
import { useIngredientList } from './hooks/useIngredientList';
import { useSupermarkets } from './hooks/useSupermarkets';
import { Link } from 'react-router-dom';

const PAGE_SIZE = 25;

function IngredientMaintenancePage() {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [archived, setArchived] = useState(false);
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
              />
              <IngredientCards
                ingredients={ingredients}
                stores={stores}
                archived={archived}
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
