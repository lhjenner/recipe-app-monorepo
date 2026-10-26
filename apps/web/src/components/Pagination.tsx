import { useIsMobile } from '../hooks/useIsMobile';
import { getPageItems } from '../pagination';

interface PaginationProps {
  label: string;
  page: number;
  pageCount: number;
  onPageChange: (page: number) => void;
}

const DESKTOP_SIBLINGS = 2;
const MOBILE_SIBLINGS = 0;

function Pagination({ label, page, pageCount, onPageChange }: PaginationProps) {
  const isMobile = useIsMobile();
  if (pageCount <= 1) {
    return null;
  }

  const items = getPageItems(page, pageCount, isMobile ? MOBILE_SIBLINGS : DESKTOP_SIBLINGS);

  return (
    <nav className="pagination" aria-label={label}>
      <button
        className="btn btn-secondary pagination-step"
        type="button"
        onClick={() => onPageChange(page - 1)}
        disabled={page <= 1}
        aria-label="Previous page"
      >
        <span aria-hidden="true">‹</span>
        <span className="pagination-step-label">Previous</span>
      </button>
      <ol className="pagination-pages">
        {items.map((item, index) => (
          <li key={item === 'ellipsis' ? `ellipsis-${index}` : item}>
            {item === 'ellipsis'
              ? <span className="pagination-ellipsis" aria-hidden="true">…</span>
              : (
                <button
                  className="pagination-page"
                  type="button"
                  aria-label={`Page ${item}`}
                  aria-current={item === page ? 'page' : undefined}
                  onClick={() => {
                    if (item !== page) {
                      onPageChange(item);
                    }
                  }}
                >
                  {item}
                </button>
              )}
          </li>
        ))}
      </ol>
      <button
        className="btn btn-secondary pagination-step"
        type="button"
        onClick={() => onPageChange(page + 1)}
        disabled={page >= pageCount}
        aria-label="Next page"
      >
        <span className="pagination-step-label">Next</span>
        <span aria-hidden="true">›</span>
      </button>
    </nav>
  );
}

export default Pagination;
