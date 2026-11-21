import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import PageTitle from '../../../shared/components/PageTitle';
import ConfirmDialog from '../../../shared/components/ConfirmDialog';
import { toTitleCase } from '../../../shared/utils/titleCase';
import { useSupermarkets } from '../../ingredients/hooks/useSupermarkets';
import type { SupermarketStore } from '../../ingredients/services/ingredientsApi';
import GroceryItemList from '../components/GroceryItemList';
import GroceryItemEditDialog from '../components/GroceryItemEditDialog';
import GroceryManualEntryModal from '../components/GroceryManualEntryModal';
import { groupGroceryItems } from '../models/groceryGrouping';
import { useGroceryList } from '../hooks/useGroceryList';
import { useGroceryQuantityUpdate } from '../hooks/useGroceryQuantityUpdate';
import { useGroceryItemRemoval } from '../hooks/useGroceryItemRemoval';
import type { GroceryItem } from '../services/groceryListApi';

const STORE_PREFERENCE_KEY = 'recipe-app:grocery-list:selected-store';
function GroceryListPage() {
  const grocery = useGroceryList();
  const quantityUpdate = useGroceryQuantityUpdate(grocery);
  const removal = useGroceryItemRemoval(grocery);
  const supermarkets = useSupermarkets();
  const [storePreference, setStorePreference] = useState(readStorePreference);
  const [isAddingItems, setIsAddingItems] = useState(false);
  const [editingItem, setEditingItem] = useState<GroceryItem | null>(null);
  const stores = useMemo(
    () => supermarkets.data?.flatMap((chain) => chain.stores) ?? [],
    [supermarkets.data],
  );
  const selectedStoreId = supermarkets.data
    && !stores.some((store) => store.id === storePreference.storeId)
    ? null
    : storePreference.storeId;
  const groups = useMemo(
    () => groupGroceryItems(grocery.data?.items ?? [], selectedStoreId),
    [grocery.data?.items, selectedStoreId],
  );

  function selectStore(storeId: string) {
    const nextStoreId = storeId || null;
    try {
      if (nextStoreId) {
        window.localStorage.setItem(STORE_PREFERENCE_KEY, nextStoreId);
      } else {
        window.localStorage.removeItem(STORE_PREFERENCE_KEY);
      }
      setStorePreference({ storeId: nextStoreId, error: null });
    } catch {
      setStorePreference({
        storeId: nextStoreId,
        error: 'Your store selection could not be saved in this browser.',
      });
    }
  }

  const outstandingItems = grocery.data?.items.filter((item) => item.state === 'outstanding') ?? [];
  const currentEditingItem = editingItem
    ? grocery.data?.items.find((item) => item.id === editingItem.id) ?? editingItem
    : null;

  return (
    <>
      <PageTitle title="Grocery List" />
      <main className="app-main">
        <section className="app-page grocery-list-page" aria-labelledby="grocery-list-heading">
          <header className="grocery-page-heading">
            <div>
              <h1 id="grocery-list-heading">Grocery List</h1>
              <p>Outstanding items are grouped by their selected store and department.</p>
            </div>
            <div className="grocery-page-actions">
              <button
                className="btn btn-primary"
                type="button"
                onClick={() => setIsAddingItems(true)}
                disabled={!grocery.data || quantityUpdate.disabled || removal.disabled}
              >
                Add items
              </button>
              <Link className="btn btn-secondary" to="/grocery-list/history">Purchase history</Link>
            </div>
          </header>

          <div className="grocery-list-controls">
            <label className="grocery-store-select" htmlFor="grocery-store">
              <span>Group for store</span>
              <select
                id="grocery-store"
                value={selectedStoreId ?? ''}
                onChange={(event) => selectStore(event.currentTarget.value)}
              >
                <option value="">Choose a store</option>
                {stores.map((store: SupermarketStore) => (
                  <option value={store.id} key={store.id}>{store.name}</option>
                ))}
              </select>
            </label>
            {supermarkets.error && (
              <p className="grocery-inline-error" role="alert">
                {supermarkets.error}
                <button className="btn btn-secondary" type="button" onClick={supermarkets.reload}>
                  Retry stores
                </button>
              </p>
            )}
          </div>

          {storePreference.error && <p className="grocery-inline-error" role="status">{storePreference.error}</p>}
          {removal.error && (
            <p className="grocery-inline-error" role="alert">
              {removal.error}
              {removal.canRetry && (
                <button className="btn btn-secondary" type="button" onClick={removal.retry}>
                  Retry removal
                </button>
              )}
              {removal.canRetry && removal.retryName && ` for ${toTitleCase(removal.retryName)}`}
            </p>
          )}
          {quantityUpdate.error && (
            <p className="grocery-inline-error" role="alert">
              {quantityUpdate.error}
              {quantityUpdate.canRetry && (
                <button className="btn btn-secondary" type="button" onClick={quantityUpdate.retry}>
                  Retry quantity update
                </button>
              )}
            </p>
          )}
          {grocery.updated && <p className="grocery-update-notice" role="status">Grocery list updated.</p>}
          {grocery.lastSuccessfulAt && (
            <p className="grocery-last-check">
              Last successful check:{' '}
              {new Intl.DateTimeFormat('en-NZ', {
                dateStyle: 'medium',
                timeStyle: 'short',
                timeZone: 'Pacific/Auckland',
              }).format(grocery.lastSuccessfulAt)}
            </p>
          )}
          {grocery.isStale && grocery.data && (
            <p className="grocery-stale-notice" role="status">
              Unable to check for updates. Showing the last successful list.
              <button className="btn btn-secondary" type="button" onClick={grocery.reload}>Retry</button>
            </p>
          )}
          {grocery.isLoading && !grocery.data && (
            <p className="grocery-list-status" role="status">Loading your grocery list…</p>
          )}
          {grocery.error && !grocery.data && (
            <div className="grocery-inline-error" role="alert">
              <p>{grocery.error}</p>
              <button className="btn btn-secondary" type="button" onClick={grocery.reload}>
                Retry grocery list
              </button>
            </div>
          )}
          {!grocery.isLoading && grocery.data && outstandingItems.length === 0 && (
            <div className="grocery-empty-state" role="status">
              {grocery.data.id === null
                ? <p>Your grocery list is ready. It will stay empty until you add items.</p>
                : grocery.data.completedItemCount > 0
                  ? <p>All items are completed. Review them in <Link to="/grocery-list/history">purchase history</Link>.</p>
                  : <p>There are no outstanding grocery items.</p>}
            </div>
          )}
          {grocery.data && outstandingItems.length > 0 && (
            <GroceryItemList
              groups={groups}
              onEdit={setEditingItem}
              onRemove={removal.request}
              onQuantityChange={quantityUpdate.changeQuantity}
              disabled={quantityUpdate.disabled || removal.disabled}
            />
          )}
        </section>
      </main>
      <ConfirmDialog
        open={removal.item !== null}
        title="Remove grocery item?"
        message={`Remove ${removal.item ? toTitleCase(removal.item.name) : 'this item'} and cancel its outstanding demand, including any Recipe contributions? Purchase history will be kept.`}
        confirmLabel="Remove item"
        tone="danger"
        confirmFirst
        onCancel={removal.cancel}
        onConfirm={removal.confirm}
      />
      {isAddingItems && grocery.data && (
        <GroceryManualEntryModal
          expectedListVersion={grocery.data.version}
          onClose={() => setIsAddingItems(false)}
          onAdded={grocery.reload}
        />
      )}
      {currentEditingItem && grocery.data && (
        <GroceryItemEditDialog
          item={currentEditingItem}
          listVersion={grocery.data.version}
          selectedStoreId={selectedStoreId}
          stores={stores}
          onClose={() => setEditingItem(null)}
          onSaved={grocery.reload}
        />
      )}
    </>
  );
}

function readStorePreference(): { storeId: string | null; error: string | null } {
  try {
    return { storeId: window.localStorage.getItem(STORE_PREFERENCE_KEY), error: null };
  } catch {
    return {
      storeId: null,
      error: 'Your selected store could not be loaded from this browser.',
    };
  }
}

export default GroceryListPage;
