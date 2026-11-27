import { Link } from 'react-router-dom';
import type { SupermarketStore } from '../../ingredients/services/ingredientsApi';

interface GroceryListControlsProps {
  stores: SupermarketStore[];
  selectedStoreId: string | null;
  onSelectStore: (storeId: string) => void;
  storeError: string | null;
  onRetryStores: () => void;
  onAdd: () => void;
  onClear: () => void;
  outstandingCount: number;
  disabled: boolean;
  canAdd: boolean;
  showCompleted: boolean;
  onShowCompletedChange: (show: boolean) => void;
  completedDate: string;
  onCompletedDateChange: (date: string) => void;
}

function GroceryListControls({
  stores,
  selectedStoreId,
  onSelectStore,
  storeError,
  onRetryStores,
  onAdd,
  onClear,
  outstandingCount,
  disabled,
  canAdd,
  showCompleted,
  onShowCompletedChange,
  completedDate,
  onCompletedDateChange,
}: GroceryListControlsProps) {
  return (
    <>
      <header className="grocery-page-heading">
        <div>
          <h1 id="grocery-list-heading">Grocery List</h1>
          <p>Outstanding items are grouped by their selected store and department.</p>
        </div>
        <div className="grocery-page-actions">
          <button className="btn btn-primary" type="button" onClick={onAdd} disabled={!canAdd || disabled}>
            Add items
          </button>
          <Link className="btn btn-secondary" to="/grocery-list/history">Purchase history</Link>
          {outstandingCount > 0 && (
            <button className="btn btn-danger" type="button" onClick={onClear} disabled={disabled}>
              Clear outstanding list
            </button>
          )}
        </div>
      </header>

      <div className="grocery-list-controls">
        <label className="grocery-store-select" htmlFor="grocery-store">
          <span>Group for store</span>
          <select
            id="grocery-store"
            value={selectedStoreId ?? ''}
            onChange={(event) => onSelectStore(event.currentTarget.value)}
          >
            <option value="">Choose a store</option>
            {stores.map((store) => <option value={store.id} key={store.id}>{store.name}</option>)}
          </select>
        </label>
        {storeError && (
          <p className="grocery-inline-error" role="alert">
            {storeError}
            <button className="btn btn-secondary" type="button" onClick={onRetryStores}>Retry stores</button>
          </p>
        )}
      </div>

      <div className="grocery-list-controls">
        <label>
          <input
            type="checkbox"
            checked={showCompleted}
            onChange={(event) => onShowCompletedChange(event.currentTarget.checked)}
          />
          Show completed
        </label>
        {showCompleted && (
          <label htmlFor="grocery-completed-date">
            Completed on (Auckland time)
            <input
              id="grocery-completed-date"
              type="date"
              value={completedDate}
              onChange={(event) => onCompletedDateChange(event.currentTarget.value)}
            />
          </label>
        )}
      </div>
    </>
  );
}

export default GroceryListControls;
