import { useEffect, useState } from 'react';
import { SESSION_EXPIRED_EVENT } from '../services/apiClient';
import {
  getRecipe,
  getRecipeImage,
  getRecipeList,
  RecipeApiError,
  type RecipeDetail,
  type RecipeList,
} from '../services/recipesApi';

interface RecipeListParameters {
  search: string;
  page: number;
  pageSize: number;
}

interface AsyncData<T> {
  data: T | null;
  isLoading: boolean;
  error: string | null;
  status: number | null;
  reload: () => void;
}

interface RequestResult<T> {
  key: string;
  data: T | null;
  error: string | null;
  status: number | null;
}

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

function errorStatus(error: unknown): number | null {
  return error instanceof RecipeApiError && error.status > 0 ? error.status : null;
}

export function useRecipeList(parameters: RecipeListParameters): AsyncData<RecipeList> {
  const { search, page, pageSize } = parameters;
  const [result, setResult] = useState<RequestResult<RecipeList> | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const requestKey = JSON.stringify([search, page, pageSize, reloadToken]);

  useEffect(() => {
    const controller = new AbortController();
    const timeout = window.setTimeout(() => {
      void getRecipeList({ search, page, pageSize }, controller.signal)
        .then((data) => {
          if (!controller.signal.aborted) {
            setResult({ key: requestKey, data, error: null, status: null });
          }
        })
        .catch((error: unknown) => {
          if (!controller.signal.aborted) {
            setResult({
              key: requestKey,
              data: null,
              error: errorMessage(error, 'Unable to load recipes. Please try again.'),
              status: errorStatus(error),
            });
          }
        });
    }, search.trim() ? 300 : 0);

    return () => {
      window.clearTimeout(timeout);
      controller.abort();
    };
  }, [page, pageSize, requestKey, search]);

  const isCurrentResult = result?.key === requestKey;
  return {
    data: isCurrentResult ? result.data : null,
    isLoading: !isCurrentResult,
    error: isCurrentResult ? result.error : null,
    status: isCurrentResult ? result.status : null,
    reload: () => setReloadToken((token) => token + 1),
  };
}

export function useRecipe(id: string | null): AsyncData<RecipeDetail> {
  const [result, setResult] = useState<RequestResult<RecipeDetail> | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const requestKey = JSON.stringify([id, reloadToken]);

  useEffect(() => {
    if (id === null) {
      return;
    }

    const controller = new AbortController();
    void getRecipe(id, controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) {
          setResult({ key: requestKey, data, error: null, status: null });
        }
      })
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setResult({
            key: requestKey,
            data: null,
            error: errorMessage(error, 'Unable to load this recipe. Please try again.'),
            status: errorStatus(error),
          });
        }
      });

    return () => controller.abort();
  }, [id, requestKey]);

  const isCurrentResult = result?.key === requestKey;
  return {
    data: isCurrentResult ? result.data : null,
    isLoading: id !== null && !isCurrentResult,
    error: isCurrentResult ? result.error : null,
    status: isCurrentResult ? result.status : null,
    reload: () => setReloadToken((token) => token + 1),
  };
}

export interface RecipeImageState {
  url: string | null;
  isLoading: boolean;
  error: string | null;
  status: number | null;
  reload: () => void;
}

export function useRecipeImage(imageUrl: string | null): RecipeImageState {
  const [result, setResult] = useState<RequestResult<string> | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const requestKey = JSON.stringify([imageUrl, reloadToken]);

  useEffect(() => {
    if (imageUrl === null) {
      return;
    }

    const controller = new AbortController();
    let objectUrl: string | null = null;
    const releaseObjectUrl = () => {
      if (objectUrl !== null) {
        URL.revokeObjectURL(objectUrl);
        objectUrl = null;
      }
    };
    const handleSessionExpired = () => {
      controller.abort();
      releaseObjectUrl();
      setResult({
        key: requestKey,
        data: null,
        error: 'Your session has expired. Please sign in again.',
        status: 401,
      });
    };

    window.addEventListener(SESSION_EXPIRED_EVENT, handleSessionExpired);
    void getRecipeImage(imageUrl, controller.signal)
      .then((blob) => {
        if (!controller.signal.aborted) {
          objectUrl = URL.createObjectURL(blob);
          setResult({ key: requestKey, data: objectUrl, error: null, status: null });
        }
      })
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setResult({
            key: requestKey,
            data: null,
            error: errorMessage(error, 'Unable to load this recipe image. Please try again.'),
            status: errorStatus(error),
          });
        }
      });

    return () => {
      window.removeEventListener(SESSION_EXPIRED_EVENT, handleSessionExpired);
      controller.abort();
      releaseObjectUrl();
    };
  }, [imageUrl, requestKey]);

  const isCurrentResult = result?.key === requestKey;
  return {
    url: isCurrentResult ? result.data : null,
    isLoading: imageUrl !== null && !isCurrentResult,
    error: isCurrentResult ? result.error : null,
    status: isCurrentResult ? result.status : null,
    reload: () => setReloadToken((token) => token + 1),
  };
}
