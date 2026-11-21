import { useState } from 'react';
import {
  addRecipeToGroceryList,
  getRecipeGroceryPreview,
  GroceryListApiError,
  removeRecipeGroceryRequirements,
  type GroceryRecipeAddCommand,
  type GroceryRecipePreview,
  type GroceryRecipeRemovalCommand,
} from '../../grocery-list/services/groceryListApi';

type PendingCommand =
  | { kind: 'add'; command: GroceryRecipeAddCommand }
  | { kind: 'remove'; command: GroceryRecipeRemovalCommand };

export function useRecipeGroceryActions(recipeId: string, onChanged?: () => void) {
  const [isOpen, setIsOpen] = useState(false);
  const [preview, setPreview] = useState<GroceryRecipePreview | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [pendingCommand, setPendingCommand] = useState<PendingCommand | null>(null);
  const [canRetry, setCanRetry] = useState(false);
  const [needsFreshReview, setNeedsFreshReview] = useState(false);
  const [mode, setMode] = useState<'add' | 'remove'>('add');
  const locked = isSubmitting || canRetry;

  async function loadPreview() {
    setIsLoading(true);
    setError(null);
    setNeedsFreshReview(false);
    setPendingCommand(null);
    setCanRetry(false);
    try {
      setPreview(await getRecipeGroceryPreview(recipeId));
    } catch (requestError: unknown) {
      setError(requestError instanceof Error
        ? requestError.message
        : 'Unable to review this Recipe. Please try again.');
      setPreview(null);
    } finally {
      setIsLoading(false);
    }
  }

  function openReview(intent: 'add' | 'remove' = 'add') {
    setMode(intent);
    setNotice(null);
    setPreview(null);
    setIsOpen(true);
    void loadPreview();
  }

  async function runCommand(command: PendingCommand) {
    setIsSubmitting(true);
    setError(null);
    setPendingCommand(command);
    try {
      if (command.kind === 'add') {
        await addRecipeToGroceryList(recipeId, command.command);
        setNotice('Recipe requirements were added to your Grocery List.');
      } else {
        await removeRecipeGroceryRequirements(recipeId, command.command);
        setNotice('Outstanding requirements from this Recipe were removed.');
      }
      setPendingCommand(null);
      setCanRetry(false);
      setIsOpen(false);
      onChanged?.();
    } catch (requestError: unknown) {
      const status = requestError instanceof GroceryListApiError ? requestError.status : -1;
      setError(requestError instanceof Error
        ? requestError.message
        : 'The Recipe grocery command failed.');
      setCanRetry(status === 0);
      if (status !== 0) {
        setPendingCommand(null);
        setNeedsFreshReview(status === 409);
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  function addRecipe() {
    if (!preview || preview.included.length === 0 || needsFreshReview) {
      return;
    }
    void runCommand({
      kind: 'add',
      command: {
        commandId: crypto.randomUUID(),
        expectedListVersion: preview.listVersion,
        expectedRecipeVersion: preview.recipeVersion,
        confirmRepeat: preview.hasOutstandingAddition,
      },
    });
  }

  function removeRecipe() {
    if (!preview || needsFreshReview) {
      return;
    }
    void runCommand({
      kind: 'remove',
      command: {
        commandId: crypto.randomUUID(),
        expectedListVersion: preview.listVersion,
      },
    });
  }

  function retryCommand() {
    if (pendingCommand) {
      void runCommand(pendingCommand);
    }
  }

  function closeReview() {
    if (!locked) {
      setIsOpen(false);
    }
  }

  return {
    isOpen,
    preview,
    isLoading,
    isSubmitting,
    error,
    notice,
    canRetry,
    needsFreshReview,
    mode,
    locked,
    loadPreview,
    openReview,
    addRecipe,
    removeRecipe,
    retryCommand,
    closeReview,
  };
}
