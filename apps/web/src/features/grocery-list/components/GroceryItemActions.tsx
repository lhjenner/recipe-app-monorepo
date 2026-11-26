import IconButton from '../../../shared/components/IconButton';
import { Check, Pencil, Trash2 } from '../../../shared/components/icons';
import type { GroceryItem } from '../services/groceryListApi';

interface GroceryItemActionsProps {
  item: GroceryItem;
  disabled: boolean;
  onEdit: (item: GroceryItem) => void;
  onRemove: (item: GroceryItem) => void;
  onPurchase?: (item: GroceryItem) => void;
}

function GroceryItemActions({ item, disabled, onEdit, onRemove, onPurchase }: GroceryItemActionsProps) {
  return (
    <div className="grocery-item-actions">
      {onPurchase && (
        <IconButton
          label="Mark purchased"
          tooltip="Mark purchased"
          icon={<Check aria-hidden="true" />}
          variant="btn-primary"
          disabled={disabled}
          onClick={() => onPurchase(item)}
        />
      )}
      <IconButton
        label="Edit"
        tooltip="Edit"
        icon={<Pencil aria-hidden="true" />}
        variant="btn-secondary"
        disabled={disabled}
        onClick={() => onEdit(item)}
      />
      <IconButton
        label={`Remove ${item.name} from Grocery List`}
        tooltip="Remove"
        icon={<Trash2 aria-hidden="true" />}
        variant="btn-danger-secondary"
        disabled={disabled}
        onClick={() => onRemove(item)}
      />
    </div>
  );
}

export default GroceryItemActions;
