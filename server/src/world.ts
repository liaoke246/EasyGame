import { directionFromAxes, directionVector } from "@easygame/shared";
import type {
  CharacterId,
  Obstacle,
  PlayerIdentity,
  PublicPlayer,
} from "./protocol.js";

export const WORLD_WIDTH = 2560;
export const WORLD_HEIGHT = 1440;
export const PLAYER_SPEED = 205;
export const PLAYER_RADIUS = 15;
export const MOVE_ACCELERATION = 2_600;
export const TURN_ACCELERATION = 4_200;
export const STOP_DECELERATION = 3_600;
export const TICK_RATE = 30;
export const SNAPSHOT_RATE = 15;
export const ATTACK_COOLDOWN_MS = 620;
export const ATTACK_DURATION_MS = 180;
export const RESPAWN_DELAY_MS = 1_250;

export const OBSTACLES: Obstacle[] = [
  { id: "cabin", type: "cabin", x: 1_760, y: 190, width: 330, height: 245 },
  { id: "cabin-2", type: "cabin", x: 380, y: 235, width: 300, height: 225 },
  { id: "pond", type: "pond", x: 230, y: 970, width: 450, height: 270 },
  { id: "garden", type: "garden", x: 1_830, y: 1_015, width: 390, height: 220 },
  { id: "tree-nw-1", type: "tree", x: 90, y: 80, width: 78, height: 92 },
  { id: "tree-nw-2", type: "tree", x: 220, y: 135, width: 78, height: 92 },
  { id: "tree-nw-3", type: "tree", x: 720, y: 80, width: 78, height: 92 },
  { id: "tree-west-1", type: "tree", x: 80, y: 540, width: 78, height: 92 },
  { id: "tree-west-2", type: "tree", x: 180, y: 700, width: 78, height: 92 },
  { id: "tree-north", type: "tree", x: 1_180, y: 70, width: 78, height: 92 },
  { id: "tree-ne-1", type: "tree", x: 2_310, y: 110, width: 78, height: 92 },
  { id: "tree-ne-2", type: "tree", x: 2_180, y: 270, width: 78, height: 92 },
  { id: "tree-east-1", type: "tree", x: 2_390, y: 610, width: 78, height: 92 },
  { id: "tree-east-2", type: "tree", x: 2_300, y: 840, width: 78, height: 92 },
  { id: "tree-south-1", type: "tree", x: 840, y: 1_285, width: 78, height: 92 },
  { id: "tree-south-2", type: "tree", x: 1_060, y: 1_300, width: 78, height: 92 },
  { id: "tree-south-3", type: "tree", x: 1_550, y: 1_285, width: 78, height: 92 },
  { id: "tree-south-4", type: "tree", x: 2_360, y: 1_260, width: 78, height: 92 },
  { id: "rock-1", type: "rock", x: 850, y: 330, width: 58, height: 44 },
  { id: "rock-2", type: "rock", x: 1_590, y: 1_080, width: 62, height: 48 },
  { id: "rock-3", type: "rock", x: 720, y: 930, width: 54, height: 42 },
  { id: "rock-4", type: "rock", x: 2_210, y: 650, width: 64, height: 48 },
  { id: "rock-5", type: "rock", x: 1_510, y: 285, width: 56, height: 44 },
];

export const SPAWN_POINTS = [
  { x: 1_135, y: 630 },
  { x: 1_280, y: 620 },
  { x: 1_425, y: 630 },
  { x: 1_145, y: 800 },
  { x: 1_280, y: 820 },
  { x: 1_415, y: 800 },
];

let testSpawnIndex = 0;

export const CHARACTER_OPTIONS: Array<
  PlayerIdentity & { characterId: CharacterId }
> = [
  {
    characterId: "ranger",
    roleName: "森林游侠",
    color: "#4c956c",
    displayId: "",
  },
  {
    characterId: "farmer",
    roleName: "麦田农夫",
    color: "#d8a548",
    displayId: "",
  },
  {
    characterId: "herbalist",
    roleName: "山谷药师",
    color: "#8f6bb3",
    displayId: "",
  },
  {
    characterId: "smith",
    roleName: "河畔铁匠",
    color: "#c7654d",
    displayId: "",
  },
];

export interface PlayerState extends PublicPlayer {
  guestToken: string;
  firing: boolean;
  input: {
    up: boolean;
    down: boolean;
    left: boolean;
    right: boolean;
  };
  lastAttackAt: number;
  attackEndsAt: number;
  knockbackX: number;
  knockbackY: number;
  knockbackEndsAt: number;
}

export function randomSpawn(): { x: number; y: number } {
  if (process.env.TEST_MODE === "1") {
    const point = [
      { x: 900, y: 540 },
      { x: 970, y: 540 },
    ][testSpawnIndex % 2];
    testSpawnIndex += 1;
    return { ...point };
  }

  const point = SPAWN_POINTS[Math.floor(Math.random() * SPAWN_POINTS.length)];
  return {
    x: point.x + Math.round((Math.random() - 0.5) * 26),
    y: point.y + Math.round((Math.random() - 0.5) * 26),
  };
}

