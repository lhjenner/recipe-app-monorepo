import { fireEvent, render, screen, within } from '@testing-library/react';
import { beforeEach, expect, test, vi } from 'vitest';
import Pagination from '../../src/components/Pagination';

let isMobile = false;

beforeEach(() => {
  isMobile = false;
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: isMobile,
      media: query,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })),
  });
});

function renderPagination(page: number, pageCount: number) {
  const onPageChange = vi.fn();
  render(<Pagination label="Ingredient pages" page={page} pageCount={pageCount} onPageChange={onPageChange} />);
  return onPageChange;
}

test('renders nothing when everything fits on one page', () => {
  renderPagination(1, 1);

  expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
});

test('renders numbered pages with ellipses and marks the current page', () => {
  renderPagination(7, 13);

  const navigation = screen.getByRole('navigation', { name: 'Ingredient pages' });
  const pageButtons = within(navigation).getAllByRole('button', { name: /^Page \d+$/ });
  expect(pageButtons.map((button) => button.textContent)).toEqual(['1', '5', '6', '7', '8', '9', '13']);
  expect(within(navigation).getAllByText('…')).toHaveLength(2);
  expect(screen.getByRole('button', { name: 'Page 7' })).toHaveAttribute('aria-current', 'page');
  expect(screen.getByRole('button', { name: 'Page 6' })).not.toHaveAttribute('aria-current');
});

test('shows only the first, current and last page on mobile', () => {
  isMobile = true;
  renderPagination(7, 13);

  const pageButtons = screen.getAllByRole('button', { name: /^Page \d+$/ });
  expect(pageButtons.map((button) => button.textContent)).toEqual(['1', '7', '13']);
});

test('requests the chosen, previous and next pages', () => {
  const onPageChange = renderPagination(7, 13);

  fireEvent.click(screen.getByRole('button', { name: 'Page 13' }));
  fireEvent.click(screen.getByRole('button', { name: 'Previous page' }));
  fireEvent.click(screen.getByRole('button', { name: 'Next page' }));
  fireEvent.click(screen.getByRole('button', { name: 'Page 7' }));

  expect(onPageChange.mock.calls).toEqual([[13], [6], [8]]);
});

test('disables Previous on the first page and Next on the last page', () => {
  renderPagination(1, 3);
  expect(screen.getByRole('button', { name: 'Previous page' })).toBeDisabled();
  expect(screen.getByRole('button', { name: 'Next page' })).toBeEnabled();
});

test('disables Next on the last page', () => {
  renderPagination(3, 3);
  expect(screen.getByRole('button', { name: 'Next page' })).toBeDisabled();
  expect(screen.getByRole('button', { name: 'Previous page' })).toBeEnabled();
});
