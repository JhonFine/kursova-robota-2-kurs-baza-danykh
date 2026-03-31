import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Api, getStoredToken, setAuthToken } from '../api/client';
import type { AuthenticatedUser } from '../api/types';
import { AuthContext, type AuthContextValue } from './AuthContext.shared';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  const [token, setToken] = useState<string | null>(() => getStoredToken());
  const [isLoading, setIsLoading] = useState(true);

  const applyAuthState = useCallback((accessToken: string | null, authenticatedUser: AuthenticatedUser | null) => {
    setToken(accessToken);
    setUser(authenticatedUser);
    setAuthToken(accessToken);
  }, []);

  const refreshUser = useCallback(async () => {
    if (!token) {
      applyAuthState(null, null);
      return;
    }

    try {
      const me = await Api.me();
      setUser(me);
    } catch {
      applyAuthState(null, null);
    }
  }, [applyAuthState, token]);

  useEffect(() => {
    let isMounted = true;

    const initialize = async (): Promise<void> => {
      try {
        if (token) {
          const me = await Api.me();
          if (isMounted) {
            setUser(me);
          }
        }
      } catch {
        if (isMounted) {
          applyAuthState(null, null);
        }
      } finally {
        if (isMounted) {
          setIsLoading(false);
        }
      }
    };

    void initialize();

    return () => {
      isMounted = false;
    };
  }, [applyAuthState, token]);

  const login = useCallback(async (loginValue: string, password: string) => {
    const response = await Api.login(loginValue, password);
    applyAuthState(response.accessToken, response.user);
  }, [applyAuthState]);

  const register = useCallback(async (payload: { fullName: string; login: string; phone: string; password: string }) => {
    const response = await Api.register(payload);
    applyAuthState(response.accessToken, response.user);
  }, [applyAuthState]);

  const logout = useCallback(() => {
    applyAuthState(null, null);
  }, [applyAuthState]);

  const value = useMemo<AuthContextValue>(() => ({
    user,
    token,
    isLoading,
    isAuthenticated: Boolean(user && token),
    login,
    register,
    logout,
    refreshUser,
  }), [user, token, isLoading, login, logout, refreshUser, register]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
