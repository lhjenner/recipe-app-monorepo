import { act, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, test, vi } from 'vitest';
import Snackbar from '../../../src/shared/components/Snackbar';

afterEach(() => {
  vi.useRealTimers();
});

test('announces a message without moving focus and dismisses it after four seconds', () => {
  vi.useFakeTimers();
  render(<><button type="button">Keep focus</button><Snackbar message="List updated" /></>);
  const button = screen.getByRole('button', { name: 'Keep focus' });
  button.focus();
  expect(screen.getByRole('status')).toHaveTextContent('List updated');
  expect(button).toHaveFocus();
  act(() => vi.advanceTimersByTime(3_999));
  expect(screen.getByRole('status')).toBeInTheDocument();
  act(() => vi.advanceTimersByTime(1));
  expect(screen.queryByRole('status')).not.toBeInTheDocument();
});

test('a new notification restarts the dismissal timer', () => {
  vi.useFakeTimers();
  const { rerender } = render(<Snackbar key="first" message="List updated" />);
  act(() => vi.advanceTimersByTime(3_000));
  rerender(<Snackbar key="second" message="List updated" />);
  act(() => vi.advanceTimersByTime(1_000));
  expect(screen.getByRole('status')).toBeInTheDocument();
  act(() => vi.advanceTimersByTime(3_000));
  expect(screen.queryByRole('status')).not.toBeInTheDocument();
});

test('stacked notifications expire independently', () => {
  vi.useFakeTimers();
  const { rerender } = render(<Snackbar key="first" message="Purchased: Onions" />);
  act(() => vi.advanceTimersByTime(2_000));
  rerender(<><Snackbar key="first" message="Purchased: Onions" /><Snackbar key="second" message="Purchased: Potatoes" /></>);
  act(() => vi.advanceTimersByTime(2_000));
  expect(screen.queryByText('Purchased: Onions')).not.toBeInTheDocument();
  expect(screen.getByText('Purchased: Potatoes')).toBeInTheDocument();
  act(() => vi.advanceTimersByTime(2_000));
  expect(screen.queryByText('Purchased: Potatoes')).not.toBeInTheDocument();
});

test('pauses dismissal while hovered or focused and resumes the remaining time', () => {
  vi.useFakeTimers();
  render(<Snackbar message="Purchased: Onions" action={{
    label: 'Undo', accessibleLabel: 'Undo purchase of Onions', disabled: false, onClick: vi.fn(),
  }} />);
  const undo = screen.getByRole('button', { name: 'Undo purchase of Onions' });
  act(() => vi.advanceTimersByTime(1_000));
  fireEvent.mouseEnter(undo.parentElement!);
  fireEvent.focus(undo);
  act(() => vi.advanceTimersByTime(10_000));
  expect(undo).toBeInTheDocument();
  fireEvent.mouseLeave(undo.parentElement!);
  act(() => vi.advanceTimersByTime(10_000));
  expect(undo).toBeInTheDocument();
  fireEvent.blur(undo);
  act(() => vi.advanceTimersByTime(2_999));
  expect(undo).toBeInTheDocument();
  act(() => vi.advanceTimersByTime(1));
  expect(screen.queryByRole('button', { name: 'Undo purchase of Onions' })).not.toBeInTheDocument();
});

test('preserves the Undo window while its action is disabled', () => {
  vi.useFakeTimers();
  const action = { label: 'Undo', accessibleLabel: 'Undo purchase', disabled: true, onClick: vi.fn() };
  const { rerender } = render(<Snackbar message="Purchased" action={action} />);
  act(() => vi.advanceTimersByTime(10_000));
  expect(screen.getByRole('status')).toBeInTheDocument();
  rerender(<Snackbar message="Purchased" action={{ ...action, disabled: false }} />);
  act(() => vi.advanceTimersByTime(3_999));
  expect(screen.getByRole('status')).toBeInTheDocument();
  act(() => vi.advanceTimersByTime(1));
  expect(screen.queryByRole('status')).not.toBeInTheDocument();
});
