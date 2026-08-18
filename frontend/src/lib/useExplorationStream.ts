import { useEffect, useRef, useState } from 'react';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { useAuthStore } from '../store/authStore';

export interface ExplorationLiveStatus {
  sessionId: string;
  status: string;
  currentUrl: string | null;
  pagesDiscovered: number;
  elementsDiscovered: number;
}

export interface ExplorationLiveStep {
  sessionId: string;
  scenarioId: string;
  scenarioTitle: string | null;
  stepOrder: number;
  action: string;
  status: string;
  detail: string | null;
  url: string | null;
}

export interface ExplorationLogLine {
  sessionId: string;
  level: string;
  message: string;
  timestampUtc: string;
}

interface FramePayload {
  sessionId: string;
  data: string;
}

/**
 * Subscribes to the tenant-scoped exploration live stream for a single session.
 * The SignalR hub validates that the session belongs to the caller's organization,
 * so frames from other organizations are never delivered here.
 */
export function useExplorationStream(sessionId: string | null, enabled: boolean) {
  const [frame, setFrame] = useState<string | null>(null);
  const [status, setStatus] = useState<ExplorationLiveStatus | null>(null);
  const [steps, setSteps] = useState<ExplorationLiveStep[]>([]);
  const [logs, setLogs] = useState<ExplorationLogLine[]>([]);
  const [connected, setConnected] = useState(false);
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    if (!enabled || !sessionId) {
      setFrame(null);
      setConnected(false);
      return;
    }

    let disposed = false;
    setSteps([]);
    setLogs([]);

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/exploration', {
        accessTokenFactory: () => useAuthStore.getState().accessToken ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    connection.on('frame', (payload: FramePayload) => {
      if (payload.sessionId === sessionId) {
        setFrame(payload.data);
      }
    });

    connection.on('status', (payload: ExplorationLiveStatus) => {
      if (payload.sessionId === sessionId) {
        setStatus(payload);
      }
    });

    connection.on('step', (payload: ExplorationLiveStep) => {
      if (payload.sessionId === sessionId) {
        setSteps((prev) => [...prev, payload]);
      }
    });

    connection.on('log', (payload: ExplorationLogLine) => {
      if (payload.sessionId === sessionId) {
        setLogs((prev) => (prev.length > 500 ? [...prev.slice(-500), payload] : [...prev, payload]));
      }
    });

    const joinSession = async () => {
      try {
        await connection.start();
        await connection.invoke('JoinSession', sessionId);
        if (!disposed) setConnected(true);
      } catch {
        if (!disposed) setConnected(false);
      }
    };

    void joinSession();

    return () => {
      disposed = true;
      setConnected(false);
      const active = connectionRef.current;
      connectionRef.current = null;
      if (active) {
        if (active.state === HubConnectionState.Connected) {
          active.invoke('LeaveSession', sessionId).catch(() => undefined);
        }
        active.stop().catch(() => undefined);
      }
    };
  }, [sessionId, enabled]);

  return { frame, status, steps, logs, connected };
}
