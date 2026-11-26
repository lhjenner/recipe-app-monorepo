import type { ReactNode } from 'react';

interface IconButtonProps {
  label: string;
  tooltip: string;
  icon: ReactNode;
  variant: string;
  disabled: boolean;
  onClick: () => void;
}

function IconButton({ label, tooltip, icon, variant, disabled, onClick }: IconButtonProps) {
  return (
    <button
      className={`btn btn-icon ${variant}`}
      type="button"
      aria-label={label}
      disabled={disabled}
      onClick={onClick}
    >
      {icon}
      <span className="icon-button-tooltip" aria-hidden="true">{tooltip}</span>
    </button>
  );
}

export default IconButton;
