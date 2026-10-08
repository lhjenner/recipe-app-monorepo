import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { z } from 'zod';
import { registerAccount, RegistrationApiError, type RegistrationFieldErrors } from './services/authApi';
import './styles/login.css';

const registrationSchema = z.object({
  email: z.string().trim().min(1, 'Email is required').email('Enter a valid email address'),
  password: z.string().min(1, 'Password is required'),
});

function RegistrationPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<RegistrationFieldErrors>({});
  const [notice, setNotice] = useState('');
  const [formError, setFormError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const result = registrationSchema.safeParse({ email, password });
    if (!result.success) {
      const nextErrors: RegistrationFieldErrors = {};
      for (const issue of result.error.issues) {
        const field = issue.path[0];
        if ((field === 'email' || field === 'password') && !nextErrors[field]) {
          nextErrors[field] = issue.message;
        }
      }
      setErrors(nextErrors);
      setNotice('');
      setFormError('');
      return;
    }

    setErrors({});
    setNotice('');
    setFormError('');
    setIsSubmitting(true);

    try {
      await registerAccount(result.data.email, result.data.password);
      setPassword('');
      setNotice('Account created. You can now log in.');
    } catch (error) {
      if (error instanceof RegistrationApiError) {
        if (error.fieldErrors) {
          setErrors(error.fieldErrors);
        } else {
          setFormError(error.message);
        }
      } else {
        setFormError('Registration failed. Please try again.');
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
        <section className="login-panel registration-panel" aria-labelledby="registration-heading">
          <div className="panel-heading">
            <p className="eyebrow">ACCOUNT ACCESS</p>
            <h2 id="registration-heading">Create account</h2>
            <p className="panel-copy">Create an account to start building your kitchen notebook.</p>
          </div>

          <form className="login-form" noValidate onSubmit={handleSubmit} aria-busy={isSubmitting}>
            <div className="form-field">
              <label htmlFor="registration-email">Email</label>
              <input
                autoComplete="email"
                id="registration-email"
                name="email"
                type="email"
                value={email}
                aria-invalid={Boolean(errors.email)}
                aria-describedby={errors.email ? 'registration-email-error' : undefined}
                onChange={(event) => {
                  setEmail(event.target.value);
                  setErrors((current) => ({ ...current, email: undefined }));
                  setFormError('');
                }}
              />
              {errors.email && (
                <p className="field-error" id="registration-email-error">{errors.email}</p>
              )}
            </div>

            <div className="form-field">
              <label htmlFor="registration-password">Password</label>
              <input
                autoComplete="new-password"
                id="registration-password"
                name="password"
                type="password"
                value={password}
                aria-invalid={Boolean(errors.password)}
                aria-describedby={errors.password ? 'registration-password-error' : undefined}
                onChange={(event) => {
                  setPassword(event.target.value);
                  setErrors((current) => ({ ...current, password: undefined }));
                  setFormError('');
                }}
              />
              {errors.password && (
                <p className="field-error" id="registration-password-error">{errors.password}</p>
              )}
            </div>

            <button className="btn btn-primary login-submit" type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Creating account…' : 'Create account'}
            </button>
            {formError && <p className="form-error" role="alert">{formError}</p>}
            <p className="form-notice" role="status">{notice}</p>
          </form>

          <nav className="account-links registration-links" aria-label="Account navigation">
            <Link to="/">Log in</Link>
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

export default RegistrationPage;
