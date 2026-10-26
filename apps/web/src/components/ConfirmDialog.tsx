import { useEffect, useId, useRef, type ReactNode } from 'react';

export type ConfirmTone = 'primary' | 'danger';

export interface ConfirmDialogProps {
  open: boolean;
  title: string;
  message?: ReactNode;
  confirmLabel: string;
  cancelLabel?: string;
  tone?: ConfirmTone;
  onConfirm: () => void;
  onCancel: () => void;
}

function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel,
  cancelLabel = 'Cancel',
  tone = 'primary',
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const cancelRef = useRef<HTMLButtonElement>(null);
  const titleId = useId();
  const messageId = useId();

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) {
      return;
    }
    if (open && !dialog.open) {
      dialog.showModal();
      // Default to the non-destructive choice so a stray Enter never confirms.
      cancelRef.current?.focus();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  }, [open]);

  return (
    <dialog
      ref={dialogRef}
      className="confirm-dialog"
      aria-labelledby={titleId}
      aria-describedby={message ? messageId : undefined}
      onCancel={(event) => {
        // Escape: let the owner decide, so the open prop stays the source of truth.
        event.preventDefault();
        onCancel();
      }}
      onClick={(event) => {
        if (event.target === event.currentTarget) {
          onCancel();
        }
      }}
    >
      <div className="confirm-dialog-body">
        <h2 id={titleId} className="confirm-dialog-title">{title}</h2>
        {message && <p id={messageId} className="confirm-dialog-message">{message}</p>}
        <div className="confirm-dialog-actions">
          <button ref={cancelRef} className="btn btn-secondary" type="button" onClick={onCancel}>
            {cancelLabel}
          </button>
          <button className={`btn btn-${tone}`} type="button" onClick={onConfirm}>
            {confirmLabel}
          </button>
        </div>
      </div>
    </dialog>
  );
}

export default ConfirmDialog;
