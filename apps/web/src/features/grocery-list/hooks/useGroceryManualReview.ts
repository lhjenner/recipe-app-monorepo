import { useRef, useState } from 'react';
import {
  addGroceryItems,
  type GroceryDuplicateReview,
  type GroceryManualEntryCommand,
} from '../services/groceryListApi';
import { useGroceryCommand } from './useGroceryCommand';

interface ReviewState {
  command: GroceryManualEntryCommand;
  duplicates: GroceryDuplicateReview[];
  removed: number[];
}

export function useGroceryManualReview(onAdded: () => void, onClose: () => void) {
  const [review, setReview] = useState<ReviewState | null>(null);
  const submitted = useRef<GroceryManualEntryCommand | null>(null);
  const mutation = useGroceryCommand(addGroceryItems, () => {
    onAdded();
    onClose();
  }, (failure) => {
    if (failure.duplicateReviews.length > 0 && submitted.current) {
      setReview({ command: submitted.current, duplicates: failure.duplicateReviews, removed: [] });
    } else if (failure.status === 409) {
      onAdded();
    }
  });

  function submit(command: GroceryManualEntryCommand) {
    if (mutation.locked) return;
    if (mutation.submit(command)) {
      submitted.current = command;
    }
  }

  function backToEditing() {
    if (mutation.locked) return;
    setReview(null);
    mutation.clearError();
  }

  function removeDuplicate(entryIndex: number) {
    if (mutation.locked) return;
    setReview((current) => current
      ? { ...current, removed: [...current.removed, entryIndex] }
      : null);
  }

  function submitReview(skipDuplicates: boolean) {
    if (!review || mutation.locked) return;
    const duplicateIndexes = new Set(review.duplicates.map((duplicate) => duplicate.entryIndex));
    const selected = skipDuplicates
      ? []
      : review.duplicates.filter((duplicate) => !review.removed.includes(duplicate.entryIndex));
    const selectedIndexes = new Set(selected.map((duplicate) => duplicate.entryIndex));
    const retainedIndexes = review.command.items
      .map((_, index) => index)
      .filter((index) => !duplicateIndexes.has(index) || selectedIndexes.has(index));
    if (retainedIndexes.length === 0) {
      onClose();
      return;
    }

    submit({
      ...review.command,
      commandId: crypto.randomUUID(),
      items: retainedIndexes.map((index) => review.command.items[index]),
      reviewedDuplicates: selected.map((duplicate) => ({
        ...duplicate,
        entryIndex: retainedIndexes.indexOf(duplicate.entryIndex),
      })),
    });
  }

  const visibleDuplicates = review?.duplicates.filter(
    (duplicate) => !review.removed.includes(duplicate.entryIndex),
  ) ?? [];
  const otherCount = review
    ? review.command.items.length - review.duplicates.length
    : 0;
  const reviewError = mutation.error && mutation.error.duplicateReviews.length === 0
    ? mutation.error
    : null;

  return {
    mutation,
    review,
    visibleDuplicates,
    otherCount,
    reviewError,
    submit,
    submitReview,
    backToEditing,
    removeDuplicate,
  };
}
