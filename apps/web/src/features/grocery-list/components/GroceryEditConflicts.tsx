import type { LookupValue } from '../../ingredients/services/ingredientsApi';
import type { GroceryEditConflict } from '../hooks/useGroceryEditDraft';
import { toTitleCase } from '../../../shared/utils/titleCase';

interface GroceryEditConflictsProps {
  conflicts: GroceryEditConflict[];
  departments: LookupValue[];
  onChoose: (field: GroceryEditConflict['field'], choice: 'mine' | 'latest') => void;
  onApply: () => void;
}

function GroceryEditConflicts({ conflicts, departments, onChoose, onApply }: GroceryEditConflictsProps) {
  if (!conflicts.length) return null;
  function display(field: GroceryEditConflict['field'], value: string) {
    if (field !== 'department') return value || '(empty)';
    return value ? toTitleCase(departments.find((department) => department.id === value)?.name ?? value) : 'Uncategorised';
  }
  return (
    <section className="grocery-stale-notice" aria-label="Resolve conflicting edits">
      <p>These fields changed on another device. Choose which value to keep for each field.</p>
      {conflicts.map((conflict) => (
        <fieldset key={conflict.field}>
          <legend>{toTitleCase(conflict.field)}</legend>
          <p>My {conflict.field}: {display(conflict.field, conflict.mine)}</p>
          <p>Latest {conflict.field}: {display(conflict.field, conflict.latest)}</p>
          <label>
            <input type="radio" name={`conflict-${conflict.field}`} checked={conflict.choice === 'mine'}
              onChange={() => onChoose(conflict.field, 'mine')} />
            Keep my {conflict.field}
          </label>
          {' '}
          <label>
            <input type="radio" name={`conflict-${conflict.field}`} checked={conflict.choice === 'latest'}
              onChange={() => onChoose(conflict.field, 'latest')} />
            Use latest {conflict.field}
          </label>
        </fieldset>
      ))}
      <button className="btn btn-secondary" type="button" onClick={onApply}
        disabled={conflicts.some((conflict) => conflict.choice === null)}>
        Apply review choices
      </button>
    </section>
  );
}

export default GroceryEditConflicts;
