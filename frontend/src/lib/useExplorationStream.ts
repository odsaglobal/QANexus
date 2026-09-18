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
  /** Stable correlation id; a later line with the same id rewrites this one rather than appending. */
  id?: string | null;
  level: string;
  message: string;
  timestampUtc: string;
}

/** Which test the run is on. A suite runs its scenarios sequentially inside one session. */
export interface ExplorationLiveScenario {
  sessionId: string;
  scenarioId: string;
  scenarioTitle: string;
  /** 1-based position within the run. */
  index: number;
  total: number;
  stepCount: number;
  /** 'Running' while executing, then the worst outcome among its steps. */
  status: string;
}

interface FramePayload {
  sessionId: string;
  data: string;
}

export interface ExplorationLiveTab {
  index: number;
  title: string;
  url: string;
  isActive: boolean;
}

interface TabsPayload {
  sessionId: string;
  tabs: ExplorationLiveTab[];
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
  const [scenarios, setScenarios] = useState<ExplorationLiveScenario[]>([]);
  const [logs, setLogs] = useState<ExplorationLogLine[]>([]);
  const [tabs, setTabs] = useState<ExplorationLiveTab[]>([]);
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
    setScenarios([]);
    setLogs([]);
    setTabs([]);

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

    // Whole-list snapshot, not an increment: the server only sends it when the tabs actually change.
    connection.on('tabs', (payload: TabsPayload) => {
      if (payload.sessionId === sessionId) {
        setTabs(payload.tabs ?? []);
      }
    });

    connection.on('step', (payload: ExplorationLiveStep) => {
      if (payload.sessionId === sessionId) {
        // A step is streamed twice: once as "Running" the instant it starts, then again with its
        // verdict. Replace by (scenario, order) so the panel shows the action live and settles in
        // place instead of listing the same step twice.
        setSteps((prev) => {
          const at = prev.findIndex((s) => s.scenarioId === payload.scenarioId && s.stepOrder === payload.stepOrder);
          if (at === -1) return [...prev, payload];
          const next = prev.slice();
          next[at] = payload;
          return next;
        });
      }
    });

    connection.on('log', (payload: ExplorationLogLine) => {
      if (payload.sessionId === sessionId) {
        setLogs((prev) => {
          // Identified lines are announcements that get rewritten with their outcome. Keep the
          // ORIGINAL timestamp so the entry stays anchored to when the action actually happened.
          const at = payload.id ? prev.findIndex((l) => l.id === payload.id) : -1;
          if (at !== -1) {
            const next = prev.slice();
            next[at] = { ...payload, timestampUtc: prev[at].timestampUtc };
            return next;
          }
          return prev.length > 500 ? [...prev.slice(-500), payload] : [...prev, payload];
        });
      }
    });

    // Sent twice per test — 'Running' on entry, then the verdict. Replace by scenarioId so the list
    // stays one row per test and settles in place, preserving the order the run visits them in.
    connection.on('scenario', (payload: ExplorationLiveScenario) => {
      if (payload.sessionId === sessionId) {
        setScenarios((prev) => {
          const at = prev.findIndex((s) => s.scenarioId === payload.scenarioId);
          if (at === -1) return [...prev, payload];
          const next = prev.slice();
          next[at] = payload;
          return next;
        });
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

  return { frame, status, steps, scenarios, logs, tabs, connected };
}
