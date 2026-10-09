import { useState } from 'react';
import { Link } from 'react-router-dom';
import { AuthApiError, type UserDto } from './services/authApi';

interface TemporaryLandingPageProps {
  user: UserDto;
  onLogout: () => Promise<void>;
}

function TemporaryLandingPage({ user, onLogout }: TemporaryLandingPageProps) {
  const [isLoggingOut, setIsLoggingOut] = useState(false);
  const [error, setError] = useState('');

  async function handleLogout() {
    setIsLoggingOut(true);
    setError('');

    try {
      await onLogout();
    } catch (logoutError) {
      setError(logoutError instanceof AuthApiError
        ? logoutError.message
        : 'Unable to log out. Please try again.');
    } finally {
      setIsLoggingOut(false);
    }
  }

  return (
    <div className="login-shell">
      <header className="login-header">
        <Link className="brand" to="/home" aria-label="Recipe App home">
          <span className="brand-mark" aria-hidden="true">R</span>
          <span>Recipe App</span>
        </Link>
        <span className="header-note">A LITTLE MORE ROOM AT THE TABLE</span>
      </header>

      <main className="login-main">
        <section className="login-panel registration-panel" aria-labelledby="home-heading">
          <div className="panel-heading">
            <p className="eyebrow">YOUR KITCHEN NOTEBOOK</p>
            <h1 id="home-heading">You're signed in<span>.</span></h1>
            <p className="panel-copy">Signed in as {user.email}</p>
          </div>

          {error && <p className="form-error" role="alert">{error}</p>}
          <button className="btn btn-primary login-submit" type="button" onClick={handleLogout} disabled={isLoggingOut}>
            {isLoggingOut
              ? <span className="login-submit-content"><span className="login-spinner" aria-hidden="true" />Logging out…</span>
              : 'Log out'}
          </button>
        </section>
      </main>

      <footer className="login-footer">
        <span>RECIPE APP</span>
        <span>Made for the everyday table</span>
      </footer>
    </div>
  );
}

export default TemporaryLandingPage;
