import { StrictMode } from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { expect, test, vi } from 'vitest';
import ModalContainer from '../../../src/shared/components/ModalContainer';

function content() {
  return (
    <>
      <h2 id="modal-heading">Edit draft</h2>
      <button type="button">Close</button>
      <textarea aria-label="Note" />
    </>
  );
}

test('opens natively, focuses the requested field and restores focus on unmount in Strict Mode', () => {
  const { unmount: unmountTrigger } = render(<button type="button">Open editor</button>);
  const trigger = screen.getByRole('button', { name: 'Open editor' });
  trigger.focus();
  const { unmount } = render(
    <StrictMode>
      <ModalContainer labelledBy="modal-heading" className="editor-dialog"
        initialFocusSelector="textarea" locked={false} onClose={vi.fn()}>
        {content()}
      </ModalContainer>
    </StrictMode>,
  );
  const dialog = screen.getByRole('dialog', { name: 'Edit draft' });
  expect(dialog).toHaveAttribute('open');
  expect(dialog).toHaveAttribute('aria-modal', 'true');
  expect(dialog).toHaveClass('editor-dialog');
  expect(screen.getByRole('textbox', { name: 'Note' })).toHaveFocus();

  unmount();
  expect(dialog).not.toHaveAttribute('open');
  expect(trigger).toHaveFocus();
  unmountTrigger();
});

test('uses the latest lock and close callback without reopening or moving focus on rerender', () => {
  const originalClose = vi.fn();
  const latestClose = vi.fn();
  const props = {
    labelledBy: 'modal-heading', className: 'editor-dialog', initialFocusSelector: 'textarea',
  };
  const { rerender } = render(
    <ModalContainer {...props} locked={false} onClose={originalClose}>{content()}</ModalContainer>,
  );
  const dialog = screen.getByRole('dialog');
  const showModal = vi.spyOn(HTMLDialogElement.prototype, 'showModal');
  screen.getByRole('button', { name: 'Close' }).focus();
  rerender(
    <ModalContainer {...props} locked onClose={latestClose}>{content()}</ModalContainer>,
  );
  const lockedCancel = new Event('cancel', { cancelable: true });
  fireEvent(dialog, lockedCancel);
  expect(lockedCancel.defaultPrevented).toBe(true);
  expect(originalClose).not.toHaveBeenCalled();
  expect(latestClose).not.toHaveBeenCalled();
  expect(dialog).toHaveAttribute('open');
  expect(screen.getByRole('button', { name: 'Close' })).toHaveFocus();
  expect(showModal).not.toHaveBeenCalled();
  showModal.mockRestore();

  rerender(
    <ModalContainer {...props} locked={false} onClose={latestClose}>{content()}</ModalContainer>,
  );
  const cancel = new Event('cancel', { cancelable: true });
  fireEvent(dialog, cancel);
  expect(cancel.defaultPrevented).toBe(true);
  expect(latestClose).toHaveBeenCalledOnce();
  expect(originalClose).not.toHaveBeenCalled();
});

test('does not dismiss on backdrop or content clicks', () => {
  const onClose = vi.fn();
  render(
    <ModalContainer labelledBy="modal-heading" className="editor-dialog" locked={false} onClose={onClose}>
      {content()}
    </ModalContainer>,
  );
  fireEvent.click(screen.getByRole('dialog'));
  fireEvent.click(screen.getByRole('heading'));
  expect(onClose).not.toHaveBeenCalled();
});

test('only dismisses an opted-in backdrop when unlocked', () => {
  const onClose = vi.fn();
  const props = { labelledBy: 'modal-heading', className: 'editor-dialog', closeOnBackdrop: true, onClose };
  const { rerender } = render(<ModalContainer {...props} locked>{content()}</ModalContainer>);
  fireEvent.click(screen.getByRole('dialog'));
  expect(onClose).not.toHaveBeenCalled();
  rerender(<ModalContainer {...props} locked={false}>{content()}</ModalContainer>);
  fireEvent.click(screen.getByRole('heading'));
  expect(onClose).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('dialog'));
  expect(onClose).toHaveBeenCalledOnce();
});

test('does not try to restore focus to a removed trigger', () => {
  const { unmount: removeTrigger } = render(<button type="button">Open editor</button>);
  const trigger = screen.getByRole('button', { name: 'Open editor' });
  trigger.focus();
  const { unmount } = render(
    <ModalContainer labelledBy="modal-heading" className="editor-dialog" locked={false} onClose={vi.fn()}>
      {content()}
    </ModalContainer>,
  );
  removeTrigger();
  const focus = vi.spyOn(trigger, 'focus');
  unmount();
  expect(focus).not.toHaveBeenCalled();
});
