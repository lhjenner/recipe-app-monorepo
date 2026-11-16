import { useEffect, useRef, type ReactNode } from 'react';

interface GroceryDialogProps {
  labelledBy: string;
  locked: boolean;
  onClose: () => void;
  children: ReactNode;
}

function GroceryDialog({ labelledBy, locked, onClose, children }: GroceryDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const dialog = dialogRef.current!;
    const previousFocus = document.activeElement;
    dialog.showModal();
    dialog.querySelector<HTMLInputElement>('input')?.focus();
    return () => {
      dialog.close();
      if (previousFocus instanceof HTMLElement && previousFocus.isConnected) {
        previousFocus.focus();
      }
    };
  }, []);

  return (
    <dialog
      ref={dialogRef}
      className="grocery-entry-dialog"
      aria-modal="true"
      aria-labelledby={labelledBy}
      onCancel={(event) => {
        event.preventDefault();
        if (!locked) {
          onClose();
        }
      }}
    >
      {children}
    </dialog>
  );
}

export default GroceryDialog;
