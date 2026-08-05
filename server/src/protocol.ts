export type Direction = "up" | "down" | "left" | "right";

export type CharacterId = "ranger" | "farmer" | "herbalist" | "smith";

export interface Obstacle {
  id: string;
  type: "cabin" | "pond" | "tree" | "rock" | "garden";
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface PlayerIdentity {
  displayId: string;
  characterId: CharacterId;
  roleName: string;
  color: string;
}

export interface PublicPlayer extends PlayerIdentity {
  id: string;
  x: number;
  y: number;
  vx: number;
  vy: number;
  direction: Direction;
  health: number;
  maxHealth: number;
  attacking: boolean;
  kills: number;
  respawning: boolean;
}

export interface WelcomePayload {
  playerId: string;
  guestToken: string;
  identity: PlayerIdentity;
  world: {
    width: number;
    height: number;
    obstacles: Obstacle[];
  };
}

export interface WorldSnapshot {
  serverTime: number;
  players: PublicPlayer[];
}

export interface NetworkProbe {
  sequence: number;
  clientSentAt: number;
}

export interface NetworkPong extends NetworkProbe {
  serverTime: number;
}

export interface InputPayload {
  up?: boolean;
  down?: boolean;
  left?: boolean;
  right?: boolean;
  attack?: boolean;
}

export interface AttackEvent {
  attackerId: string;
  direction: Direction;
  x: number;
  y: number;
  hitPlayerIds: string[];
}

export interface NotificationEvent {
  kind: "join" | "leave" | "hit" | "defeat" | "system";
  text: string;
}

export interface ClientToServerEvents {
  input: (payload: InputPayload) => void;
  "network:ping": (payload: NetworkProbe) => void;
}

export interface ServerToClientEvents {
  welcome: (payload: WelcomePayload) => void;
  snapshot: (payload: WorldSnapshot) => void;
  attack: (payload: AttackEvent) => void;
  notification: (payload: NotificationEvent) => void;
  "network:pong": (payload: NetworkPong) => void;
}
