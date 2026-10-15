import { useEffect, useState } from 'react';
import { BrowserRouter, Navigate, Route, Routes, useNavigate } from 'react-router-dom';
import AppShell from './AppShell';
import { getCurrentUser, logout, type UserDto } from './services/authApi';
import LoginPage from './LoginPage';
import ForgotPasswordPage from './ForgotPasswordPage';
import ResetPasswordPage from './ResetPasswordPage';
import RegistrationPage from './RegistrationPage';
import HomePage from './HomePage';
import NotFoundPage from './NotFoundPage';
import { navigationItems } from './navigation';
import StubPage from './StubPage';

function App() {
  return (
    <BrowserRouter>
      <ApplicationRoutes />
    </BrowserRouter>
  );
}

function ApplicationRoutes() {
  const navigate = useNavigate();
  const [user, setUser] = useState<UserDto | null>(null);
  const [sessionChecked, setSessionChecked] = useState(false);
  const [sessionNotice, setSessionNotice] = useState('');

  useEffect(() => {
    let isActive = true;

    async function checkSession() {
      try {
        const currentUser = await getCurrentUser();
        if (isActive) {
          setUser(currentUser);
          setSessionNotice('');
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

  function handleLoginSuccess(currentUser: UserDto) {
    setUser(currentUser);
    setSessionNotice('');
    navigate('/home', { replace: true });
  }

  async function handleLogout() {
    await logout();
    setUser(null);
    navigate('/', { replace: true });
  }

  const checkingSession = <main className="auth-loading" role="status">Checking your session…</main>;
  const authenticatedApp = !sessionChecked
    ? checkingSession
    : user
      ? <AppShell onLogout={handleLogout} />
      : <Navigate to="/" replace />;
  const [homeItem, ...stubItems] = navigationItems;

  return (
    <Routes>
      <Route
        path="/"
        element={!sessionChecked
          ? checkingSession
          : user
            ? <Navigate to="/home" replace />
            : <LoginPage onLoginSuccess={handleLoginSuccess} sessionNotice={sessionNotice} />}
      />
      <Route path="/register" element={<RegistrationPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />
      <Route element={authenticatedApp}>
        <Route path={homeItem.route} element={<HomePage pageTitle={homeItem.pageTitle} />} />
        {stubItems.map((item) => (
          <Route key={item.route} path={item.route} element={<StubPage pageTitle={item.pageTitle} />} />
        ))}
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}

export default App
