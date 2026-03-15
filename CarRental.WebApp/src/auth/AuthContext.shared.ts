import { createContext } from 'react';
import type { Employee } from '../api/types';

export interface AuthContextValue {
  user: Employee | null;
  token: string | null;
  isLoading: boolean;
  isAuthenticated: boolean;
  login: (login: string, password: string) => Promise<void>;
  register: (payload: { fullName: string; login: string; phone: string; password: string }) => Promise<void>;
  logout: () => void;
  refreshUser: () => Promise<void>;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);
