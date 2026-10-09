import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { z } from 'zod';
import { AuthApiError, loginAccount, type UserDto } from './services/authApi';
import './styles/login.css';

const loginSchema = z.object({
  email: z.string().trim().min(1, 'Email is required').email('Enter a valid email address'),
  password: z.string().min(1, 'Password is required'),
});

type LoginField = keyof z.infer<typeof loginSchema>;
type LoginErrors = Partial<Record<LoginField, string>>;

interface LoginPageProps {
  onLoginSuccess: (user: UserDto) => void;
  sessionNotice: string;
}

function LoginPage({ onLoginSuccess, sessionNotice }: LoginPageProps) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<LoginErrors>({});
  const [formError, setFormError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const result = loginSchema.safeParse({ email, password });
    if (!result.success) {
      const nextErrors: LoginErrors = {};
      for (const issue of result.error.issues) {
        const field = issue.path[0];
        if ((field === 'email' || field === 'password') && !nextErrors[field]) {
          nextErrors[field] = issue.message;
        }
      }
      setErrors(nextErrors);
      setFormError('');
      return;
    }

    setErrors({});
    setFormError('');
    setIsSubmitting(true);

    try {
      const user = await loginAccount(result.data.email, result.data.password);
      setPassword('');
      onLoginSuccess(user);
    } catch (error) {
      if (error instanceof AuthApiError) {
        if (error.fieldErrors) {
          setErrors(error.fieldErrors);
        } else {
          setFormError(error.message);
        }
      } else {
        setFormError('Unable to log in. Please try again.');
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
        <div className="login-layout">
          <section className="login-intro" aria-labelledby="welcome-heading">
            <p className="eyebrow">YOUR KITCHEN NOTEBOOK</p>
            <h1 id="welcome-heading">Welcome<br />back<span>.</span></h1>
            <p className="intro-copy">Good to see you again.</p>
            <div className="notebook-mark" aria-hidden="true">
              <span className="notebook-number">01</span>
              <span className="notebook-rule" />
              <span className="notebook-caption">THE EVERYDAY EDITION</span>
            </div>
          </section>

          <section className="login-panel" aria-labelledby="login-heading">
            <div className="panel-heading">
              <p className="eyebrow">ACCOUNT ACCESS</p>
              <h2 id="login-heading">Log in</h2>
              <p className="panel-copy">Enter your email and password to continue.</p>
            </div>

            <form className="login-form" noValidate onSubmit={handleSubmit} aria-busy={isSubmitting}>
              <div className="form-field">
                <label htmlFor="email">Email</label>
                <input
                  autoComplete="email"
                  id="email"
                  name="email"
                  type="email"
                  value={email}
                  aria-invalid={Boolean(errors.email)}
                  aria-describedby={errors.email ? 'email-error' : undefined}
                  onChange={(event) => {
                    setEmail(event.target.value);
                    setErrors((current) => ({ ...current, email: undefined }));
                    setFormError('');
                  }}
                />
                {errors.email && <p className="field-error" id="email-error">{errors.email}</p>}
              </div>

              <div className="form-field">
                <label htmlFor="password">Password</label>
                <input
                  autoComplete="current-password"
                  id="password"
                  name="password"
                  type="password"
                  value={password}
                  aria-invalid={Boolean(errors.password)}
                  aria-describedby={errors.password ? 'password-error' : undefined}
                  onChange={(event) => {
                    setPassword(event.target.value);
                    setErrors((current) => ({ ...current, password: undefined }));
                    setFormError('');
                  }}
                />
                {errors.password && <p className="field-error" id="password-error">{errors.password}</p>}
              </div>

              <button className="btn btn-primary login-submit" type="submit" disabled={isSubmitting}>
                {isSubmitting
                  ? <span className="login-submit-content"><span className="login-spinner" aria-hidden="true" />Logging in…</span>
                  : 'Log in'}
              </button>
              {formError && <p className="form-error" role="alert">{formError}</p>}
              <p className="form-notice" role="status">{sessionNotice}</p>
            </form>

            <nav className="account-links" aria-label="Account help">
              <Link to="/register">Create account</Link>
              <Link to="/forgot-password">Forgot password</Link>
            </nav>
          </section>
        </div>
      </main>

      <footer className="login-footer">
        <span>RECIPE APP</span>
        <span>Made for the everyday table</span>
      </footer>
    </div>
  );
}

export default LoginPage;
