import { useEffect, useRef, type ReactNode } from 'react';

interface ModalContainerProps {
  labelledBy: string;
  className: string;
  initialFocusSelector?: string;
  locked: boolean;
  closeOnBackdrop?: boolean;
  onClose: () => void;
  children: ReactNode;
}

function ModalContainer({
  labelledBy, className, initialFocusSelector, locked, closeOnBackdrop = false, onClose, children,
}: ModalContainerProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const dialog = dialogRef.current!;
    const previousFocus = document.activeElement;
    dialog.showModal();
    if (initialFocusSelector) {
      dialog.querySelector<HTMLElement>(initialFocusSelector)?.focus();
    }
    return () => {
      dialog.close();
      if (previousFocus instanceof HTMLElement && previousFocus.isConnected) {
        previousFocus.focus();
      }
    };
  }, [initialFocusSelector]);

  return (
    <dialog
      ref={dialogRef}
      className={className}
      aria-modal="true"
      aria-labelledby={labelledBy}
      onCancel={(event) => {
        event.preventDefault();
        event.stopPropagation();
        if (!locked) {
          onClose();
        }
      }}
      onClick={(event) => {
        if (closeOnBackdrop && !locked && event.target === event.currentTarget) {
          onClose();
        }
      }}
    >
      {children}
    </dialog>
  );
}

export default ModalContainer;
