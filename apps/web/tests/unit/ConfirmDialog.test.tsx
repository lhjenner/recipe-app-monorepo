import { act, fireEvent, render, screen } from '@testing-library/react';
import { expect, test, vi } from 'vitest';
import ConfirmDialog from '../../src/components/ConfirmDialog';
import { useConfirmDialog, type ConfirmOptions } from '../../src/hooks/useConfirmDialog';

function expectDialogClosed() {
  // jsdom has no UA stylesheet for <dialog>, so check the open state rather than visibility.
  expect(screen.getByRole('dialog', { hidden: true })).not.toHaveAttribute('open');
}

const archiveOptions: ConfirmOptions = {
  title: 'Archive ingredient?',
  message: 'Flour will be hidden from the active list.',
  confirmLabel: 'Archive',
  tone: 'danger',
};

function Harness({ onResult }: { onResult: (confirmed: boolean) => void }) {
  const { confirm, confirmDialog } = useConfirmDialog();
  return (
    <>
      <button type="button" onClick={() => void confirm(archiveOptions).then(onResult)}>
        Open
      </button>
      {confirmDialog}
    </>
  );
}

async function openHarness() {
  const onResult = vi.fn();
  render(<Harness onResult={onResult} />);
  fireEvent.click(screen.getByRole('button', { name: 'Open' }));
  const dialog = await screen.findByRole('dialog', { name: 'Archive ingredient?' });
  return { onResult, dialog };
}

test('renders an accessible modal with the supplied title, message and button labels', async () => {
  const { dialog } = await openHarness();

  expect(dialog).toHaveAccessibleDescription('Flour will be hidden from the active list.');
  expect(screen.getByRole('button', { name: 'Archive' })).toHaveClass('btn', 'btn-danger');
  expect(screen.getByRole('button', { name: 'Cancel' })).toHaveFocus();
});

test('resolves true when the user confirms and closes the dialog', async () => {
  const { onResult } = await openHarness();

  await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Archive' })));

  expect(onResult).toHaveBeenCalledWith(true);
  expectDialogClosed();
});

test.each([
  ['the cancel button', () => fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))],
  ['the Escape key', () => fireEvent(screen.getByRole('dialog'), new Event('cancel', { cancelable: true }))],
  ['a backdrop click', () => fireEvent.click(screen.getByRole('dialog'))],
])('resolves false when the user dismisses with %s', async (_label, dismiss) => {
  const { onResult } = await openHarness();

  await act(async () => dismiss());

  expect(onResult).toHaveBeenCalledWith(false);
  expectDialogClosed();
});

test('can be used directly as a controlled component', () => {
  const onConfirm = vi.fn();
  const onCancel = vi.fn();
  const { rerender } = render(
    <ConfirmDialog
      open
      title="Discard unsaved changes?"
      confirmLabel="Discard changes"
      cancelLabel="Keep editing"
      onConfirm={onConfirm}
      onCancel={onCancel}
    />,
  );

  fireEvent.click(screen.getByRole('button', { name: 'Keep editing' }));
  fireEvent.click(screen.getByRole('button', { name: 'Discard changes' }));
  expect(onCancel).toHaveBeenCalledOnce();
  expect(onConfirm).toHaveBeenCalledOnce();

  rerender(
    <ConfirmDialog
      open={false}
      title="Discard unsaved changes?"
      confirmLabel="Discard changes"
      onConfirm={onConfirm}
      onCancel={onCancel}
    />,
  );
  expectDialogClosed();
});
