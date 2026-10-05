import { useState, type FormEvent } from 'react';
import { describeError } from '../api/client';
import { useAuth } from '../auth/AuthContext';

export function LoginPage() {
  const { login } = useAuth();
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError('');
    setBusy(true);
    try {
      await login(userName, password);
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="login-shell">
      <div className="card login-card">
        <h1>ClubStack</h1>
        <p>Sign in to manage the club.</p>
        {error ? <div className="alert alert-error">{error}</div> : null}
        <form className="login-form" onSubmit={(event) => void submit(event)}>
          <input
            aria-label="User name"
            placeholder="User name"
            autoComplete="username"
            value={userName}
            onChange={(event) => setUserName(event.target.value)}
          />
          <input
            aria-label="Password"
            type="password"
            placeholder="Password"
            autoComplete="current-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
          <button type="submit" className="btn btn-primary" disabled={busy}>
            {busy ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
      </div>
    </div>
  );
}