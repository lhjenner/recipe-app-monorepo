import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { z } from 'zod';
import { AuthApiError, requestPasswordReset } from './services/authApi';
import './styles/login.css';

const emailSchema = z.string().trim().min(1, 'Email is required').email('Enter a valid email address');

function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [emailError, setEmailError] = useState('');
  const [formError, setFormError] = useState('');
  const [notice, setNotice] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setNotice('');
    setFormError('');

    const result = emailSchema.safeParse(email);
    if (!result.success) {
      setEmailError(result.error.issues[0].message);
      return;
    }

    setEmailError('');
    setIsSubmitting(true);

    try {
      await requestPasswordReset(result.data);
      setNotice("If that email is registered, we've sent a link to reset your password.");
    } catch (error) {
      setFormError(error instanceof AuthApiError ? error.message : 'Unable to send the reset link. Please try again.');
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
        <section className="login-panel" aria-labelledby="forgot-heading">
          <div className="panel-heading">
            <p className="eyebrow">ACCOUNT ACCESS</p>
            <h2 id="forgot-heading">Forgot your password?</h2>
            <p className="panel-copy">Enter your email and we'll send you a link to reset it.</p>
          </div>

          <form className="login-form" noValidate onSubmit={handleSubmit} aria-busy={isSubmitting}>
            <div className="form-field">
              <label htmlFor="forgot-email">Email</label>
              <input
                autoComplete="email"
                id="forgot-email"
                name="email"
                type="email"
                value={email}
                aria-invalid={Boolean(emailError)}
                aria-describedby={emailError ? 'forgot-email-error' : undefined}
                onChange={(event) => {
                  setEmail(event.target.value);
                  setEmailError('');
                  setFormError('');
                }}
              />
              {emailError && <p className="field-error" id="forgot-email-error">{emailError}</p>}
            </div>

            <button className="btn btn-primary login-submit" type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Sending…' : 'Send reset link'}
            </button>
            {formError && <p className="form-error" role="alert">{formError}</p>}
            <p className="form-notice" role="status">{notice}</p>
          </form>

          <nav className="account-links registration-links" aria-label="Account navigation">
            <Link to="/">Back to log in</Link>
          </nav>
        </section>
      </main>

      <footer className="login-footer">
        <span>RECIPE APP</span>
        <span>Made for the everyday table</span>
      </footer>
    </div>
  );
}

export default ForgotPasswordPage;
