import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import ConfirmDialog, { type ConfirmDialogProps } from '../components/ConfirmDialog';

export type ConfirmOptions = Omit<ConfirmDialogProps, 'open' | 'onConfirm' | 'onCancel'>;

interface ConfirmDialogApi {
  confirm: (options: ConfirmOptions) => Promise<boolean>;
  confirmDialog: ReactNode;
}

/**
 * Promise-based replacement for window.confirm: `if (await confirm({...})) { ... }`.
 * Render `confirmDialog` once in the calling component.
 */
export function useConfirmDialog(): ConfirmDialogApi {
  const [options, setOptions] = useState<ConfirmOptions | null>(null);
  const [open, setOpen] = useState(false);
  const resolveRef = useRef<((confirmed: boolean) => void) | null>(null);

  const settle = useCallback((confirmed: boolean) => {
    resolveRef.current?.(confirmed);
    resolveRef.current = null;
    setOpen(false);
  }, []);

  const confirm = useCallback((next: ConfirmOptions) => new Promise<boolean>((resolve) => {
    resolveRef.current?.(false);
    resolveRef.current = resolve;
    setOptions(next);
    setOpen(true);
  }), []);

  useEffect(() => () => {
    resolveRef.current?.(false);
    resolveRef.current = null;
  }, []);

  const confirmDialog = options && (
    <ConfirmDialog
      {...options}
      open={open}
      onConfirm={() => settle(true)}
      onCancel={() => settle(false)}
    />
  );

  return { confirm, confirmDialog };
}
