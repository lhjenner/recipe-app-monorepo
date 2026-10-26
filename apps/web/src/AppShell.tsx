import { useEffect, useRef, useState, type KeyboardEvent, type MouseEvent } from 'react';
import { Link, Outlet, useLocation } from 'react-router-dom';
import { useIsMobile } from './hooks/useIsMobile';
import { getActiveNavigationItem, MOBILE_BREAKPOINT, navigationItems, type NavigationItem } from './navigation';
import { AuthApiError } from './services/authApi';
import './styles/app-shell.css';
import './styles/flyout.css';

interface AppShellProps {
  onLogout: () => Promise<void>;
}

function AppShell({ onLogout }: AppShellProps) {
  const { pathname } = useLocation();
  const isMobile = useIsMobile();
  const [isLoggingOut, setIsLoggingOut] = useState(false);
  const [logoutError, setLogoutError] = useState('');
  const [isFlyoutOpen, setIsFlyoutOpen] = useState(false);
  const menuButtonRef = useRef<HTMLButtonElement>(null);
  const flyoutRef = useRef<HTMLElement>(null);
  const activeItem = getActiveNavigationItem(pathname);
  const isFlyoutVisible = isMobile && isFlyoutOpen;

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

  function closeFlyout(restoreFocus = true) {
    setIsFlyoutOpen(false);
    if (restoreFocus) {
      requestAnimationFrame(() => menuButtonRef.current?.focus());
    }
  }

  function handleNavigationClick(event: MouseEvent<HTMLAnchorElement>, item: NavigationItem) {
    if (activeItem?.route === item.route) {
      event.preventDefault();
    }

    if (isMobile) {
      closeFlyout();
    }
  }

  function handleFlyoutKeyDown(event: KeyboardEvent<HTMLElement>) {
    if (event.key !== 'Tab' || !flyoutRef.current) {
      return;
    }

    const focusableElements = Array.from(
      flyoutRef.current.querySelectorAll<HTMLElement>('a[href], button:not(:disabled)'),
    );
    const firstFocusableElement = focusableElements[0];
    const lastFocusableElement = focusableElements.at(-1);

    if (!firstFocusableElement || !lastFocusableElement) {
      return;
    }

    if (event.shiftKey && document.activeElement === firstFocusableElement) {
      event.preventDefault();
      lastFocusableElement.focus();
    } else if (!event.shiftKey && document.activeElement === lastFocusableElement) {
      event.preventDefault();
      firstFocusableElement.focus();
    }
  }

  useEffect(() => {
    function closeFlyoutOnDesktop() {
      if (!window.matchMedia(`(max-width: ${MOBILE_BREAKPOINT}px)`).matches) {
        setIsFlyoutOpen(false);
      }
    }

    window.addEventListener('resize', closeFlyoutOnDesktop);
    return () => window.removeEventListener('resize', closeFlyoutOnDesktop);
  }, []);

  useEffect(() => {
    if (!isFlyoutVisible) {
      return;
    }

    document.body.classList.add('mobile-flyout-open');
    flyoutRef.current?.querySelector<HTMLElement>('a[href], button:not(:disabled)')?.focus();

    function handleKeyDown(event: globalThis.KeyboardEvent) {
      if (event.key === 'Escape') {
        closeFlyout();
      }
    }

    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.body.classList.remove('mobile-flyout-open');
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [isFlyoutVisible]);

  return (
    <div className="app-shell">
      {isMobile
        ? (
          <>
            <header className="login-header app-header app-header-mobile" data-testid="app-header">
              <button
                ref={menuButtonRef}
                className="mobile-menu-button"
                type="button"
                aria-controls="mobile-flyout"
                aria-expanded={isFlyoutOpen}
                aria-label={isFlyoutOpen ? 'Close menu' : 'Open menu'}
                data-testid="mobile-menu-button"
                onClick={() => isFlyoutOpen ? closeFlyout(false) : setIsFlyoutOpen(true)}
              >
                <span className="mobile-menu-button-line" />
                <span className="mobile-menu-button-line" />
                <span className="mobile-menu-button-line" />
              </button>
              <span className="mobile-page-title" data-testid="mobile-page-title">{activeItem?.pageTitle ?? 'Recipe App'}</span>
              <span className="mobile-header-actions-placeholder" aria-hidden="true" />
            </header>
            {isFlyoutVisible && (
              <>
                <button
                  className="mobile-flyout-overlay"
                  type="button"
                  aria-label="Close menu"
                  data-testid="mobile-flyout-overlay"
                  onClick={() => closeFlyout()}
                />
                <aside
                  ref={flyoutRef}
                  id="mobile-flyout"
                  className="mobile-flyout"
                  aria-label="Main menu"
                  data-testid="mobile-flyout"
                  onKeyDown={handleFlyoutKeyDown}
                >
                  <div className="flyout-brand">
                    <span className="brand-mark" aria-hidden="true">R</span>
                    <span>Recipe App</span>
                  </div>
                  <nav aria-label="Main">
                    <ul className="flyout-navigation-list">
                      {navigationItems.map((item) => {
                        const isActive = activeItem?.route === item.route;
                        return (
                          <li key={item.route}>
                            <Link
                              className={`flyout-navigation-link${isActive ? ' flyout-navigation-link-active' : ''}`}
                              to={item.route}
                              aria-current={isActive ? 'page' : undefined}
                              onClick={(event) => handleNavigationClick(event, item)}
                            >
                              {item.label}
                            </Link>
                          </li>
                        );
                      })}
                    </ul>
                  </nav>
                  <button className="btn btn-danger flyout-logout" type="button" onClick={handleLogout} disabled={isLoggingOut}>
                    {isLoggingOut ? 'Logging out…' : 'Log out'}
                  </button>
                  {logoutError && <p className="flyout-logout-error" role="alert">{logoutError}</p>}
                </aside>
              </>
            )}
          </>
        )
        : (
          <header className="login-header app-header" data-testid="app-header">
            <Link className="brand" to="/home" aria-label="Recipe App home">
              <span className="brand-mark" aria-hidden="true">R</span>
              <span>Recipe App</span>
            </Link>
            <nav className="app-navigation" aria-label="Main">
              <ul className="app-navigation-list">
                {navigationItems.map((item) => {
                  const isActive = activeItem?.route === item.route;
                  return (
                    <li key={item.route}>
                      <Link
                        className={`app-navigation-link${isActive ? ' app-navigation-link-active' : ''}`}
                        to={item.route}
                        aria-current={isActive ? 'page' : undefined}
                        onClick={(event) => handleNavigationClick(event, item)}
                      >
                        {item.label}
                      </Link>
                    </li>
                  );
                })}
              </ul>
            </nav>
            <button className="btn btn-danger app-logout" type="button" onClick={handleLogout} disabled={isLoggingOut}>
              {isLoggingOut ? 'Logging out…' : 'Log out'}
            </button>
            {logoutError && <p className="app-logout-error" role="alert">{logoutError}</p>}
          </header>
        )}
      <Outlet />
    </div>
  );
}

export default AppShell;
