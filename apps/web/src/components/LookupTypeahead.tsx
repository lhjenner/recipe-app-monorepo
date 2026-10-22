import { useId, useState } from 'react';
import type { LookupValue } from '../services/ingredientsApi';

interface LookupTypeaheadProps {
  label?: string;
  accessibleLabel: string;
  options: LookupValue[];
  value: string;
  disabled?: boolean;
  onChange: (id: string | null, name: string) => void;
  onCreate: (name: string) => Promise<LookupValue>;
}

function normalize(value: string) {
  return value.trim().replace(/\s+/g, ' ').toLocaleLowerCase();
}

function LookupTypeahead({
  label,
  accessibleLabel,
  options,
  value,
  disabled = false,
  onChange,
  onCreate,
}: LookupTypeaheadProps) {
  const listId = useId();
  const [error, setError] = useState<string | null>(null);
  const [isCreating, setIsCreating] = useState(false);
  const selected = options.find((option) => normalize(option.name) === normalize(value));

  async function createOption() {
    setIsCreating(true);
    setError(null);
    try {
      const created = await onCreate(value.trim().replace(/\s+/g, ' '));
      onChange(created.id, created.name);
    } catch (requestError: unknown) {
      setError(requestError instanceof Error ? requestError.message : 'Unable to create this option.');
    } finally {
      setIsCreating(false);
    }
  }

  return (
    <div className="ingredient-lookup-field">
      <label>
        {label && <span>{label}</span>}
        <input
          aria-label={accessibleLabel}
          disabled={disabled}
          list={listId}
          value={value}
          onChange={(event) => {
            const nextValue = event.currentTarget.value;
            const match = options.find((option) => normalize(option.name) === normalize(nextValue));
            onChange(match?.id ?? null, nextValue);
          }}
        />
      </label>
      <datalist id={listId}>
        {options.map((option) => <option key={option.id} value={option.name} />)}
      </datalist>
      {!disabled && !selected && value.trim() && (
        <button type="button" onClick={() => void createOption()} disabled={isCreating}>
          {isCreating ? 'Creating…' : `Create '${value.trim()}'`}
        </button>
      )}
      {error && <span className="ingredient-field-error" role="alert">{error}</span>}
    </div>
  );
}

export default LookupTypeahead;
