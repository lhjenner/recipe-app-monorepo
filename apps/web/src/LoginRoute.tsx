import { useState } from 'react';
import { Navigate, useSearchParams } from 'react-router-dom';
import LoginPage from './LoginPage';
import { SessionLoading } from './ProtectedRoute';
import { getSafeReturnTo } from './returnTo';
import type { UserDto } from './services/authApi';

interface LoginRouteProps {
  sessionChecked: boolean;
  authenticated: boolean;
  sessionNotice: string;
  onLoginSuccess: (user: UserDto) => void;
}

export default function LoginRoute({ sessionChecked, authenticated, sessionNotice, onLoginSuccess }: LoginRouteProps) {
  const [searchParams] = useSearchParams();
  const [destination, setDestination] = useState<string | null>(null);

  if (!sessionChecked) {
    return <SessionLoading />;
  }

  if (authenticated) {
    return <Navigate to={destination ?? '/home'} replace />;
  }

  function handleLoginSuccess(user: UserDto) {
    setDestination(getSafeReturnTo(searchParams.get('returnTo')));
    onLoginSuccess(user);
  }

  return <LoginPage onLoginSuccess={handleLoginSuccess} sessionNotice={sessionNotice} />;
}
