import { useId, useState } from 'react';
import { Link } from 'react-router-dom';
import { toTitleCase } from '../../../shared/utils/titleCase';
import type { GroceryItem } from '../services/groceryListApi';
import type { GroceryItemGroup } from '../models/groceryGrouping';
import GroceryQuantityStepper from './GroceryQuantityStepper';

interface GroceryItemListProps {
  groups: GroceryItemGroup[];
  onEdit: (item: GroceryItem) => void;
  onRemove: (item: GroceryItem) => void;
  onQuantityChange: (item: GroceryItem, quantity: number) => void;
  disabled: boolean;
}

function GroceryItemList({ groups, onEdit, onRemove, onQuantityChange, disabled }: GroceryItemListProps) {
  const id = useId();
  const [collapsedGroups, setCollapsedGroups] = useState<Set<string>>(() => new Set());

  function toggleGroup(name: string) {
    setCollapsedGroups((current) => {
      const next = new Set(current);
      if (next.has(name)) {
        next.delete(name);
      } else {
        next.add(name);
      }
      return next;
    });
  }

  if (groups.length === 0) {
    return null;
  }

  return (
    <>
      <div className="grocery-items-table-wrap">
        <table className="grocery-items-table" aria-label="Outstanding grocery items">
          <thead>
            <tr>
              <th scope="col">Item</th>
              <th scope="col">Requirements and sources</th>
              <th scope="col">To buy</th>
              <th scope="col">Actions</th>
            </tr>
          </thead>
          {groups.map((group) => (
            <tbody key={group.name}>
              <tr className="grocery-group-heading">
                <th scope="rowgroup" colSpan={4}>
                  <GroupToggle
                    name={group.name}
                    expanded={!collapsedGroups.has(group.name)}
                    controls={group.items.map((item) => `${id}-row-${item.id}`).join(' ')}
                    onToggle={() => toggleGroup(group.name)}
                  />
                </th>
              </tr>
              {group.items.map((item) => (
                <tr key={item.id} id={`${id}-row-${item.id}`} hidden={collapsedGroups.has(group.name)}>
                  <th scope="row">{toTitleCase(item.name)}</th>
                  <td><RequirementDetails item={item} /></td>
                  <td>
                    <GroceryQuantityStepper
                      value={String(item.outstandingPurchaseQuantity)}
                      itemName={toTitleCase(item.name)}
                      disabled={disabled}
                      readOnly
                      onChange={(value) => onQuantityChange(item, Number(value))}
                    />
                  </td>
                  <td>
                    <div className="grocery-item-actions">
                      <button className="btn btn-secondary" type="button" disabled={disabled} onClick={() => onEdit(item)}>
                        Edit
                      </button>
                      <button
                        className="btn btn-danger-secondary"
                        type="button"
                        disabled={disabled}
                        aria-label={`Remove ${item.name} from Grocery List`}
                        onClick={() => onRemove(item)}
                      >
                        Remove
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          ))}
        </table>
      </div>
      <div className="grocery-item-cards" aria-label="Outstanding grocery items">
        {groups.map((group, index) => (
          <section className="grocery-item-card-group" key={group.name} aria-labelledby={`group-${slug(group.name)}`}>
            <h2 id={`group-${slug(group.name)}`}>
              <GroupToggle
                name={group.name}
                expanded={!collapsedGroups.has(group.name)}
                controls={`${id}-cards-${index}`}
                onToggle={() => toggleGroup(group.name)}
              />
            </h2>
            <div className="grocery-group-items" id={`${id}-cards-${index}`} hidden={collapsedGroups.has(group.name)}>
              {group.items.map((item) => (
                <article className="grocery-item-card" key={item.id}>
                  <h3>{toTitleCase(item.name)}</h3>
                  <RequirementDetails item={item} />
                  <div className="grocery-item-card-controls">
                    <GroceryQuantityStepper
                      value={String(item.outstandingPurchaseQuantity)}
                      itemName={toTitleCase(item.name)}
                      disabled={disabled}
                      readOnly
                      onChange={(value) => onQuantityChange(item, Number(value))}
                    />
                    <div className="grocery-item-actions">
                      <button className="btn btn-secondary" type="button" disabled={disabled} onClick={() => onEdit(item)}>
                        Edit
                      </button>
                      <button
                        className="btn btn-danger-secondary"
                        type="button"
                        disabled={disabled}
                        aria-label={`Remove ${item.name} from Grocery List`}
                        onClick={() => onRemove(item)}
                      >
                        Remove
                      </button>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          </section>
        ))}
      </div>
    </>
  );
}

interface GroupToggleProps {
  name: string;
  expanded: boolean;
  controls: string;
  onToggle: () => void;
}

function GroupToggle({ name, expanded, controls, onToggle }: GroupToggleProps) {
  return (
    <button
      className="grocery-group-toggle"
      type="button"
      aria-expanded={expanded}
      aria-controls={controls}
      onClick={onToggle}
    >
      <svg className="grocery-group-chevron" width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
        <path d="m3 6 5 5 5-5" />
      </svg>
      {name}
    </button>
  );
}

function RequirementDetails({ item }: { item: GroceryItem }) {
  return (
    <div className="grocery-requirement-details">
      {item.requirementAmount !== null && item.requirementUnit !== null && (
        <p><strong>Requirement:</strong> {formatAmount(item.requirementAmount)} {item.requirementUnit}</p>
      )}
      {item.note && <p><strong>Note:</strong> {item.note}</p>}
      {item.contributions.some((contribution) => contribution.origin === 'recipe') && (
        <details>
          <summary>Sources ({item.contributions.length})</summary>
          <ul>
            {item.contributions.map((contribution) => {
              const original = [contribution.originalAmountText, contribution.originalUnitText]
                .filter(Boolean)
                .join(' ');
              const sourceName = contribution.recipeNameSnapshot
                ? toTitleCase(contribution.recipeNameSnapshot)
                : 'Manual addition';
              return (
                <li key={contribution.id}>
                  {contribution.recipeId
                    ? <Link to={`/recipes/${contribution.recipeId}`}>{sourceName}</Link>
                    : sourceName}
                  {original && ` — ${original}`}
                  {contribution.originalNotesText && ` (${contribution.originalNotesText})`}
                  {contribution.unresolvedReason && ` — Needs review: ${contribution.unresolvedReason}`}
                </li>
              );
            })}
          </ul>
        </details>
      )}
    </div>
  );
}

function formatAmount(value: number): string {
  return new Intl.NumberFormat('en-NZ', { maximumFractionDigits: 9 }).format(value);
}

function slug(value: string): string {
  return value.toLocaleLowerCase().replaceAll(/[^a-z0-9]+/g, '-').replaceAll(/^-|-$/g, '');
}

export default GroceryItemList;
