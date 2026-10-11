"use client";

import { useEffect, useState } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ApiError } from "@/lib/api";
import { AuthProvider, useAuth } from "@/lib/auth";

export function Providers({ children }: { children: React.ReactNode }) {
  return <AuthProvider><SessionQueries>{children}</SessionQueries></AuthProvider>;
}

function SessionQueries({ children }: { children: React.ReactNode }) {
  const { user, status, privateMode } = useAuth();
  return <QueryScope key={`${status}:${user?.id ?? ""}:${user?.roles.join(",") ?? ""}:${privateMode}`}>{children}</QueryScope>;
}

function QueryScope({ children }: { children: React.ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 30_000,
            refetchOnWindowFocus: true,
            retry: (failureCount, error) => {
              // Auth and validation failures will never succeed on a retry.
              if (error instanceof DOMException && error.name === "AbortError") return false;
              if (error instanceof ApiError && error.status < 500) return false;
              return failureCount < 2;
            },
          },
        },
      }),
  );

  useEffect(() => () => {
    void queryClient.cancelQueries();
    queryClient.clear();
  }, [queryClient]);

  return (
    <QueryClientProvider client={queryClient}>
      {children}
    </QueryClientProvider>
  );
}
