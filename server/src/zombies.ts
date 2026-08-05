import { randomInt, randomUUID } from "node:crypto";
import type { Direction, PublicZombie, ZombieKind } from "./protocol.js";
import {
  PLAYER_RADIUS,
  WORLD_HEIGHT,
  WORLD_WIDTH,
  positionCollides,
  type PlayerState,
} from "./world.js";

const ZOMBIE_RADIUS = 17;
const CONTACT_DISTANCE = PLAYER_RADIUS + ZOMBIE_RADIUS + 4;
const CONTACT_DAMAGE = 12;
const CONTACT_COOLDOWN_MS = 850;

const ZOMBIE_STATS: Record<
  ZombieKind,
  { health: number; speed: number }
> = {
  walker: { health: 70, speed: 58 },
  runner: { health: 45, speed: 88 },
  brute: { health: 150, speed: 42 },
};

export interface ZombieState extends PublicZombie {
  lastAttackAt: number;
}

export function createZombie(index: number): ZombieState {
  const kind = chooseKind();
  const stats = ZOMBIE_STATS[kind];
  const spawn = zombieSpawn(index);
  return {
    id: randomUUID(),
    kind,
    ...spawn,
    vx: 0,
    vy: 0,
    direction: "down",
    health: stats.health,
    maxHealth: stats.health,
    lastAttackAt: -CONTACT_COOLDOWN_MS,
  };
}

export function updateZombie(
  zombie: ZombieState,
  players: Iterable<PlayerState>,
  deltaSeconds: number,
  now: number,
): PlayerState | undefined {
  const target = nearestLivingPlayer(zombie, players);
  if (!target) {
    zombie.vx = 0;
    zombie.vy = 0;
    return undefined;
  }

  const deltaX = target.x - zombie.x;
  const deltaY = target.y - zombie.y;
  const distance = Math.hypot(deltaX, deltaY);
  if (distance <= CONTACT_DISTANCE) {
    zombie.vx = 0;
    zombie.vy = 0;
    if (now - zombie.lastAttackAt >= CONTACT_COOLDOWN_MS) {
      zombie.lastAttackAt = now;
      target.health = Math.max(0, target.health - CONTACT_DAMAGE);
      return target;
    }
    return undefined;
  }

  const speed = ZOMBIE_STATS[zombie.kind].speed;
  const normalX = distance > 0 ? deltaX / distance : 0;
  const normalY = distance > 0 ? deltaY / distance : 0;
  zombie.vx = normalX * speed;
  zombie.vy = normalY * speed;
  zombie.direction = directionFromVector(normalX, normalY);

  const nextX = zombie.x + zombie.vx * deltaSeconds;
  if (!positionCollides(nextX, zombie.y)) {
    zombie.x = nextX;
  } else {
    zombie.vx = 0;
  }
  const nextY = zombie.y + zombie.vy * deltaSeconds;
  if (!positionCollides(zombie.x, nextY)) {
    zombie.y = nextY;
  } else {
    zombie.vy = 0;
  }
  return undefined;
}

export function toPublicZombie(zombie: ZombieState): PublicZombie {
  return {
    id: zombie.id,
    kind: zombie.kind,
    x: round(zombie.x),
    y: round(zombie.y),
    vx: round(zombie.vx),
    vy: round(zombie.vy),
    direction: zombie.direction,
    health: zombie.health,
    maxHealth: zombie.maxHealth,
  };
}

function chooseKind(): ZombieKind {
  if (process.env.TEST_MODE === "1") {
    return "walker";
  }
  const roll = randomInt(100);
  return roll < 18 ? "brute" : roll < 45 ? "runner" : "walker";
}

function zombieSpawn(index: number): { x: number; y: number } {
  if (process.env.TEST_MODE === "1") {
    return { x: 1_045 + index * 35, y: 540 };
  }

  for (let attempt = 0; attempt < 30; attempt += 1) {
    const edge = randomInt(4);
    const margin = 32;
    const candidate = edgeSpawn(edge, margin);
    if (!positionCollides(candidate.x, candidate.y)) {
      return candidate;
    }
  }
  return { x: 960, y: 50 };
}

function edgeSpawn(edge: number, margin: number): { x: number; y: number } {
  if (edge === 0) {
    return { x: margin, y: randomInt(margin, WORLD_HEIGHT - margin) };
  }
  if (edge === 1) {
    return {
      x: WORLD_WIDTH - margin,
      y: randomInt(margin, WORLD_HEIGHT - margin),
    };
  }
  if (edge === 2) {
    return { x: randomInt(margin, WORLD_WIDTH - margin), y: margin };
  }
  return {
    x: randomInt(margin, WORLD_WIDTH - margin),
    y: WORLD_HEIGHT - margin,
  };
}

function nearestLivingPlayer(
  zombie: ZombieState,
  players: Iterable<PlayerState>,
): PlayerState | undefined {
  let nearest: PlayerState | undefined;
  let nearestDistance = Number.POSITIVE_INFINITY;
  for (const player of players) {
    if (player.respawning || player.health <= 0) {
      continue;
    }
    const distance = Math.hypot(player.x - zombie.x, player.y - zombie.y);
    if (distance < nearestDistance) {
      nearest = player;
      nearestDistance = distance;
    }
  }
  return nearest;
}

function directionFromVector(x: number, y: number): Direction {
  if (Math.abs(x) > Math.abs(y)) {
    return x > 0 ? "right" : "left";
  }
  return y > 0 ? "down" : "up";
}

function round(value: number): number {
  return Math.round(value * 10) / 10;
}
