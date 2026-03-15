import { Navigate } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';
import { getDefaultPathByRole } from '../utils/access';

export function HomeRedirectPage() {
  const { user } = useAuth();
  return <Navigate to={getDefaultPathByRole(user?.role)} replace />;
}

