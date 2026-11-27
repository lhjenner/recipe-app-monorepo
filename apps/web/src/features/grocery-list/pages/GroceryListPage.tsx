import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import PageTitle from '../../../shared/components/PageTitle';
import Snackbar from '../../../shared/components/Snackbar';
import { toTitleCase } from '../../../shared/utils/titleCase';
import { useSupermarkets } from '../../ingredients/hooks/useSupermarkets';
import GroceryListControls from '../components/GroceryListControls';
import GroceryListOverlays from '../components/GroceryListOverlays';
import GroceryItemList from '../components/GroceryItemList';
import GroceryItemEditDialog from '../components/GroceryItemEditDialog';
import GroceryManualEntryModal from '../components/GroceryManualEntryModal';
import GroceryRecipeSources from '../components/GroceryRecipeSources';
import { groupGroceryItems } from '../models/groceryGrouping';
import { isPurchaseOnlyChange, useGroceryList } from '../hooks/useGroceryList';
import { useGroceryQuantityUpdate } from '../hooks/useGroceryQuantityUpdate';
import { useGroceryItemRemoval } from '../hooks/useGroceryItemRemoval';
import { useGroceryRecipeRemoval } from '../hooks/useGroceryRecipeRemoval';
import { useGroceryClearAll } from '../hooks/useGroceryClearAll';
import { useGroceryPurchaseActions } from '../hooks/useGroceryPurchaseActions';
import { getAucklandToday } from '../models/groceryDate';
import type { GroceryItem } from '../services/groceryListApi';

const STORE_PREFERENCE_KEY = 'recipe-app:grocery-list:selected-store';
function GroceryListPage() {
  const [showCompleted, setShowCompleted] = useState(false);
  const [completedDate, setCompletedDate] = useState(getAucklandToday);
  const grocery = useGroceryList({
    includeCompleted: showCompleted,
    from: showCompleted ? completedDate : undefined,
    to: showCompleted ? completedDate : undefined,
  });
  const quantityUpdate = useGroceryQuantityUpdate(grocery);
  const removal = useGroceryItemRemoval(grocery);
  const recipeRemoval = useGroceryRecipeRemoval(grocery);
  const clearAll = useGroceryClearAll(grocery);
  const purchases = useGroceryPurchaseActions(grocery);
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
  const outstandingItems = useMemo(
    () => grocery.data?.items.filter((item) => item.state === 'outstanding') ?? [],
    [grocery.data?.items],
  );
  const completedItems = useMemo(
    () => grocery.data?.items.filter((item) => item.state === 'completed') ?? [],
    [grocery.data?.items],
  );
  const groups = useMemo(() => groupGroceryItems(outstandingItems, selectedStoreId), [outstandingItems, selectedStoreId]);
  const mutationsDisabled = quantityUpdate.disabled
    || removal.disabled
    || recipeRemoval.disabled
    || clearAll.disabled
    || purchases.disabled;
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

  const currentEditingItem = editingItem
    ? grocery.data?.items.find((item) => item.id === editingItem.id) ?? editingItem
    : null;

  return (
    <>
      <PageTitle title="Grocery List" />
      <main className="app-main">
        <section className="app-page grocery-list-page" aria-labelledby="grocery-list-heading">
          <GroceryListControls
            stores={stores}
            selectedStoreId={selectedStoreId}
            onSelectStore={selectStore}
            storeError={supermarkets.error}
            onRetryStores={supermarkets.reload}
            onAdd={() => setIsAddingItems(true)}
            onClear={clearAll.request}
            outstandingCount={outstandingItems.length}
            disabled={mutationsDisabled}
            canAdd={grocery.data !== null}
            showCompleted={showCompleted}
            onShowCompletedChange={setShowCompleted}
            completedDate={completedDate}
            onCompletedDateChange={setCompletedDate}
          />

          {grocery.data && (
            <GroceryRecipeSources items={grocery.data.items}
              disabled={mutationsDisabled}
              onRemove={recipeRemoval.request} />
          )}

          {storePreference.error && <p className="grocery-inline-error" role="status">{storePreference.error}</p>}
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
              <button className="btn btn-secondary" type="button" onClick={() => grocery.reload()}>Retry</button>
            </p>
          )}
          {grocery.isLoading && !grocery.data && (
            <p className="grocery-list-status" role="status">Loading your grocery list…</p>
          )}
          {grocery.error && !grocery.data && (
            <div className="grocery-inline-error" role="alert">
              <p>{grocery.error}</p>
              <button className="btn btn-secondary" type="button" onClick={() => grocery.reload()}>
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
              onPurchase={(item) => purchases.requestPurchase(item, selectedStoreId)}
              onQuantityChange={quantityUpdate.changeQuantity}
              disabled={mutationsDisabled}
            />
          )}
          {showCompleted && completedItems.length > 0 && (
            <section aria-labelledby="grocery-completed-heading">
              <h2 id="grocery-completed-heading">Completed items</h2>
              <ul>
                {completedItems.map((item) => (
                  <li key={item.id}>
                    {toTitleCase(item.name)} —{' '}
                    <Link to="/grocery-list/history">view recorded purchases and amounts</Link>
                  </li>
                ))}
              </ul>
            </section>
          )}
        </section>
      </main>
      <GroceryListOverlays
        removal={removal}
        recipeRemoval={recipeRemoval}
        quantityUpdate={quantityUpdate}
        clearAll={clearAll}
        purchases={purchases}
      />
      <div className="snackbar-stack">
        {grocery.updateCount > 0 && !(grocery.lastChange && purchases.lastPurchase
          && isPurchaseOnlyChange(grocery.lastChange.previous, grocery.lastChange.next, purchases.lastPurchase)) && (
          <Snackbar key={`update-${grocery.updateCount}`} message="Grocery list updated." />
        )}
        {purchases.purchaseNotices.map((notice) => (
          <Snackbar
            key={notice.eventId}
            message={`Purchased: ${toTitleCase(notice.itemName)}`}
            onDismiss={() => purchases.dismissPurchaseNotice(notice.eventId)}
            action={{
              label: 'Undo',
              accessibleLabel: `Undo purchase of ${toTitleCase(notice.itemName)}`,
              disabled: mutationsDisabled,
              onClick: () => purchases.undoPurchase(notice.eventId, notice.itemName),
            }}
          />
        ))}
      </div>
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
