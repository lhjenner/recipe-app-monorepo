import { useEffect, useRef, useState } from 'react';
import { getDepartments, type LookupValue, type SupermarketStore } from '../../ingredients/services/ingredientsApi';
import {
  updateGroceryItem,
  type GroceryItem,
  type GroceryItemUpdateRequest,
} from '../services/groceryListApi';
import { useGroceryCommand } from '../hooks/useGroceryCommand';
import { useGroceryEditDraft } from '../hooks/useGroceryEditDraft';
import ModalContainer from '../../../shared/components/ModalContainer';
import GroceryEditConflicts from './GroceryEditConflicts';
import GroceryEditDialogStatus from './GroceryEditDialogStatus';
import GroceryItemEditForm from './GroceryItemEditForm';

interface GroceryItemEditDialogProps {
  item: GroceryItem;
  listVersion: number;
  selectedStoreId: string | null;
  stores: SupermarketStore[];
  onClose: () => void;
  onSaved: () => void;
}

function GroceryItemEditDialog({
  item,
  listVersion,
  selectedStoreId,
  stores,
  onClose,
  onSaved,
}: GroceryItemEditDialogProps) {
  const draft = useGroceryEditDraft(item, listVersion, selectedStoreId);
  const {
    quantity, note, reviewedStoreId, expectedListVersion, expectedIngredientVersion,
    currentDepartment, departmentChanged, latestListNeedsReview,
    storeSelectionNeedsReview, ingredientNeedsReview,
  } = draft;
  const [departments, setDepartments] = useState<LookupValue[]>([]);
  const [lookupError, setLookupError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [showConfirmation, setShowConfirmation] = useState(false);
  const commandId = useRef(crypto.randomUUID());
  const mutation = useGroceryCommand(
    (request: GroceryItemUpdateRequest) => updateGroceryItem(item.id, request),
    () => {
      onSaved();
      onClose();
    },
  );
  const selectedStore = stores.find((store) => store.id === reviewedStoreId);
  const needsReview = latestListNeedsReview || storeSelectionNeedsReview || ingredientNeedsReview
    || draft.conflicts.length > 0;

  useEffect(() => {
    const controller = new AbortController();
    void getDepartments(controller.signal)
      .then(setDepartments)
      .catch((requestError: unknown) => {
        if (!controller.signal.aborted) {
          setLookupError(requestError instanceof Error
            ? requestError.message
            : 'Unable to load supermarket departments.');
        }
      });
    return () => controller.abort();
  }, []);

  function updateDraft(action: () => void) {
    if (mutation.locked) {
      return;
    }
    action();
    commandId.current = crypto.randomUUID();
    setShowConfirmation(false);
    setError(null);
    mutation.clearError();
  }

  function reviewLatest(reviewStore: boolean) {
    if (mutation.locked) {
      return;
    }
    draft.startReview(reviewStore);
    commandId.current = crypto.randomUUID();
    setShowConfirmation(false);
    setError(null);
    mutation.clearError();
  }

  function save(approveDepartmentChange = false, discardDepartmentChange = false) {
    if (mutation.locked) {
      return;
    }
    if (needsReview) {
      setError('Review the latest list, Ingredient, and selected supermarket before saving this draft.');
      return;
    }
    setError(null);
    const changeAdHocDepartment = !item.ingredientId && departmentChanged
      && !discardDepartmentChange && reviewedStoreId !== null;
    mutation.submit({
      commandId: commandId.current,
      expectedListVersion,
      outstandingPurchaseQuantity: Number(quantity),
      purchaseUnit: item.purchaseUnit,
      note: note || null,
      departmentUpdate: approveDepartmentChange && reviewedStoreId && expectedIngredientVersion !== null
        ? {
          storeId: reviewedStoreId,
          departmentId: currentDepartment || null,
          expectedIngredientVersion,
        }
        : null,
      adHocStoreId: item.ingredientId ? null
        : changeAdHocDepartment ? reviewedStoreId : item.adHocStoreId,
      adHocDepartmentId: item.ingredientId
        ? null
        : changeAdHocDepartment ? currentDepartment || null : item.adHocDepartmentId,
    });
  }

  function submit() {
    if (needsReview) {
      setError('Review the latest list, Ingredient, and selected supermarket before saving this draft.');
      return;
    }
    if (item.ingredientId && departmentChanged) {
      setShowConfirmation(true);
      return;
    }
    void save();
  }

  return (
    <ModalContainer labelledBy="grocery-edit-heading" className="grocery-entry-dialog"
      initialFocusSelector="input" locked={mutation.locked} onClose={onClose}>
      <GroceryEditDialogStatus
        itemName={item.name}
        isSaving={mutation.locked}
        latestListNeedsReview={latestListNeedsReview}
        storeSelectionNeedsReview={storeSelectionNeedsReview}
        ingredientNeedsReview={ingredientNeedsReview}
        selectedStoreName={stores.find((store) => store.id === selectedStoreId)?.name ?? null}
        lookupError={lookupError}
        error={error ?? draft.reviewError ?? mutation.error?.message ?? null}
        fieldErrors={mutation.error?.fieldErrors ?? {}}
        onClose={onClose}
        onReviewLatest={() => reviewLatest(false)}
        onReviewStore={() => reviewLatest(true)}
      />
      {mutation.canRetry && (
        <p role="status">
          The outcome is unknown. Retry the original request before reviewing or changing this draft.
          <button className="btn btn-secondary" type="button" onClick={mutation.retry}>Retry edit</button>
        </p>
      )}
      <GroceryEditConflicts conflicts={draft.conflicts} departments={departments}
        onChoose={draft.choose} onApply={draft.applyReview} />

      <GroceryItemEditForm
        item={item}
        quantity={quantity}
        note={note}
        selectedStore={selectedStore}
        departmentId={currentDepartment}
        departments={departments}
        isSaving={mutation.isSaving}
        locked={mutation.locked || draft.conflicts.length > 0}
        showConfirmation={showConfirmation}
        canApprove={expectedIngredientVersion !== null}
        needsReview={needsReview}
        onSubmit={submit}
        onQuantityChange={(value) => updateDraft(() => draft.setQuantity(value))}
        onNoteChange={(value) => updateDraft(() => draft.setNote(value))}
        onDepartmentChange={(departmentId) => updateDraft(() => draft.setDepartment(departmentId))}
        onApprove={() => save(true)}
        onDiscard={() => save(false, true)}
        onCancelConfirmation={() => setShowConfirmation(false)}
        onClose={onClose}
      />
    </ModalContainer>
  );
}

export default GroceryItemEditDialog;
