import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from './useAuth';
import { LoadingView } from '../components/LoadingView';
import type { UserRole } from '../api/types';
import { getDefaultPathByRole } from '../utils/access';

export function RequireAuth({ children }: { children: ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return <LoadingView text="Перевірка авторизації..." />;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  return <>{children}</>;
}

export function RequireRoles({ roles, children }: { roles: UserRole[]; children: ReactNode }) {
  const { user } = useAuth();

  if (!user) {
    return <Navigate to="/login" replace />;
  }

  if (!roles.includes(user.role)) {
    const fallback = getDefaultPathByRole(user.role);
    return <Navigate to={fallback === '/prokat' ? '/forbidden' : fallback} replace />;
  }

  return <>{children}</>;
}

