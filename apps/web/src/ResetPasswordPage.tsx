import { useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { z } from 'zod';
import { AuthApiError, resetPassword } from './services/authApi';
import './styles/login.css';

const newPasswordSchema = z.string().min(1, 'New password is required');

function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') ?? '';
  const [newPassword, setNewPassword] = useState('');
  const [passwordError, setPasswordError] = useState('');
  const [formError, setFormError] = useState('');
  const [isComplete, setIsComplete] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError('');

    const result = newPasswordSchema.safeParse(newPassword);
    if (!result.success) {
      setPasswordError(result.error.issues[0].message);
      return;
    }

    setPasswordError('');
    setIsSubmitting(true);

    try {
      await resetPassword(token, result.data);
      setNewPassword('');
      setIsComplete(true);
    } catch (error) {
      if (error instanceof AuthApiError && error.fieldErrors?.newPassword) {
        setPasswordError(error.fieldErrors.newPassword);
      } else {
        setFormError(error instanceof AuthApiError ? error.message : 'Unable to reset your password. Please try again.');
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="login-shell">
      <header className="login-header">
        <Link className="brand" to="/" aria-label="Recipe App home">
          <span className="brand-mark" aria-hidden="true">R</span>
          <span>Recipe App</span>
        </Link>
        <span className="header-note">A LITTLE MORE ROOM AT THE TABLE</span>
      </header>

      <main className="login-main">
        <section className="login-panel" aria-labelledby="reset-heading">
          <div className="panel-heading">
            <p className="eyebrow">ACCOUNT ACCESS</p>
            <h2 id="reset-heading">Reset your password</h2>
          </div>

          {isComplete ? (
            <p className="form-notice" role="status">
              Your password has been reset. <Link to="/">Log in</Link> with your new password.
            </p>
          ) : !token ? (
            <p className="form-error" role="alert">
              This reset link is invalid. <Link to="/forgot-password">Request a new one</Link>.
            </p>
          ) : (
            <form className="login-form" noValidate onSubmit={handleSubmit} aria-busy={isSubmitting}>
              <div className="form-field">
                <label htmlFor="reset-password">New password</label>
                <input
                  autoComplete="new-password"
                  id="reset-password"
                  name="newPassword"
                  type="password"
                  value={newPassword}
                  aria-invalid={Boolean(passwordError)}
                  aria-describedby={passwordError ? 'reset-password-error' : undefined}
                  onChange={(event) => {
                    setNewPassword(event.target.value);
                    setPasswordError('');
                    setFormError('');
                  }}
                />
                {passwordError && <p className="field-error" id="reset-password-error">{passwordError}</p>}
              </div>

              <button className="btn btn-primary login-submit" type="submit" disabled={isSubmitting}>
                {isSubmitting ? 'Resetting…' : 'Reset password'}
              </button>
              {formError && (
                <p className="form-error" role="alert">
                  {formError} <Link to="/forgot-password">Request a new link</Link>
                </p>
              )}
            </form>
          )}
        </section>
      </main>

      <footer className="login-footer">
        <span>RECIPE APP</span>
        <span>Made for the everyday table</span>
      </footer>
    </div>
  );
}

export default ResetPasswordPage;
