"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";
import { useRouter } from "next/navigation";
import * as apiClient from "./api";
import type { Role, User } from "./types";

interface AuthState {
  user: User | null;
  status: "loading" | "authenticated" | "anonymous";
  signIn: (email: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;
  hasRole: (role: Role) => boolean;
  isAdmin: boolean;
}

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const [user, setUser] = useState<User | null>(null);
  const [status, setStatus] = useState<AuthState["status"]>("loading");

  useEffect(() => {
    let cancelled = false;

    // The page load has no access token; the HttpOnly refresh cookie is what revives the session.
    apiClient
      .restoreSession()
      .then((restored) => {
        if (cancelled) return;
        setUser(restored);
        setStatus(restored ? "authenticated" : "anonymous");
      })
      .catch(() => {
        if (cancelled) return;
        setUser(null);
        setStatus("anonymous");
      });

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    apiClient.setSessionLostHandler(() => {
      setUser(null);
      setStatus("anonymous");
    });

    return () => apiClient.setSessionLostHandler(null);
  }, []);

  const signIn = useCallback(async (email: string, password: string) => {
    const auth = await apiClient.login(email, password);
    setUser(auth.user);
    setStatus("authenticated");
  }, []);

  const signOut = useCallback(async () => {
    setUser(null);
    setStatus("loading");
    try {
      await apiClient.logout();
    } finally {
      // Clear local identity even if cookie revocation could not reach the server.
      setStatus("anonymous");
      router.replace("/login");
    }
  }, [router]);

  const value = useMemo<AuthState>(
    () => ({
      user,
      status,
      signIn,
      signOut,
      hasRole: (role) => user?.roles.includes(role) ?? false,
      isAdmin: user?.roles.includes("Admin") ?? false,
    }),
    [user, status, signIn, signOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used inside an AuthProvider.");
  }

  return context;
}
