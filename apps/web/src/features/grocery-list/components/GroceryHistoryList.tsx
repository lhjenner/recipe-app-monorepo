import { Undo2 } from 'lucide-react';
import IconButton from '../../../shared/components/IconButton';
import type { GroceryHistoryEvent } from '../services/groceryListApi';
import { toTitleCase } from '../../../shared/utils/titleCase';

interface GroceryHistoryListProps {
  events: GroceryHistoryEvent[];
  onUndo?: (event: GroceryHistoryEvent) => void;
  disabled?: boolean;
}

function GroceryHistoryList({ events, onUndo, disabled = false }: GroceryHistoryListProps) {
  return (
    <>
      <div className="grocery-history-table-wrap">
        <table className="grocery-history-table" aria-label="Purchase history">
          <thead>
            <tr>
              <th scope="col">Date</th>
              <th scope="col">Item</th>
              <th scope="col">Quantity</th>
              <th scope="col">Store</th>
              <th scope="col">Record</th>
            </tr>
          </thead>
          <tbody>
            {events.map((event) => (
              <tr key={event.id}>
                <td>{formatDate(event.occurredAtUtc)}</td>
                <th scope="row">
                  {toTitleCase(event.itemNameSnapshot)}
                  {event.noteSnapshot && <small>{event.noteSnapshot}</small>}
                </th>
                <td>{formatPurchaseQuantity(event)}</td>
                <td>{event.storeName ?? 'Store not recorded'}</td>
                <td>
                  <div className="grocery-history-record">
                    <span><EventState event={event} /></span>
                    {onUndo && event.kind === 'purchase' && !event.isReversed && (
                      <button className="btn btn-secondary" type="button" disabled={disabled} onClick={() => onUndo(event)}>
                        Undo purchase
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <ul className="grocery-history-cards" aria-label="Purchase history">
        {events.map((event) => (
          <li key={event.id} className="grocery-history-card">
            <h2>{toTitleCase(event.itemNameSnapshot)}</h2>
            <dl>
              <div><dt>Date</dt><dd>{formatDate(event.occurredAtUtc)}</dd></div>
              <div><dt>Quantity</dt><dd>{formatPurchaseQuantity(event)}</dd></div>
              <div><dt>Store</dt><dd>{event.storeName ?? 'Store not recorded'}</dd></div>
              <div className="grocery-history-record-row">
                <dt>Record</dt>
                <dd className="grocery-history-record">
                  <span><EventState event={event} /></span>
                  {onUndo && event.kind === 'purchase' && !event.isReversed && (
                    <IconButton
                      label={`Undo purchase of ${toTitleCase(event.itemNameSnapshot)}`}
                      tooltip="Undo purchase"
                      icon={<Undo2 aria-hidden="true" />}
                      variant="btn-secondary"
                      disabled={disabled}
                      onClick={() => onUndo(event)}
                    />
                  )}
                </dd>
              </div>
            </dl>
            {event.noteSnapshot && <p>{event.noteSnapshot}</p>}
          </li>
        ))}
      </ul>
    </>
  );
}

function EventState({ event }: { event: GroceryHistoryEvent }) {
  if (event.kind === 'reversal') {
    return <>Purchase undone</>;
  }
  return event.isReversed ? <>Purchase reversed</> : <>Purchase recorded</>;
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat('en-NZ', {
    dateStyle: 'medium',
    timeZone: 'Pacific/Auckland',
  }).format(new Date(value));
}

function formatPurchaseQuantity(event: GroceryHistoryEvent): string {
  const amount = new Intl.NumberFormat('en-NZ', { maximumFractionDigits: 9 }).format(event.quantity);
  return event.purchaseUnit.trim().toLowerCase() === 'item' ? amount : `${amount} ${event.purchaseUnit}`;
}

export default GroceryHistoryList;
