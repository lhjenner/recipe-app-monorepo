import { useEffect, useState } from 'react';
import { BrowserRouter, Navigate, Route, Routes, useNavigate } from 'react-router-dom';
import { getCurrentUser, logout, type UserDto } from './services/authApi';
import LoginPage from './LoginPage';
import RegistrationPage from './RegistrationPage';
import TemporaryLandingPage from './TemporaryLandingPage';

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

    void getCurrentUser()
      .then((currentUser) => {
        if (isActive) {
          setUser(currentUser);
        }
      })
      .catch(() => {
        if (isActive) {
          setUser(null);
          setSessionNotice('We could not check your current session. You can still log in.');
        }
      })
      .finally(() => {
        if (isActive) {
          setSessionChecked(true);
        }
      });

    return () => {
      isActive = false;
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
      <Route
        path="/home"
        element={!sessionChecked
          ? checkingSession
          : user
            ? <TemporaryLandingPage user={user} onLogout={handleLogout} />
            : <Navigate to="/" replace />}
      />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default App
