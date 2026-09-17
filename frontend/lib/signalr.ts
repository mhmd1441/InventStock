"use client";

import { useEffect, useRef, useState } from "react";
import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { API_BASE_URL, getAccessToken } from "./api";
import { invalidateStockViews } from "./queries";
import type { StockChangedNotification } from "./types";

export type ConnectionStatus = "connecting" | "live" | "offline";

interface UseStockFeedOptions {
  enabled: boolean;
  onStockChanged?: (notification: StockChangedNotification) => void;
}

/**
 * Subscribes to server-pushed stock changes and refreshes the cached views they invalidate.
 */
export function useStockFeed({ enabled, onStockChanged }: UseStockFeedOptions) {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<ConnectionStatus>("offline");
  const [lastChange, setLastChange] = useState<StockChangedNotification | null>(
    null,
  );

  const handlerRef = useRef(onStockChanged);

  useEffect(() => {
    handlerRef.current = onStockChanged;
  }, [onStockChanged]);

  useEffect(() => {
    if (!enabled) return;

    let connection: HubConnection | null = null;
    let startup: Promise<void> | null = null;
    let disposed = false;

    const start = async () => {
      setStatus("connecting");

      connection = new HubConnectionBuilder()
        .withUrl(`${API_BASE_URL}/hubs/stock`, {
          // A browser cannot attach an Authorization header to the WebSocket handshake, so the
          // token travels as a query-string value the API accepts only on this path.
          accessTokenFactory: () => getAccessToken() ?? "",
        })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      connection.on("StockChanged", (notification: StockChangedNotification) => {
        setLastChange(notification);
        handlerRef.current?.(notification);
        void invalidateStockViews(queryClient);
      });

      connection.on("GlobalRefreshRequested", () => {
        void invalidateStockViews(queryClient);
      });

      connection.onreconnecting(() => setStatus("connecting"));
      connection.onreconnected(() => setStatus("live"));
      connection.onclose(() => setStatus("offline"));

      // Held so teardown can wait for negotiation to settle. Calling stop() while the handshake is
      // still in flight aborts it and logs an error, which is what React's double-invoked effects in
      // development would otherwise trigger on every mount.
      startup = connection
        .start()
        .then(() => {
          if (!disposed) setStatus("live");
        })
        .catch(() => {
          if (!disposed) setStatus("offline");
        });

      await startup;
    };

    void start();

    return () => {
      disposed = true;

      const stop = () => {
        if (connection && connection.state !== HubConnectionState.Disconnected) {
          void connection.stop();
        }
      };

      if (startup) {
        void startup.then(stop);
      } else {
        stop();
      }
    };
  }, [enabled, queryClient]);

  return { status: enabled ? status : "offline", lastChange };
}
