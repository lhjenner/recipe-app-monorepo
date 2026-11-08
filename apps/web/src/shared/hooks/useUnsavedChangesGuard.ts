import { useCallback, useEffect, useRef } from 'react';
import { useBlocker, type Blocker } from 'react-router-dom';

interface UnsavedChangesGuard {
  blocker: Blocker;
  /** Lets the next navigation through, e.g. after a successful save. */
  allowNavigation: () => void;
}

/**
 * Blocks in-app navigation and warns on tab close/refresh while `isDirty` is true.
 * Render a confirmation for `blocker.state === 'blocked'` and call proceed/reset.
 */
export function useUnsavedChangesGuard(isDirty: boolean): UnsavedChangesGuard {
  const bypass = useRef(false);
  const blocker = useBlocker(useCallback(
    () => isDirty && !bypass.current,
    [isDirty],
  ));

  useEffect(() => {
    if (!isDirty) {
      return;
    }
    const confirmUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', confirmUnload);
    return () => {
      window.removeEventListener('beforeunload', confirmUnload);
    };
  }, [isDirty]);

  const allowNavigation = useCallback(() => {
    bypass.current = true;
  }, []);

  return { blocker, allowNavigation };
}
