import { useState } from 'react';
import type { GroceryItem } from '../services/groceryListApi';

type Field = 'quantity' | 'note' | 'department';
type Choice = 'mine' | 'latest';
export interface GroceryEditConflict {
  field: Field;
  mine: string;
  latest: string;
  choice: Choice | null;
}

interface Review {
  item: GroceryItem;
  version: number;
  selectedStoreId: string | null;
  targetStoreId: string | null;
  conflicts: GroceryEditConflict[];
}

function getDepartmentId(item: GroceryItem, storeId: string | null): string {
  if (!storeId) return '';
  if (!item.ingredientId) return item.adHocStoreId === storeId ? item.adHocDepartmentId ?? '' : '';
  return item.storeMappings.find((mapping) => mapping.storeId === storeId)?.departmentId ?? '';
}

export function useGroceryEditDraft(item: GroceryItem, listVersion: number, selectedStoreId: string | null) {
  const [quantity, setQuantity] = useState(String(item.outstandingPurchaseQuantity));
  const [note, setNote] = useState(item.note ?? '');
  const [baseline, setBaseline] = useState({ quantity: String(item.outstandingPurchaseQuantity), note: item.note ?? '' });
  const [reviewedStoreId, setReviewedStoreId] = useState(selectedStoreId);
  const [departmentDrafts, setDepartmentDrafts] = useState<Record<string, string>>(() => ({
    ...(selectedStoreId ? { [selectedStoreId]: getDepartmentId(item, selectedStoreId) } : {}),
  }));
  const [originalDepartments, setOriginalDepartments] = useState<Record<string, string>>(() => ({
    ...(selectedStoreId ? { [selectedStoreId]: getDepartmentId(item, selectedStoreId) } : {}),
  }));
  const [expectedListVersion, setExpectedListVersion] = useState(listVersion);
  const [expectedIngredientVersion, setExpectedIngredientVersion] = useState(item.ingredientVersion);
  const [review, setReview] = useState<Review | null>(null);
  const [reviewError, setReviewError] = useState<string | null>(null);
  const currentDepartment = reviewedStoreId ? departmentDrafts[reviewedStoreId] ?? '' : '';
  const initialDepartment = reviewedStoreId ? originalDepartments[reviewedStoreId] ?? '' : '';

  function finishReview(snapshot: Review, conflicts: GroceryEditConflict[]) {
    const resolve = (field: Field, mine: string, previous: string, latest: string) => {
      const conflict = conflicts.find((value) => value.field === field);
      if (conflict) return conflict.choice === 'latest' ? latest : mine;
      return mine === previous ? latest : mine;
    };
    const latestQuantity = String(snapshot.item.outstandingPurchaseQuantity);
    const latestNote = snapshot.item.note ?? '';
    setQuantity(resolve('quantity', quantity, baseline.quantity, latestQuantity));
    setNote(resolve('note', note, baseline.note, latestNote));
    setBaseline({ quantity: latestQuantity, note: latestNote });
    const storeId = snapshot.targetStoreId;
    setReviewedStoreId(storeId);
    if (storeId) {
      const latest = getDepartmentId(snapshot.item, storeId);
      const previous = originalDepartments[storeId] ?? latest;
      const mine = departmentDrafts[storeId] ?? latest;
      setDepartmentDrafts((current) => ({ ...current, [storeId]: resolve('department', mine, previous, latest) }));
      setOriginalDepartments((current) => ({ ...current, [storeId]: latest }));
    }
    setExpectedListVersion(snapshot.version);
    setExpectedIngredientVersion(snapshot.item.ingredientVersion);
    setReview(null);
    setReviewError(null);
  }

  function startReview(reviewStore: boolean) {
    const targetStoreId = reviewStore ? selectedStoreId : reviewedStoreId;
    const conflicts: GroceryEditConflict[] = [];
    function check(field: Field, mine: string, previous: string, latest: string) {
      if (mine !== previous && latest !== previous && mine !== latest) {
        conflicts.push({ field, mine, latest, choice: null });
      }
    }
    check('quantity', quantity, baseline.quantity, String(item.outstandingPurchaseQuantity));
    check('note', note, baseline.note, item.note ?? '');
    if (targetStoreId && targetStoreId in originalDepartments) {
      const latest = getDepartmentId(item, targetStoreId);
      check('department', departmentDrafts[targetStoreId] ?? latest, originalDepartments[targetStoreId], latest);
    }
    const snapshot = { item, version: listVersion, selectedStoreId, targetStoreId, conflicts };
    setReviewError(null);
    if (conflicts.length) setReview(snapshot);
    else finishReview(snapshot, conflicts);
  }

  function applyReview() {
    if (!review || review.conflicts.some((conflict) => conflict.choice === null)) return;
    if (review.version !== listVersion || review.item.ingredientVersion !== item.ingredientVersion
      || review.selectedStoreId !== selectedStoreId) {
      setReview(null);
      setReviewError('The list or store changed again. Review the latest values before saving.');
      return;
    }
    finishReview(review, review.conflicts);
  }

  return {
    quantity, setQuantity, note, setNote, reviewedStoreId,
    currentDepartment, departmentChanged: currentDepartment !== initialDepartment,
    setDepartment: (value: string) => {
      if (reviewedStoreId) setDepartmentDrafts((current) => ({ ...current, [reviewedStoreId]: value }));
    },
    expectedListVersion, expectedIngredientVersion,
    latestListNeedsReview: expectedListVersion !== listVersion,
    storeSelectionNeedsReview: reviewedStoreId !== selectedStoreId,
    ingredientNeedsReview: item.ingredientVersion !== expectedIngredientVersion,
    conflicts: review?.conflicts ?? [], reviewError,
    startReview, applyReview,
    choose: (field: Field, choice: Choice) => setReview((current) => current && ({
      ...current,
      conflicts: current.conflicts.map((conflict) => conflict.field === field ? { ...conflict, choice } : conflict),
    })),
  };
}
