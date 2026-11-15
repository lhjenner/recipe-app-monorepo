import type { GroceryHistoryEvent } from '../services/groceryListApi';
import { toTitleCase } from '../../../shared/utils/titleCase';

interface GroceryHistoryListProps {
  events: GroceryHistoryEvent[];
}

function GroceryHistoryList({ events }: GroceryHistoryListProps) {
  return (
    <>
      <div className="grocery-history-table-wrap">
        <table className="grocery-history-table" aria-label="Purchase history">
          <thead>
            <tr>
              <th scope="col">Date and item</th>
              <th scope="col">Quantity</th>
              <th scope="col">Store</th>
              <th scope="col">Record</th>
            </tr>
          </thead>
          <tbody>
            {events.map((event) => (
              <tr key={event.id}>
                <th scope="row">
                  {formatDate(event.occurredAtUtc)}
                  <span>{toTitleCase(event.itemNameSnapshot)}</span>
                  {event.noteSnapshot && <small>{event.noteSnapshot}</small>}
                </th>
                <td>{formatAmount(event.quantity)} {event.purchaseUnit}</td>
                <td>{event.storeName ?? 'Store not recorded'}</td>
                <td><EventState event={event} /></td>
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
              <div><dt>Quantity</dt><dd>{formatAmount(event.quantity)} {event.purchaseUnit}</dd></div>
              <div><dt>Store</dt><dd>{event.storeName ?? 'Store not recorded'}</dd></div>
              <div><dt>Record</dt><dd><EventState event={event} /></dd></div>
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
    return <>Undo recorded ({event.origin === 'manual' ? 'manual' : 'online cart'})</>;
  }
  return event.isReversed ? <>Purchase reversed</> : <>Purchase recorded</>;
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat('en-NZ', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'Pacific/Auckland',
  }).format(new Date(value));
}

function formatAmount(value: number): string {
  return new Intl.NumberFormat('en-NZ', { maximumFractionDigits: 9 }).format(value);
}

export default GroceryHistoryList;