export function createPlayer(
  id: string,
  guestToken: string,
  identity: PlayerIdentity,
): PlayerState {
  const spawn = randomSpawn();
  return {
    id,
    guestToken,
    ...identity,
    x: spawn.x,
    y: spawn.y,
    vx: 0,
    vy: 0,
    direction: "down",
    health: 100,
    maxHealth: 100,
    attacking: false,
    kills: 0,
    respawning: false,
    weapon: "smg",
    firing: false,
    input: { up: false, down: false, left: false, right: false },
    lastAttackAt: -ATTACK_COOLDOWN_MS,
    attackEndsAt: 0,
    knockbackX: 0,
    knockbackY: 0,
    knockbackEndsAt: 0,
  };
}

export function toPublicPlayer(player: PlayerState): PublicPlayer {
  return {
    id: player.id,
    displayId: player.displayId,
    characterId: player.characterId,
    roleName: player.roleName,
    color: player.color,
    x: Math.round(player.x * 10) / 10,
    y: Math.round(player.y * 10) / 10,
    vx: Math.round(player.vx * 10) / 10,
    vy: Math.round(player.vy * 10) / 10,
    direction: player.direction,
    health: player.health,
    maxHealth: player.maxHealth,
    attacking: player.attacking,
    kills: player.kills,
    respawning: player.respawning,
    weapon: player.weapon,
  };
}

export function updatePlayerMovement(
  player: PlayerState,
  deltaSeconds: number,
  now: number,
): void {
  if (player.respawning) {
    player.vx = 0;
    player.vy = 0;
    return;
  }

  let xAxis = Number(player.input.right) - Number(player.input.left);
  let yAxis = Number(player.input.down) - Number(player.input.up);

  if (xAxis !== 0 && yAxis !== 0) {
    const diagonalScale = Math.SQRT1_2;
    xAxis *= diagonalScale;
    yAxis *= diagonalScale;
  }

  if (now < player.knockbackEndsAt) {
    player.vx = player.knockbackX;
    player.vy = player.knockbackY;
  } else {
    const desiredVelocityX = xAxis * PLAYER_SPEED;
    const desiredVelocityY = yAxis * PLAYER_SPEED;
    const moving = xAxis !== 0 || yAxis !== 0;
    const reversing =
      player.vx * desiredVelocityX + player.vy * desiredVelocityY < 0;
    const acceleration = moving
      ? reversing
        ? TURN_ACCELERATION
        : MOVE_ACCELERATION
      : STOP_DECELERATION;
    const velocityStep = acceleration * deltaSeconds;
    player.vx = moveToward(player.vx, desiredVelocityX, velocityStep);
    player.vy = moveToward(player.vy, desiredVelocityY, velocityStep);

    const speed = Math.hypot(player.vx, player.vy);
    if (speed > PLAYER_SPEED) {
      const scale = PLAYER_SPEED / speed;
      player.vx *= scale;
      player.vy *= scale;
    }
  }

  if (xAxis !== 0 || yAxis !== 0) {
    player.direction = directionFromAxes(xAxis, yAxis);
  }

  const nextX = player.x + player.vx * deltaSeconds;
  if (!positionCollides(nextX, player.y)) {
    player.x = nextX;
  } else {
    player.vx = 0;
  }

  const nextY = player.y + player.vy * deltaSeconds;
  if (!positionCollides(player.x, nextY)) {
    player.y = nextY;
  } else {
    player.vy = 0;
  }
}

export function canHit(
  attacker: PlayerState,
  victim: PlayerState,
): boolean {
  if (attacker.id === victim.id || victim.respawning || victim.health <= 0) {
    return false;
  }

  const facing = directionVector(attacker.direction);
  const relativeX = victim.x - attacker.x;
  const relativeY = victim.y - attacker.y;
  const forward = relativeX * facing.x + relativeY * facing.y;
  const side = Math.abs(relativeX * -facing.y + relativeY * facing.x);

  return forward >= 2 && forward <= 76 && side <= 38;
}

export function positionCollides(
  x: number,
  y: number,
  radius = PLAYER_RADIUS,
): boolean {
  if (
    x < radius ||
    y < radius ||
    x > WORLD_WIDTH - radius ||
    y > WORLD_HEIGHT - radius
  ) {
    return true;
  }

  return OBSTACLES.some((obstacle) =>
    circleIntersectsRect(x, y, obstacle, radius),
  );
}

function circleIntersectsRect(
  centerX: number,
  centerY: number,
  rect: Obstacle,
  radius: number,
): boolean {
  const closestX = clamp(centerX, rect.x, rect.x + rect.width);
  const closestY = clamp(centerY, rect.y, rect.y + rect.height);
  const deltaX = centerX - closestX;
  const deltaY = centerY - closestY;
  return deltaX * deltaX + deltaY * deltaY < radius * radius;
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.max(minimum, Math.min(maximum, value));
}

function moveToward(
  current: number,
  target: number,
  maximumDelta: number,
): number {
  if (Math.abs(target - current) <= maximumDelta) {
    return target;
  }
  return current + Math.sign(target - current) * maximumDelta;
}
