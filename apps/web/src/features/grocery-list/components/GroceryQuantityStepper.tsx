import { useId } from 'react';

interface GroceryQuantityStepperProps {
  id?: string;
  value: string;
  disabled: boolean;
  readOnly?: boolean;
  itemName?: string;
  onChange: (value: string) => void;
}

function GroceryQuantityStepper({
  id,
  value,
  disabled,
  readOnly = false,
  itemName,
  onChange,
}: GroceryQuantityStepperProps) {
  const generatedId = useId();
  const quantity = Number(value);
  const minimum = readOnly ? 0.000000001 : 1;
  const valid = Number.isFinite(quantity) && (readOnly || Number.isInteger(quantity))
    && quantity >= minimum && quantity <= 9999;
  const suffix = itemName ? ` for ${itemName}` : '';
  // Preserve the purchase contract's nine decimal places when stepping measured quantities.
  const decrease = Number((quantity - 1).toFixed(9));
  const increase = Number((quantity + 1).toFixed(9));

  return (
    <div className="grocery-quantity-stepper">
      <button
        className="btn btn-secondary"
        type="button"
        aria-label={`Decrease quantity${suffix}`}
        disabled={disabled || !valid || decrease < minimum}
        onClick={() => onChange(String(decrease))}
      >
        -
      </button>
      <input
        id={id ?? generatedId}
        type="number"
        inputMode={readOnly ? 'decimal' : 'numeric'}
        min={minimum}
        max="9999"
        step={readOnly ? 'any' : '1'}
        value={value}
        readOnly={readOnly}
        aria-label={itemName ? `Quantity to buy for ${itemName}` : undefined}
        disabled={disabled}
        onChange={(event) => onChange(event.currentTarget.value)}
      />
      <button
        className="btn btn-secondary"
        type="button"
        aria-label={`Increase quantity${suffix}`}
        disabled={disabled || !valid || increase > 9999}
        onClick={() => onChange(String(increase))}
      >
        +
      </button>
    </div>
  );
}

export default GroceryQuantityStepper;
