import type {
  CharacterId,
  Direction,
  Obstacle,
  PlayerIdentity,
  PublicPlayer,
} from "./protocol.js";

export const WORLD_WIDTH = 1920;
export const WORLD_HEIGHT = 1080;
export const PLAYER_SPEED = 190;
export const PLAYER_RADIUS = 15;
export const TICK_RATE = 20;
export const SNAPSHOT_RATE = 10;
export const ATTACK_COOLDOWN_MS = 620;
export const ATTACK_DURATION_MS = 180;
export const RESPAWN_DELAY_MS = 1_250;

export const OBSTACLES: Obstacle[] = [
  { id: "cabin", type: "cabin", x: 1260, y: 120, width: 300, height: 220 },
  { id: "pond", type: "pond", x: 150, y: 720, width: 410, height: 240 },
  { id: "garden", type: "garden", x: 1340, y: 750, width: 360, height: 190 },
  { id: "tree-nw-1", type: "tree", x: 95, y: 110, width: 74, height: 86 },
  { id: "tree-nw-2", type: "tree", x: 205, y: 165, width: 74, height: 86 },
  { id: "tree-nw-3", type: "tree", x: 330, y: 95, width: 74, height: 86 },
  { id: "tree-west", type: "tree", x: 90, y: 430, width: 74, height: 86 },
  { id: "tree-north", type: "tree", x: 760, y: 90, width: 74, height: 86 },
  { id: "tree-ne", type: "tree", x: 1720, y: 120, width: 74, height: 86 },
  { id: "tree-east-1", type: "tree", x: 1770, y: 460, width: 74, height: 86 },
  { id: "tree-east-2", type: "tree", x: 1660, y: 600, width: 74, height: 86 },
  { id: "tree-south-1", type: "tree", x: 780, y: 930, width: 74, height: 86 },
  { id: "tree-south-2", type: "tree", x: 930, y: 950, width: 74, height: 86 },
  { id: "rock-1", type: "rock", x: 620, y: 270, width: 52, height: 40 },
  { id: "rock-2", type: "rock", x: 1090, y: 850, width: 58, height: 44 },
  { id: "rock-3", type: "rock", x: 450, y: 430, width: 46, height: 38 },
];

export const SPAWN_POINTS = [
  { x: 830, y: 470 },
  { x: 960, y: 500 },
  { x: 1_080, y: 480 },
  { x: 850, y: 610 },
  { x: 980, y: 650 },
  { x: 1_105, y: 610 },
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
  };
}

export function directionVector(direction: Direction): {
  x: number;
  y: number;
} {
  switch (direction) {
    case "up":
      return { x: 0, y: -1 };
    case "down":
      return { x: 0, y: 1 };
    case "left":
      return { x: -1, y: 0 };
    case "right":
      return { x: 1, y: 0 };
  }
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
    player.vx = xAxis * PLAYER_SPEED;
    player.vy = yAxis * PLAYER_SPEED;
  }

  if (xAxis !== 0 || yAxis !== 0) {
    if (Math.abs(xAxis) > Math.abs(yAxis)) {
      player.direction = xAxis > 0 ? "right" : "left";
    } else {
      player.direction = yAxis > 0 ? "down" : "up";
    }
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

function positionCollides(x: number, y: number): boolean {
  if (
    x < PLAYER_RADIUS ||
    y < PLAYER_RADIUS ||
    x > WORLD_WIDTH - PLAYER_RADIUS ||
    y > WORLD_HEIGHT - PLAYER_RADIUS
  ) {
    return true;
  }

  return OBSTACLES.some((obstacle) => circleIntersectsRect(x, y, obstacle));
}

function circleIntersectsRect(
  centerX: number,
  centerY: number,
  rect: Obstacle,
): boolean {
  const closestX = clamp(centerX, rect.x, rect.x + rect.width);
  const closestY = clamp(centerY, rect.y, rect.y + rect.height);
  const deltaX = centerX - closestX;
  const deltaY = centerY - closestY;
  return deltaX * deltaX + deltaY * deltaY < PLAYER_RADIUS * PLAYER_RADIUS;
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.max(minimum, Math.min(maximum, value));
}
