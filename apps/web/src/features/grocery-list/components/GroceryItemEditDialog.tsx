import { useEffect, useRef, useState } from 'react';
import { getDepartments, type LookupValue, type SupermarketStore } from '../../ingredients/services/ingredientsApi';
import {
  updateGroceryItem,
  type GroceryItem,
  type GroceryItemUpdateRequest,
} from '../services/groceryListApi';
import { useGroceryCommand } from '../hooks/useGroceryCommand';
import GroceryDialog from './GroceryDialog';
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

function getDepartmentId(item: GroceryItem, storeId: string | null): string {
  if (!storeId) {
    return '';
  }
  if (!item.ingredientId) {
    return item.adHocStoreId === storeId ? item.adHocDepartmentId ?? '' : '';
  }
  return item.storeMappings.find((mapping) => mapping.storeId === storeId)?.departmentId ?? '';
}

function GroceryItemEditDialog({
  item,
  listVersion,
  selectedStoreId,
  stores,
  onClose,
  onSaved,
}: GroceryItemEditDialogProps) {
  const [quantity, setQuantity] = useState(String(item.outstandingPurchaseQuantity));
  const [note, setNote] = useState(item.note ?? '');
  const [reviewedStoreId, setReviewedStoreId] = useState(selectedStoreId);
  const [departmentDrafts, setDepartmentDrafts] = useState<Record<string, string>>(() => ({
    ...(selectedStoreId ? { [selectedStoreId]: getDepartmentId(item, selectedStoreId) } : {}),
  }));
  const [originalDepartments, setOriginalDepartments] = useState<Record<string, string>>(() => ({
    ...(selectedStoreId ? { [selectedStoreId]: getDepartmentId(item, selectedStoreId) } : {}),
  }));
  const [expectedListVersion, setExpectedListVersion] = useState(listVersion);
  const [expectedIngredientVersion, setExpectedIngredientVersion] = useState(item.ingredientVersion);
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
  const latestListNeedsReview = expectedListVersion !== listVersion;
  const storeSelectionNeedsReview = reviewedStoreId !== selectedStoreId;
  const ingredientNeedsReview = item.ingredientVersion !== expectedIngredientVersion;
  const currentDepartment = reviewedStoreId ? departmentDrafts[reviewedStoreId] ?? '' : '';
  const initialDepartment = reviewedStoreId ? originalDepartments[reviewedStoreId] ?? '' : '';
  const departmentChanged = currentDepartment !== initialDepartment;
  const selectedStore = stores.find((store) => store.id === reviewedStoreId);
  const needsReview = latestListNeedsReview || storeSelectionNeedsReview || ingredientNeedsReview;

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

  function reviewSelectedStore() {
    if (mutation.locked) {
      return;
    }
    setReviewedStoreId(selectedStoreId);
    if (selectedStoreId) {
      const departmentId = getDepartmentId(item, selectedStoreId);
      setDepartmentDrafts((current) => ({
        ...current,
        [selectedStoreId]: current[selectedStoreId] ?? departmentId,
      }));
      setOriginalDepartments((current) => ({
        ...current,
        [selectedStoreId]: departmentId,
      }));
    }
    setExpectedListVersion(listVersion);
    setExpectedIngredientVersion(item.ingredientVersion);
    commandId.current = crypto.randomUUID();
    setShowConfirmation(false);
    setError(null);
    mutation.clearError();
  }

  function reviewLatestList() {
    if (mutation.locked) {
      return;
    }
    setExpectedListVersion(listVersion);
    setExpectedIngredientVersion(item.ingredientVersion);
    if (reviewedStoreId) {
      const currentDepartmentId = getDepartmentId(item, reviewedStoreId);
      setDepartmentDrafts((current) => ({
        ...current,
        [reviewedStoreId]: current[reviewedStoreId] === originalDepartments[reviewedStoreId]
          ? currentDepartmentId
          : current[reviewedStoreId] ?? currentDepartmentId,
      }));
      setOriginalDepartments((current) => ({
        ...current,
        [reviewedStoreId]: currentDepartmentId,
      }));
    }
    setError(null);
    setShowConfirmation(false);
    mutation.clearError();
    commandId.current = crypto.randomUUID();
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
    <GroceryDialog labelledBy="grocery-edit-heading" locked={mutation.locked} onClose={onClose}>
      <GroceryEditDialogStatus
        itemName={item.name}
        isSaving={mutation.locked}
        latestListNeedsReview={latestListNeedsReview}
        storeSelectionNeedsReview={storeSelectionNeedsReview}
        ingredientNeedsReview={ingredientNeedsReview}
        selectedStoreName={stores.find((store) => store.id === selectedStoreId)?.name ?? null}
        lookupError={lookupError}
        error={error ?? mutation.error?.message ?? null}
        fieldErrors={mutation.error?.fieldErrors ?? {}}
        onClose={onClose}
        onReviewLatest={reviewLatestList}
        onReviewStore={reviewSelectedStore}
      />
      {mutation.canRetry && (
        <p role="status">
          The outcome is unknown. Retry the original request before reviewing or changing this draft.
          <button className="btn btn-secondary" type="button" onClick={mutation.retry}>Retry edit</button>
        </p>
      )}

      <GroceryItemEditForm
        item={item}
        quantity={quantity}
        note={note}
        selectedStore={selectedStore}
        departmentId={currentDepartment}
        departments={departments}
        isSaving={mutation.isSaving}
        locked={mutation.locked}
        showConfirmation={showConfirmation}
        canApprove={expectedIngredientVersion !== null}
        needsReview={needsReview}
        onSubmit={submit}
        onQuantityChange={(value) => updateDraft(() => setQuantity(value))}
        onNoteChange={(value) => updateDraft(() => setNote(value))}
        onDepartmentChange={(departmentId) => updateDraft(() =>
          setDepartmentDrafts((current) => ({
            ...current,
            [reviewedStoreId!]: departmentId,
          })))}
        onApprove={() => save(true)}
        onDiscard={() => save(false, true)}
        onCancelConfirmation={() => setShowConfirmation(false)}
        onClose={onClose}
      />
    </GroceryDialog>
  );
}

export default GroceryItemEditDialog;
