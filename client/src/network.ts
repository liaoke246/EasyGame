import { io, type Socket } from "socket.io-client";
import type {
  AttackEvent,
  ClientToServerEvents,
  InputPayload,
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

  disconnect(): void {
    this.socket?.disconnect();
  }
}
