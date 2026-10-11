"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";
import * as apiClient from "./api";
import type { Role, User } from "./types";

interface AuthState {
  user: User | null;
  status: "loading" | "authenticated" | "anonymous";
  signIn: (email: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;
  hasRole: (role: Role) => boolean;
  isAdmin: boolean;
  privateMode: boolean;
  canViewPrivate: boolean;
  showPrivate: () => void;
  hidePrivate: () => void;
}

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [status, setStatus] = useState<AuthState["status"]>("loading");
  const [privateMode, setPrivateMode] = useState(false);

  useEffect(() => {
    let cancelled = false;

    void apiClient.retryPendingRevocation();

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
    const retry = () => { void apiClient.retryPendingRevocation(); };
    window.addEventListener("online", retry);
    return () => window.removeEventListener("online", retry);
  }, []);

  useEffect(() => {
    const signedOutElsewhere = (event: StorageEvent) => {
      if (event.key !== apiClient.EXPLICIT_SIGN_OUT_KEY || event.newValue !== "1") return;
      apiClient.resetSession();
      setUser(null);
      setStatus("anonymous");
      setPrivateMode(false);
      window.location.replace("/login");
    };
    window.addEventListener("storage", signedOutElsewhere);
    return () => window.removeEventListener("storage", signedOutElsewhere);
  }, []);

  useEffect(() => {
    if (!privateMode) return;
    let timer = window.setTimeout(() => setPrivateMode(false), 120_000);
    const reset = () => {
      window.clearTimeout(timer);
      timer = window.setTimeout(() => setPrivateMode(false), 120_000);
    };
    const hideWhenBackgrounded = () => { if (document.hidden) setPrivateMode(false); };
    window.addEventListener("pointerdown", reset);
    window.addEventListener("keydown", reset);
    document.addEventListener("visibilitychange", hideWhenBackgrounded);
    return () => {
      window.clearTimeout(timer);
      window.removeEventListener("pointerdown", reset);
      window.removeEventListener("keydown", reset);
      document.removeEventListener("visibilitychange", hideWhenBackgrounded);
    };
  }, [privateMode]);

  useEffect(() => {
    apiClient.setSessionLostHandler(() => {
      setUser(null);
      setStatus("anonymous");
      setPrivateMode(false);
    });

    return () => apiClient.setSessionLostHandler(null);
  }, []);

  const signIn = useCallback(async (email: string, password: string) => {
    setPrivateMode(false);
    const auth = await apiClient.login(email, password);
    setUser(auth.user);
    setStatus("authenticated");
  }, []);

  const signOut = useCallback(async () => {
    setUser(null);
    setStatus("loading");
    setPrivateMode(false);
    try {
      await apiClient.logout();
    } finally {
      // Clear local identity even if cookie revocation could not reach the server.
      setStatus("anonymous");
      window.location.replace("/login");
    }
  }, []);

  const value = useMemo<AuthState>(
    () => ({
      user,
      status,
      signIn,
      signOut,
      hasRole: (role) => user?.roles.includes(role) ?? false,
      isAdmin: user?.roles.includes("Admin") ?? false,
      privateMode,
      canViewPrivate: (user?.roles.includes("Admin") ?? false) && privateMode,
      showPrivate: () => { if (user?.roles.includes("Admin")) setPrivateMode(true); },
      hidePrivate: () => setPrivateMode(false),
    }),
    [user, status, signIn, signOut, privateMode],
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
