import { Link } from 'react-router-dom';
import { toTitleCase } from '../../../shared/utils/titleCase';
import type { GroceryItem } from '../services/groceryListApi';
import type { GroceryItemGroup } from '../models/groceryGrouping';

interface GroceryItemListProps {
  groups: GroceryItemGroup[];
  onEdit: (item: GroceryItem) => void;
  onRemove: (item: GroceryItem) => void;
}

function GroceryItemList({ groups, onEdit, onRemove }: GroceryItemListProps) {
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
                <th scope="rowgroup" colSpan={4}>{group.name}</th>
              </tr>
              {group.items.map((item) => (
                <tr key={item.id}>
                  <th scope="row">{toTitleCase(item.name)}</th>
                  <td><RequirementDetails item={item} /></td>
                  <td>{formatAmount(item.outstandingPurchaseQuantity)} {item.purchaseUnit}</td>
                  <td>
                    <div className="grocery-item-actions">
                      <button className="btn btn-secondary" type="button" onClick={() => onEdit(item)}>
                        Edit
                      </button>
                      <button
                        className="btn btn-danger-secondary"
                        type="button"
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
        {groups.map((group) => (
          <section className="grocery-item-card-group" key={group.name} aria-labelledby={`group-${slug(group.name)}`}>
            <h2 id={`group-${slug(group.name)}`}>{group.name}</h2>
            {group.items.map((item) => (
              <article className="grocery-item-card" key={item.id}>
                <h3>{toTitleCase(item.name)}</h3>
                <p><strong>To buy:</strong> {formatAmount(item.outstandingPurchaseQuantity)} {item.purchaseUnit}</p>
                <RequirementDetails item={item} />
                <div className="grocery-item-actions">
                  <button className="btn btn-secondary" type="button" onClick={() => onEdit(item)}>
                    Edit
                  </button>
                  <button
                    className="btn btn-danger-secondary"
                    type="button"
                    aria-label={`Remove ${item.name} from Grocery List`}
                    onClick={() => onRemove(item)}
                  >
                    Remove
                  </button>
                </div>
              </article>
            ))}
          </section>
        ))}
      </div>
    </>
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
