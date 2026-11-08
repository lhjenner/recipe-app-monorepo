import { useEffect, useState } from 'react';
import { getSupermarkets, type SupermarketChain } from '../services/ingredientsApi';

interface SupermarketState {
  data: SupermarketChain[] | null;
  isLoading: boolean;
  error: string | null;
  reload: () => void;
}

export function useSupermarkets(): SupermarketState {
  const [result, setResult] = useState<{
    token: number;
    data: SupermarketChain[] | null;
    error: string | null;
  } | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    void getSupermarkets(controller.signal)
      .then((data) => setResult({ token: reloadToken, data, error: null }))
      .catch((requestError: unknown) => {
        if (!controller.signal.aborted) {
          setResult({
            token: reloadToken,
            data: null,
            error: requestError instanceof Error
              ? requestError.message
              : 'Unable to load supermarket reference data.',
          });
        }
      });

    return () => controller.abort();
  }, [reloadToken]);

  const isCurrentResult = result?.token === reloadToken;
  return {
    data: isCurrentResult ? result.data : null,
    isLoading: !isCurrentResult,
    error: isCurrentResult ? result.error : null,
    reload: () => setReloadToken((token) => token + 1),
  };
}
