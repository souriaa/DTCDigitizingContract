"use client";

import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import { io, type Socket } from "socket.io-client";

/** Shared lifecycle; the REST APIs and their authorization remain the source of truth. */
export interface RealtimeConnection {
  readonly state: HubConnectionState;
  start(): Promise<void>;
  stop(): Promise<void>;
  on(event: string, handler: () => void): void;
  onreconnected(handler: () => void): void;
  onclose(handler: () => void): void;
}

export function createRealtimeConnection(path: string, logLevel = LogLevel.None): RealtimeConnection {
  const base = (process.env.NEXT_PUBLIC_API_URL ?? "").replace(/\/$/, "");
  const transport = process.env.NEXT_PUBLIC_REALTIME_TRANSPORT ?? "signalr";
  if (transport === "signalr") {
    return new HubConnectionBuilder().withUrl(`${base}${path}`, { withCredentials: true })
      .withAutomaticReconnect([0, 2000, 10000, 30000, 60000]).configureLogging(logLevel).build();
  }
  if (transport !== "socketio") throw new Error("Cấu hình realtime không hợp lệ.");
  return new SocketIoConnection(base, path);
}

class SocketIoConnection implements RealtimeConnection {
  state = HubConnectionState.Disconnected;
  private socket: Socket | null = null;
  private generation = 0;
  private controller: AbortController | null = null;
  private readonly events = new Map<string, (() => void)[]>();
  private readonly reconnected = new Set<() => void>();
  private readonly closed = new Set<() => void>();
  private connectedBefore = false;

  constructor(private readonly base: string, private readonly path: string) {}
  on(event: string, handler: () => void) {
    this.events.set(event, [...(this.events.get(event) ?? []), handler]);
    this.socket?.on(event, handler);
  }
  onreconnected(handler: () => void) { this.reconnected.add(handler); }
  onclose(handler: () => void) { this.closed.add(handler); }

  async start() {
    if (this.state !== HubConnectionState.Disconnected) return;
    this.state = HubConnectionState.Connecting;
    const generation = ++this.generation;
    const controller = new AbortController();
    this.controller = controller;
    const timeout = setTimeout(() => controller.abort(), 10_000);
    try {
      const ticketPath = this.path === "/api/hubs/notifications"
        ? "/api/realtime/notifications/ticket" : `${this.path}/ticket`;
      const response = await fetch(`${this.base}${ticketPath}`, {
        method: "POST", credentials: "include", cache: "no-store", signal: controller.signal,
      });
      if (!response.ok) throw new Error("Không thể xác thực kết nối realtime.");
      const { ticket } = await response.json() as { ticket: string };
      if (generation !== this.generation) return;
      const origin = process.env.NEXT_PUBLIC_REALTIME_SOCKET_URL
        || new URL(this.base || window.location.href, window.location.origin).origin;
      const socket = io(origin, { path: "/realtime/socket.io", transports: ["websocket"],
        auth: { ticket }, autoConnect: false, reconnection: false, forceNew: true, timeout: 8000 });
      this.socket = socket;
      for (const [event, handlers] of this.events) for (const handler of handlers) socket.on(event, handler);
      await new Promise<void>((resolve, reject) => {
        let connected = false;
        const abort = () => { socket.disconnect(); reject(new Error("Realtime stopped")); };
        controller.signal.addEventListener("abort", abort, { once: true });
        socket.once("connect", () => {
          controller.signal.removeEventListener("abort", abort);
          if (generation !== this.generation) { socket.disconnect(); resolve(); return; }
          connected = true;
          this.state = HubConnectionState.Connected;
          if (this.connectedBefore) this.reconnected.forEach(handler => handler());
          this.connectedBefore = true;
          resolve();
        });
        socket.once("connect_error", () => {
          controller.signal.removeEventListener("abort", abort);
          socket.disconnect();
          reject(new Error("Không thể mở kết nối realtime."));
        });
        socket.on("disconnect", () => {
          if (!connected || generation !== this.generation) return;
          this.state = HubConnectionState.Disconnected;
          this.closed.forEach(handler => handler());
        });
        socket.connect();
      });
    } catch (error) {
      if (generation === this.generation) { this.state = HubConnectionState.Disconnected; this.socket?.disconnect(); }
      throw error;
    } finally {
      clearTimeout(timeout);
      if (this.controller === controller) this.controller = null;
    }
  }

  async stop() {
    ++this.generation;
    this.controller?.abort();
    this.socket?.disconnect();
    this.socket = null;
    this.state = HubConnectionState.Disconnected;
  }
}
