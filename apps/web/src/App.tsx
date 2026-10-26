import { useEffect, useState } from 'react';
import { createBrowserRouter, Route, RouterProvider, Routes } from 'react-router-dom';
import AppShell from './AppShell';
import { getCurrentUser, logout, type UserDto } from './services/authApi';
import LoginRoute from './LoginRoute';
import ForgotPasswordPage from './ForgotPasswordPage';
import ResetPasswordPage from './ResetPasswordPage';
import RegistrationPage from './RegistrationPage';
import HomePage from './HomePage';
import NotFoundPage from './NotFoundPage';
import { navigationItems } from './navigation';
import StubPage from './StubPage';
import ProtectedRoute from './ProtectedRoute';
import { SESSION_EXPIRED_EVENT } from './services/apiClient';
import IngredientMaintenancePage from './IngredientMaintenancePage';
import IngredientEditorPage from './IngredientEditorPage';

function App() {
  return <RouterProvider router={router} />;
}

const router = createBrowserRouter([{ path: '*', element: <ApplicationRoutes /> }]);

function ApplicationRoutes() {
  const [user, setUser] = useState<UserDto | null>(null);
  const [sessionChecked, setSessionChecked] = useState(false);
  const [sessionNotice, setSessionNotice] = useState('');
  const [loggedOut, setLoggedOut] = useState(false);

  useEffect(() => {
    let isActive = true;

    async function checkSession() {
      try {
        const currentUser = await getCurrentUser();
        if (isActive) {
          setUser(currentUser);
          if (currentUser) {
            setSessionNotice('');
          }
        }
      } catch {
        if (isActive) {
          setUser(null);
          setSessionNotice('We could not check your current session. You can still log in.');
        }
      } finally {
        if (isActive) {
          setSessionChecked(true);
        }
      }
    }

    function handlePageShow(event: PageTransitionEvent) {
      if (event.persisted) {
        setSessionChecked(false);
        void checkSession();
      }
    }

    void checkSession();
    window.addEventListener('pageshow', handlePageShow);

    return () => {
      isActive = false;
      window.removeEventListener('pageshow', handlePageShow);
    };
  }, []);

  useEffect(() => {
    if (!user) {
      return;
    }

    function handleSessionExpired() {
      setUser(null);
      setSessionNotice('Your session has expired. Please log in again.');
    }

    window.addEventListener(SESSION_EXPIRED_EVENT, handleSessionExpired);
    return () => window.removeEventListener(SESSION_EXPIRED_EVENT, handleSessionExpired);
  }, [user]);

  function handleLoginSuccess(currentUser: UserDto) {
    setUser(currentUser);
    setLoggedOut(false);
    setSessionNotice('');
  }

  async function handleLogout() {
    await logout();
    setLoggedOut(true);
    setUser(null);
    setSessionNotice('');
  }

  const [homeItem, ...stubItems] = navigationItems;

  return (
    <Routes>
      <Route
        path="/"
        element={
          <LoginRoute
            sessionChecked={sessionChecked}
            authenticated={Boolean(user)}
            sessionNotice={sessionNotice}
            onLoginSuccess={handleLoginSuccess}
          />
        }
      />
      <Route path="/register" element={<RegistrationPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />
      <Route element={
        <ProtectedRoute sessionChecked={sessionChecked} authenticated={Boolean(user)} preserveDestination={!loggedOut}>
          <AppShell onLogout={handleLogout} />
        </ProtectedRoute>
      }>
        <Route path={homeItem.route} element={<HomePage pageTitle={homeItem.pageTitle} />} />
        <Route path="/ingredient-maintenance" element={<IngredientMaintenancePage />} />
        <Route path="/ingredient-maintenance/new" element={<IngredientEditorPage />} />
        <Route path="/ingredient-maintenance/:id" element={<IngredientEditorPage />} />
        {stubItems.filter((item) => item.route !== '/ingredient-maintenance').map((item) => (
          <Route key={item.route} path={item.route} element={<StubPage pageTitle={item.pageTitle} />} />
        ))}
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}

export default App
