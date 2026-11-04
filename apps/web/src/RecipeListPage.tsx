import { useEffect } from 'react';
import { Link, useLocation } from 'react-router-dom';
import PageTitle from './PageTitle';
import Pagination from './components/Pagination';
import { useRecipeList } from './hooks/useRecipeData';
import { useRecipeListParams } from './hooks/useRecipeListParams';
import { toTitleCase } from './titleCase';

const PAGE_SIZE = 25;

function RecipeListPage() {
  const { search, page, setSearch, setPage } = useRecipeListParams();
  const location = useLocation();
  const recipeList = useRecipeList({ search, page, pageSize: PAGE_SIZE });
  const recipes = recipeList.data?.items ?? [];
  const totalCount = recipeList.data?.totalCount ?? 0;
  const pageCount = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));
  const isPastLastPage = !recipeList.isLoading && recipeList.data !== null && page > pageCount;
  const canShowList = !recipeList.isLoading && !recipeList.error && !isPastLastPage;

  useEffect(() => {
    if (isPastLastPage) {
      setPage(pageCount, { replace: true });
    }
  }, [isPastLastPage, pageCount, setPage]);

  return (
    <>
      <PageTitle title="Recipe List" />
      <main className="app-main">
        <section className="app-page recipe-list-page" aria-labelledby="recipe-list-heading">
          <h1 id="recipe-list-heading">Recipe List</h1>

          <label className="recipe-search">
            <span>Search recipes</span>
            <input
              type="search"
              aria-label="Search recipes"
              aria-describedby="recipe-search-help"
              value={search}
              onChange={(event) => setSearch(event.currentTarget.value)}
            />
            <span id="recipe-search-help" className="recipe-search-help">
              Search by recipe name or Ingredient name.
            </span>
          </label>

          {recipeList.error && (
            <div className="recipe-list-error" role="alert">
              <p>{recipeList.error}</p>
              <button className="btn btn-secondary" type="button" onClick={recipeList.reload}>
                Retry recipes
              </button>
            </div>
          )}

          {recipeList.isLoading && (
            <p className="recipe-list-status" role="status">
              {search.trim() ? 'Searching recipes…' : 'Loading recipes…'}
            </p>
          )}

          {canShowList && recipes.length === 0 && (
            <p className="recipe-list-status" role="status">
              {search.trim()
                ? `No recipes match '${search.trim()}'`
                : 'No recipes yet'}
            </p>
          )}

          {canShowList && recipes.length > 0 && (
            <>
              <p className="recipe-list-result-count" role="status">
                Showing {((page - 1) * PAGE_SIZE) + 1}–{Math.min(page * PAGE_SIZE, totalCount)} of {totalCount}
              </p>
              <ul className="recipe-list-items">
                {recipes.map((recipe) => (
                  <li key={recipe.id}>
                    <Link
                      to={`/recipes/${recipe.id}`}
                      state={{ listSearch: location.search }}
                    >
                      {toTitleCase(recipe.name)}
                    </Link>
                  </li>
                ))}
              </ul>
              <Pagination
                label="Recipe pages"
                page={page}
                pageCount={pageCount}
                onPageChange={(nextPage) => setPage(nextPage)}
              />
            </>
          )}
        </section>
      </main>
    </>
  );
}

export default RecipeListPage;
