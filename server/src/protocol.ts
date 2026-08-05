import type {
  CardinalDirection,
  Direction,
  WeaponId,
} from "@easygame/shared";

export type { CardinalDirection, Direction, WeaponId } from "@easygame/shared";

export type CharacterId = "ranger" | "farmer" | "herbalist" | "smith";

export type ZombieKind = "walker" | "runner" | "brute";

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
  weapon: WeaponId;
}

export interface PublicZombie {
  id: string;
  kind: ZombieKind;
  x: number;
  y: number;
  vx: number;
  vy: number;
  direction: CardinalDirection;
  health: number;
  maxHealth: number;
}

export interface PublicRocket {
  id: string;
  ownerId: string;
  x: number;
  y: number;
  vx: number;
  vy: number;
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
  zombies: PublicZombie[];
  rockets: PublicRocket[];
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
  fire?: boolean;
  weapon?: WeaponId;
}

export interface WeaponTrace {
  endX: number;
  endY: number;
  hit: boolean;
}

export interface AttackEvent {
  attackerId: string;
  weapon: WeaponId;
  phase: "fire" | "impact";
  direction: Direction;
  x: number;
  y: number;
  hitPlayerIds: string[];
  hitZombieIds: string[];
  killedZombieIds: string[];
  traces: WeaponTrace[];
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
