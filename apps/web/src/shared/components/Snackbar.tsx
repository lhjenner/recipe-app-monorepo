import { useEffect, useRef, useState } from 'react';

interface SnackbarProps {
  message: string;
  action?: {
    label: string;
    accessibleLabel: string;
    disabled: boolean;
    onClick: () => void;
  };
  onDismiss?: () => void;
}

function Snackbar({ message, action, onDismiss }: SnackbarProps) {
  const [visible, setVisible] = useState(true);
  const [hovered, setHovered] = useState(false);
  const [focused, setFocused] = useState(false);
  const remaining = useRef(4_000);

  useEffect(() => {
    if (!visible || hovered || focused || action?.disabled) return;
    const started = Date.now();
    const timer = window.setTimeout(() => {
      setVisible(false);
      onDismiss?.();
    }, remaining.current);
    return () => {
      window.clearTimeout(timer);
      remaining.current = Math.max(0, remaining.current - (Date.now() - started));
    };
  }, [visible, hovered, focused, action?.disabled, onDismiss]);

  return visible ? (
    <div
      className="snackbar"
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      onFocus={() => setFocused(true)}
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget)) setFocused(false);
      }}
    >
      <span className="snackbar-message" role="status">{message}</span>
      {action && (
        <button className="btn btn-secondary snackbar-action" type="button" aria-label={action.accessibleLabel}
          disabled={action.disabled} onClick={action.onClick}>
          {action.label}
        </button>
      )}
    </div>
  ) : null;
}

export default Snackbar;
