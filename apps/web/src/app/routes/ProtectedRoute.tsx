import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';

interface ProtectedRouteProps {
  sessionChecked: boolean;
  authenticated: boolean;
  preserveDestination: boolean;
  children: ReactNode;
}

export function SessionLoading() {
  return <main className="auth-loading" role="status">Checking your session…</main>;
}

export default function ProtectedRoute({ sessionChecked, authenticated, preserveDestination, children }: ProtectedRouteProps) {
  const { pathname, search, hash } = useLocation();

  if (!sessionChecked) {
    return <SessionLoading />;
  }

  if (!authenticated) {
    const query = new URLSearchParams({ returnTo: `${pathname}${search}${hash}` });
    return <Navigate to={preserveDestination ? `/?${query}` : '/'} replace />;
  }

  return children;
}
