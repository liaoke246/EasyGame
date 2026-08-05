import { io, type Socket } from "socket.io-client";
import type {
  AttackEvent,
  ClientToServerEvents,
  InputPayload,
  NetworkPong,
  NetworkStats,
  NotificationEvent,
  ServerToClientEvents,
  WelcomePayload,
  WorldSnapshot,
} from "./types";

type Unsubscribe = () => void;

export class NetworkClient {
  private socket?: Socket<ServerToClientEvents, ClientToServerEvents>;
  private latestSnapshot?: WorldSnapshot;
  private readonly snapshotListeners = new Set<(value: WorldSnapshot) => void>();
  private readonly attackListeners = new Set<(value: AttackEvent) => void>();
  private readonly notificationListeners = new Set<
    (value: NotificationEvent) => void
  >();
  private readonly networkStatsListeners = new Set<
    (value: NetworkStats) => void
  >();
  private readonly pendingProbes = new Map<number, number>();
  private readonly probeResults: boolean[] = [];
  private probeInterval?: number;
  private nextProbeSequence = 1;
  private latencyMs: number | null = null;
  private connected = false;

  connect(): Promise<WelcomePayload> {
    if (this.socket?.connected) {
      return Promise.reject(new Error("Already connected"));
    }

    const guestToken = localStorage.getItem("easygame.guestToken");
    const socket = io({
      auth: { guestToken },
      transports: ["websocket", "polling"],
    });
    this.socket = socket;

    socket.on("connect", () => {
      this.connected = true;
      this.pendingProbes.clear();
      this.publishNetworkStats();
      this.sendNetworkProbe();
    });
    socket.on("disconnect", () => {
      this.connected = false;
      this.pendingProbes.clear();
      this.publishNetworkStats();
    });
    socket.on("network:pong", (pong) => this.receiveNetworkPong(pong));

    if (this.probeInterval === undefined) {
      this.probeInterval = window.setInterval(
        () => this.sendNetworkProbe(),
        2_000,
      );
    }

    socket.on("snapshot", (snapshot) => {
      this.latestSnapshot = snapshot;
      for (const listener of this.snapshotListeners) {
        listener(snapshot);
      }
    });
    socket.on("attack", (event) => {
      for (const listener of this.attackListeners) {
        listener(event);
      }
    });
    socket.on("notification", (event) => {
      for (const listener of this.notificationListeners) {
        listener(event);
      }
    });

    return new Promise((resolve, reject) => {
      const onError = (error: Error): void => {
        socket.off("welcome", onWelcome);
        reject(error);
      };
      const onWelcome = (welcome: WelcomePayload): void => {
        socket.off("connect_error", onError);
        localStorage.setItem("easygame.guestToken", welcome.guestToken);
        resolve(welcome);
      };

      socket.once("connect_error", onError);
      socket.once("welcome", onWelcome);
    });
  }

  sendInput(input: InputPayload): void {
    this.socket?.emit("input", input);
  }

  onSnapshot(listener: (snapshot: WorldSnapshot) => void): Unsubscribe {
    this.snapshotListeners.add(listener);
    if (this.latestSnapshot) {
      listener(this.latestSnapshot);
    }
    return () => this.snapshotListeners.delete(listener);
  }

  onAttack(listener: (event: AttackEvent) => void): Unsubscribe {
    this.attackListeners.add(listener);
    return () => this.attackListeners.delete(listener);
  }

  onNotification(
    listener: (event: NotificationEvent) => void,
  ): Unsubscribe {
    this.notificationListeners.add(listener);
    return () => this.notificationListeners.delete(listener);
  }

  onNetworkStats(listener: (value: NetworkStats) => void): Unsubscribe {
    this.networkStatsListeners.add(listener);
    listener(this.currentNetworkStats());
    return () => this.networkStatsListeners.delete(listener);
  }

  disconnect(): void {
    if (this.probeInterval !== undefined) {
      window.clearInterval(this.probeInterval);
      this.probeInterval = undefined;
    }
    this.pendingProbes.clear();
    this.connected = false;
    this.socket?.disconnect();
  }

  private sendNetworkProbe(): void {
    const socket = this.socket;
    if (!socket?.connected) {
      return;
    }

    const now = performance.now();
    for (const [sequence, sentAt] of this.pendingProbes) {
      if (now - sentAt < 4_000) {
        continue;
      }
      this.pendingProbes.delete(sequence);
      this.recordProbeResult(false);
    }

    const sequence = this.nextProbeSequence;
    this.nextProbeSequence += 1;
    this.pendingProbes.set(sequence, now);
    socket.emit("network:ping", {
      sequence,
      clientSentAt: Date.now(),
    });
  }

  private receiveNetworkPong(pong: NetworkPong): void {
    const sentAt = this.pendingProbes.get(pong.sequence);
    if (sentAt === undefined) {
      return;
    }

    this.pendingProbes.delete(pong.sequence);
    const roundTripMs = Math.max(0, performance.now() - sentAt);
    this.latencyMs = Math.round(
      this.latencyMs === null
        ? roundTripMs
        : this.latencyMs * 0.7 + roundTripMs * 0.3,
    );
    this.recordProbeResult(true);
  }

  private recordProbeResult(success: boolean): void {
    this.probeResults.push(success);
    if (this.probeResults.length > 20) {
      this.probeResults.shift();
    }
    this.publishNetworkStats();
  }

  private currentNetworkStats(): NetworkStats {
    const lost = this.probeResults.filter((success) => !success).length;
    return {
      connected: this.connected,
      latencyMs: this.latencyMs,
      packetLossPercent:
        this.probeResults.length === 0
          ? 0
          : Math.round((lost / this.probeResults.length) * 100),
      samples: this.probeResults.length,
    };
  }

  private publishNetworkStats(): void {
    const stats = this.currentNetworkStats();
    for (const listener of this.networkStatsListeners) {
      listener(stats);
    }
  }
}
