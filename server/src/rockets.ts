import { randomUUID } from "node:crypto";
import { directionVector } from "@easygame/shared";
import type { Direction, PublicRocket } from "./protocol.js";
import {
  positionCollides,
  type PlayerState,
} from "./world.js";
import { weaponMuzzlePosition } from "./weapons.js";
import type { ZombieState } from "./zombies.js";

const ROCKET_SPEED = 430;
const ROCKET_RADIUS = 8;
const ROCKET_RANGE = 650;
const EXPLOSION_RADIUS = 120;
const MAX_STEP_DISTANCE = 6;

export interface RocketState extends PublicRocket {
  direction: Direction;
  originX: number;
  originY: number;
}

export interface RocketImpact {
  x: number;
  y: number;
  hitZombieIds: string[];
}

export function createRocket(attacker: PlayerState): RocketState {
  const vector = directionVector(attacker.direction);
  const muzzle = weaponMuzzlePosition(attacker, "rocket");
  const x = muzzle.x;
  const y = muzzle.y;
  return {
    id: randomUUID(),
    ownerId: attacker.id,
    direction: attacker.direction,
    x,
    y,
    vx: vector.x * ROCKET_SPEED,
    vy: vector.y * ROCKET_SPEED,
    originX: x,
    originY: y,
  };
}

export function updateRocket(
  rocket: RocketState,
  zombies: Iterable<ZombieState>,
  deltaSeconds: number,
): RocketImpact | undefined {
  const living = Array.from(zombies).filter((zombie) => zombie.health > 0);
  const travelDistance = ROCKET_SPEED * deltaSeconds;
  const steps = Math.max(1, Math.ceil(travelDistance / MAX_STEP_DISTANCE));
  const stepSeconds = deltaSeconds / steps;

  for (let step = 0; step < steps; step += 1) {
    rocket.x += rocket.vx * stepSeconds;
    rocket.y += rocket.vy * stepSeconds;

    const hitWall = positionCollides(rocket.x, rocket.y, ROCKET_RADIUS);
    const hitZombie = living.some((zombie) => {
      const radius = zombie.kind === "brute" ? 25 : 18;
      return (
        Math.hypot(zombie.x - rocket.x, zombie.y - rocket.y) <=
        radius + ROCKET_RADIUS
      );
    });
    const reachedRange =
      Math.hypot(rocket.x - rocket.originX, rocket.y - rocket.originY) >=
      ROCKET_RANGE;

    if (hitWall || hitZombie || reachedRange) {
      return explodeRocket(rocket, living);
    }
  }

  return undefined;
}

export function toPublicRocket(rocket: RocketState): PublicRocket {
  return {
    id: rocket.id,
    ownerId: rocket.ownerId,
    x: round(rocket.x),
    y: round(rocket.y),
    vx: round(rocket.vx),
    vy: round(rocket.vy),
  };
}

function explodeRocket(
  rocket: RocketState,
  zombies: ZombieState[],
): RocketImpact {
  const hitZombieIds: string[] = [];
  for (const zombie of zombies) {
    const distance = Math.hypot(zombie.x - rocket.x, zombie.y - rocket.y);
    if (distance > EXPLOSION_RADIUS) {
      continue;
    }
    const damage = Math.max(32, Math.round(100 - distance * 0.55));
    zombie.health = Math.max(0, zombie.health - damage);
    hitZombieIds.push(zombie.id);
  }
  return { x: rocket.x, y: rocket.y, hitZombieIds };
}

function round(value: number): number {
  return Math.round(value * 10) / 10;
}
