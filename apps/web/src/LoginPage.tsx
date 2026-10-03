import { useState, type FormEvent } from 'react';
import { z } from 'zod';
import './styles/login.css';

const loginSchema = z.object({
  email: z.string().trim().min(1, 'Email is required').email('Enter a valid email address'),
  password: z.string().min(1, 'Password is required'),
});

type LoginField = keyof z.infer<typeof loginSchema>;
type LoginErrors = Partial<Record<LoginField, string>>;

function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<LoginErrors>({});
  const [notice, setNotice] = useState('');

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
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
      setNotice('');
      return;
    }

    setErrors({});
    setNotice('Sign-in is not connected yet.');
  }

  return (
    <div className="login-shell">
      <header className="login-header">
        <a className="brand" href="/" aria-label="Recipe App home">
          <span className="brand-mark" aria-hidden="true">R</span>
          <span>Recipe App</span>
        </a>
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

            <form className="login-form" noValidate onSubmit={handleSubmit}>
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
                  onChange={(event) => setEmail(event.target.value)}
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
                  onChange={(event) => setPassword(event.target.value)}
                />
                {errors.password && <p className="field-error" id="password-error">{errors.password}</p>}
              </div>

              <button className="btn btn-primary login-submit" type="submit">Log in</button>
              <p className="form-notice" role="status">{notice}</p>
            </form>

            <nav className="account-links" aria-label="Account help">
              <a href="/register">Create account</a>
              <a href="/forgot-password">Forgot password</a>
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
