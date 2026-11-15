import { useState } from 'react';
import { Link } from 'react-router-dom';
import PageTitle from '../../../shared/components/PageTitle';
import Pagination from '../../../shared/components/Pagination';
import GroceryHistoryList from '../components/GroceryHistoryList';
import { useGroceryHistory } from '../hooks/useGroceryHistory';
import type { GroceryHistoryQuery } from '../services/groceryListApi';

const PAGE_SIZE = 25;

function GroceryHistoryPage() {
  const todayInAuckland = getAucklandToday();
  const [allDates, setAllDates] = useState(false);
  const [from, setFrom] = useState(todayInAuckland);
  const [to, setTo] = useState(todayInAuckland);
  const [page, setPage] = useState(1);
  const query: GroceryHistoryQuery = { allDates, from, to, page, pageSize: PAGE_SIZE };
  const history = useGroceryHistory(query);
  const pageCount = Math.max(1, Math.ceil((history.data?.totalCount ?? 0) / PAGE_SIZE));
  const validDateRange = allDates || from <= to;

  function updateFrom(value: string) {
    setFrom(value);
    setPage(1);
  }

  function updateTo(value: string) {
    setTo(value);
    setPage(1);
  }

  return (
    <>
      <PageTitle title="Purchase History" />
      <main className="app-main">
        <section className="app-page grocery-history-page" aria-labelledby="grocery-history-heading">
          <header className="grocery-page-heading">
            <div>
              <h1 id="grocery-history-heading">Purchase History</h1>
              <p>Dates and day boundaries use Pacific/Auckland time.</p>
            </div>
            <Link className="btn btn-secondary" to="/grocery-list">Back to grocery list</Link>
          </header>

          <div className="grocery-history-controls">
            <label className="grocery-history-all-dates">
              <input
                type="checkbox"
                checked={allDates}
                onChange={(event) => {
                  setAllDates(event.currentTarget.checked);
                  setPage(1);
                }}
              />
              Show all dates
            </label>
            {!allDates && (
              <>
                <label htmlFor="grocery-history-from">From</label>
                <input id="grocery-history-from" type="date" value={from} onChange={(event) => updateFrom(event.currentTarget.value)} />
                <label htmlFor="grocery-history-to">To</label>
                <input id="grocery-history-to" type="date" value={to} onChange={(event) => updateTo(event.currentTarget.value)} />
              </>
            )}
          </div>

          {!validDateRange && <p className="grocery-inline-error" role="alert">End date must not be before start date.</p>}
          {history.isLoading && validDateRange && <p className="grocery-list-status" role="status">Loading purchase history…</p>}
          {history.error && validDateRange && (
            <div className="grocery-inline-error" role="alert">
              <p>{history.error}</p>
              <button className="btn btn-secondary" type="button" onClick={history.reload}>Retry history</button>
            </div>
          )}
          {history.data && validDateRange && history.data.items.length === 0 && (
            <p className="grocery-empty-state" role="status">
              {history.data.totalCount === 0 ? 'No purchases for the selected dates.' : 'No purchases on this page.'}
            </p>
          )}
          {history.data && validDateRange && history.data.items.length > 0 && (
            <>
              <p className="grocery-history-count" role="status">
                {allDates ? 'All dates' : `${from} to ${to} (Auckland time)`} · {history.data.totalCount} events
              </p>
              <GroceryHistoryList events={history.data.items} />
              <Pagination
                label="Purchase history pages"
                page={page}
                pageCount={pageCount}
                onPageChange={setPage}
              />
            </>
          )}
        </section>
      </main>
    </>
  );
}

function getAucklandToday(): string {
  const parts = new Intl.DateTimeFormat('en-NZ', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    timeZone: 'Pacific/Auckland',
  }).formatToParts(new Date());
  const part = (type: 'year' | 'month' | 'day') =>
    parts.find((value) => value.type === type)?.value ?? '';
  return `${part('year')}-${part('month')}-${part('day')}`;
}

export default GroceryHistoryPage;
