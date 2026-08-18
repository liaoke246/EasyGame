import { directionFromAxes, directionVector } from "@easygame/shared";
import type {
  CharacterId,
  Obstacle,
  PlayerIdentity,
  PublicPlayer,
  SpawnSkin,
} from "./protocol.js";

export const WORLD_WIDTH = 3840;
export const WORLD_HEIGHT = 2160;
export const PLAYER_SPEED = 205;
export const PLAYER_RADIUS = 22;
export const MOVE_ACCELERATION = 2_600;
export const TURN_ACCELERATION = 4_200;
export const STOP_DECELERATION = 3_600;
export const TICK_RATE = 30;
export const SNAPSHOT_RATE = 15;
export const ATTACK_COOLDOWN_MS = 620;
export const ATTACK_DURATION_MS = 180;
export const RESPAWN_DELAY_MS = 1_250;
export const USAGI_SPAWN_CHANCE = 0.12;

export const OBSTACLES: Obstacle[] = [
  { id: "cabin-nw", type: "cabin", x: 390, y: 300, width: 330, height: 245, hitboxInset: 8 },
  { id: "cabin-ne", type: "cabin", x: 3_080, y: 260, width: 340, height: 250, hitboxInset: 8 },
  { id: "cabin-south", type: "cabin", x: 1_770, y: 1_680, width: 330, height: 245, hitboxInset: 8 },
  { id: "pond-west", type: "pond", x: 260, y: 1_330, width: 520, height: 310, hitboxInset: 8 },
  { id: "pond-east", type: "pond", x: 3_010, y: 1_070, width: 490, height: 290, hitboxInset: 8 },
  { id: "garden-north", type: "garden", x: 1_160, y: 300, width: 430, height: 230, hitboxInset: 10 },
  { id: "garden-se", type: "garden", x: 2_650, y: 1_690, width: 470, height: 250, hitboxInset: 10 },
  { id: "tree-nw-1", type: "tree", x: 100, y: 90, width: 82, height: 94, hitboxInset: 22 },
  { id: "tree-nw-2", type: "tree", x: 850, y: 120, width: 82, height: 94, hitboxInset: 22 },
  { id: "tree-north", type: "tree", x: 2_040, y: 90, width: 82, height: 94, hitboxInset: 22 },
  { id: "tree-ne", type: "tree", x: 3_620, y: 160, width: 82, height: 94, hitboxInset: 22 },
  { id: "tree-west-1", type: "tree", x: 100, y: 720, width: 82, height: 94, hitboxInset: 22 },
  { id: "tree-west-2", type: "tree", x: 870, y: 940, width: 82, height: 94, hitboxInset: 22 },
  { id: "tree-east-1", type: "tree", x: 3_620, y: 740, width: 82, height: 94, hitboxInset: 22 },
  { id: "tree-east-2", type: "tree", x: 3_480, y: 1_620, width: 82, height: 94, hitboxInset: 22 },
  { id: "tree-south-1", type: "tree", x: 720, y: 1_970, width: 82, height: 94, hitboxInset: 22 },
  { id: "tree-south-2", type: "tree", x: 1_330, y: 2_000, width: 82, height: 94, hitboxInset: 22 },
  { id: "tree-south-3", type: "tree", x: 2_380, y: 1_980, width: 82, height: 94, hitboxInset: 22 },
  { id: "tree-south-4", type: "tree", x: 3_550, y: 2_000, width: 82, height: 94, hitboxInset: 22 },
  { id: "rock-1", type: "rock", x: 980, y: 690, width: 62, height: 48, hitboxInset: 7 },
  { id: "rock-2", type: "rock", x: 2_780, y: 700, width: 66, height: 50, hitboxInset: 7 },
  { id: "rock-3", type: "rock", x: 1_150, y: 1_520, width: 58, height: 46, hitboxInset: 6 },
  { id: "rock-4", type: "rock", x: 2_450, y: 1_430, width: 68, height: 52, hitboxInset: 7 },
  { id: "rock-5", type: "rock", x: 3_450, y: 880, width: 60, height: 46, hitboxInset: 6 },
  { id: "rock-6", type: "rock", x: 520, y: 1_850, width: 64, height: 48, hitboxInset: 7 },
];

export const SPAWN_POINTS = [
  { x: 1_720, y: 960 },
  { x: 1_920, y: 940 },
  { x: 2_120, y: 960 },
  { x: 1_730, y: 1_190 },
  { x: 1_920, y: 1_220 },
  { x: 2_110, y: 1_190 },
];

let testSpawnIndex = 0;
let testSkinIndex = 0;

export const CHARACTER_OPTIONS: Array<
  PlayerIdentity & { characterId: CharacterId }
> = [
  {
    characterId: "ranger",
    roleName: "突击手",
    color: "#4c956c",
    displayId: "",
  },
  {
    characterId: "farmer",
    roleName: "守卫",
    color: "#d8a548",
    displayId: "",
  },
  {
    characterId: "herbalist",
    roleName: "战地医师",
    color: "#8f6bb3",
    displayId: "",
  },
  {
    characterId: "smith",
    roleName: "爆破手",
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

export function rollSpawnSkin(): SpawnSkin {
  if (process.env.TEST_MODE === "1") {
    const skin = testSkinIndex % 2 === 0 ? "usagi" : "default";
    testSkinIndex += 1;
    return skin;
  }
  return Math.random() < USAGI_SPAWN_CHANCE ? "usagi" : "default";
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
    spawnSkin: rollSpawnSkin(),
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
    spawnSkin: player.spawnSkin,
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
  const bounds = obstacleCollisionBounds(rect);
  const closestX = clamp(centerX, bounds.minX, bounds.maxX);
  const closestY = clamp(centerY, bounds.minY, bounds.maxY);
  const deltaX = centerX - closestX;
  const deltaY = centerY - closestY;
  return deltaX * deltaX + deltaY * deltaY < radius * radius;
}

export function obstacleCollisionBounds(rect: Obstacle): {
  minX: number;
  minY: number;
  maxX: number;
  maxY: number;
} {
  const maximumInset = Math.max(0, Math.min(rect.width, rect.height) * 0.5 - 1);
  const inset = clamp(rect.hitboxInset ?? 0, 0, maximumInset);
  return {
    minX: rect.x + inset,
    minY: rect.y + inset,
    maxX: rect.x + rect.width - inset,
    maxY: rect.y + rect.height - inset,
  };
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
