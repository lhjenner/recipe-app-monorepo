export type PageItem = number | 'ellipsis';

/**
 * Builds the page buttons to render: always the first and last page plus the current page
 * and its siblings. A gap hiding exactly one page shows that page instead of an ellipsis.
 */
export function getPageItems(current: number, total: number, siblingCount: number): PageItem[] {
  if (total < 1) {
    return [];
  }
  if (total <= (2 * siblingCount) + 3) {
    return Array.from({ length: total }, (_, index) => index + 1);
  }

  const page = Math.min(Math.max(current, 1), total);
  const visible = new Set([1, total]);
  for (let candidate = page - siblingCount; candidate <= page + siblingCount; candidate += 1) {
    if (candidate >= 1 && candidate <= total) {
      visible.add(candidate);
    }
  }

  const items: PageItem[] = [];
  let previous = 0;
  for (const pageNumber of [...visible].sort((first, second) => first - second)) {
    const gap = pageNumber - previous - 1;
    if (gap === 1) {
      items.push(previous + 1);
    } else if (gap > 1) {
      items.push('ellipsis');
    }
    items.push(pageNumber);
    previous = pageNumber;
  }
  return items;
}
