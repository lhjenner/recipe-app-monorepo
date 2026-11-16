import { useEffect, useRef, useState } from 'react';
import { getDepartments, type LookupValue, type SupermarketStore } from '../../ingredients/services/ingredientsApi';
import {
  GroceryListApiError,
  updateGroceryItem,
  type GroceryItem,
} from '../services/groceryListApi';
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
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [showConfirmation, setShowConfirmation] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const commandId = useRef(crypto.randomUUID());
  const latestListNeedsReview = expectedListVersion !== listVersion;
  const storeSelectionNeedsReview = reviewedStoreId !== selectedStoreId;
  const ingredientNeedsReview = item.ingredientVersion !== expectedIngredientVersion;
  const currentDepartment = reviewedStoreId ? departmentDrafts[reviewedStoreId] ?? '' : '';
  const initialDepartment = reviewedStoreId ? originalDepartments[reviewedStoreId] ?? '' : '';
  const departmentChanged = currentDepartment !== initialDepartment;
  const selectedStore = stores.find((store) => store.id === reviewedStoreId);

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
    action();
    commandId.current = crypto.randomUUID();
    setShowConfirmation(false);
    setError(null);
    setFieldErrors({});
  }

  function reviewSelectedStore() {
    if (!selectedStoreId) {
      return;
    }
    const departmentId = getDepartmentId(item, selectedStoreId);
    setReviewedStoreId(selectedStoreId);
    setDepartmentDrafts((current) => ({
      ...current,
      [selectedStoreId]: current[selectedStoreId] ?? departmentId,
    }));
    setOriginalDepartments((current) => ({
      ...current,
      [selectedStoreId]: departmentId,
    }));
    setExpectedListVersion(listVersion);
    setExpectedIngredientVersion(item.ingredientVersion);
    commandId.current = crypto.randomUUID();
  }

  function reviewLatestList() {
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
  }

  async function save(approveDepartmentChange = false, discardDepartmentChange = false) {
    setError(null);
    setFieldErrors({});
    setIsSaving(true);
    try {
      await updateGroceryItem(item.id, {
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
        adHocStoreId: item.ingredientId
          ? null
          : reviewedStoreId ?? item.adHocStoreId,
        adHocDepartmentId: item.ingredientId
          ? null
          : discardDepartmentChange
            ? item.adHocDepartmentId
            : reviewedStoreId
              ? currentDepartment || null
              : item.adHocDepartmentId,
      });
      onSaved();
      onClose();
    } catch (requestError: unknown) {
      if (requestError instanceof GroceryListApiError) {
        setError(requestError.message);
        setFieldErrors(requestError.fieldErrors);
      } else {
        setError(requestError instanceof Error
          ? requestError.message
          : 'Unable to save this grocery item.');
      }
    } finally {
      setIsSaving(false);
    }
  }

  function submit() {
    if (latestListNeedsReview || storeSelectionNeedsReview || ingredientNeedsReview) {
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
    <div className="grocery-modal-backdrop">
      <section
        className="grocery-entry-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="grocery-edit-heading"
      >
        <GroceryEditDialogStatus
          itemName={item.name}
          isSaving={isSaving}
          latestListNeedsReview={latestListNeedsReview}
          storeSelectionNeedsReview={storeSelectionNeedsReview}
          ingredientNeedsReview={ingredientNeedsReview}
          selectedStoreName={stores.find((store) => store.id === selectedStoreId)?.name ?? null}
          lookupError={lookupError}
          error={error}
          fieldErrors={fieldErrors}
          onClose={onClose}
          onReviewLatest={reviewLatestList}
          onReviewStore={reviewSelectedStore}
        />

        <GroceryItemEditForm
          item={item}
          quantity={quantity}
          note={note}
          selectedStore={selectedStore}
          departmentId={currentDepartment}
          departments={departments}
          isSaving={isSaving}
          showConfirmation={showConfirmation}
          canApprove={expectedIngredientVersion !== null}
          needsReview={latestListNeedsReview || storeSelectionNeedsReview || ingredientNeedsReview}
          onSubmit={submit}
          onQuantityChange={(value) => updateDraft(() => setQuantity(value))}
          onNoteChange={(value) => updateDraft(() => setNote(value))}
          onDepartmentChange={(departmentId) => updateDraft(() =>
            setDepartmentDrafts((current) => ({
              ...current,
              [reviewedStoreId!]: departmentId,
            })))}
          onApprove={() => void save(true)}
          onDiscard={() => void save(false, true)}
          onCancelConfirmation={() => setShowConfirmation(false)}
          onClose={onClose}
        />
      </section>
    </div>
  );
}

export default GroceryItemEditDialog;
