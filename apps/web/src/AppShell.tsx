import { useState } from 'react';
import { Link, Outlet } from 'react-router-dom';
import { navigationItems } from './navigation';
import { AuthApiError } from './services/authApi';
import './styles/app-shell.css';

interface AppShellProps {
  onLogout: () => Promise<void>;
}

function AppShell({ onLogout }: AppShellProps) {
  const [isLoggingOut, setIsLoggingOut] = useState(false);
  const [logoutError, setLogoutError] = useState('');

  async function handleLogout() {
    setIsLoggingOut(true);
    setLogoutError('');

    try {
      await onLogout();
    } catch (error) {
      setLogoutError(error instanceof AuthApiError
        ? error.message
        : 'Unable to log out. Please try again.');
    } finally {
      setIsLoggingOut(false);
    }
  }

  return (
    <div className="app-shell">
      <header className="login-header app-header" data-testid="app-header">
        <Link className="brand" to="/home" aria-label="Recipe App home">
          <span className="brand-mark" aria-hidden="true">R</span>
          <span>Recipe App</span>
        </Link>
        <nav className="app-navigation" aria-label="Main">
          <ul className="app-navigation-list">
            {navigationItems.map((item) => (
              <li key={item.route}>
                <Link className="app-navigation-link" to={item.route}>{item.label}</Link>
              </li>
            ))}
          </ul>
        </nav>
        <button className="btn btn-danger app-logout" type="button" onClick={handleLogout} disabled={isLoggingOut}>
          {isLoggingOut ? 'Logging out…' : 'Log out'}
        </button>
        {logoutError && <p className="app-logout-error" role="alert">{logoutError}</p>}
      </header>
      <Outlet />
    </div>
  );
}

export default AppShell;
