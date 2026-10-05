import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { json, request, setUnauthorizedHandler } from '../api/client';
import type { CurrentUser } from '../types';

interface AuthContextValue {
  user: CurrentUser | null;
  ready: boolean;
  login: (userName: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  /** True when the caller holds the named permission. */
  can: (permission: string) => boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [ready, setReady] = useState(false);

  const clear = useCallback(() => {
    setUser(null);
  }, []);

  useEffect(() => {
    void (async () => {
      try {
        setUser(await json.get<CurrentUser>('/api/v1/auth/me'));
      } catch {
        setUser(null);
      } finally {
        setReady(true);
      }
    })();
  }, [clear]);

  useEffect(() => {
    setUnauthorizedHandler(clear);
    return () => setUnauthorizedHandler(null);
  }, [clear]);

  const login = useCallback(async (userName: string, password: string) => {
    await request<{ requiresTwoFactor: boolean }>('/api/v1/auth/login', {
      method: 'POST',
      body: JSON.stringify({ userName, password }),
    });
    setUser(await json.get<CurrentUser>('/api/v1/auth/me'));
  }, []);

  const logout = useCallback(async () => {
    try {
      await request<void>('/api/v1/auth/logout', { method: 'POST' });
    } finally {
      setUser(null);
    }
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      ready,
      login,
      logout,
      can: (permission: string) => user?.permissions.includes(permission) ?? false,
    }),
    [user, ready, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (context === null) {
    throw new Error('useAuth must be used within an AuthProvider.');
  }
  return context;
}