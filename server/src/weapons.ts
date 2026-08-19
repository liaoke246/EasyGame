import {
  WEAPON_COOLDOWN_MS,
  WEAPON_MUZZLE_DISTANCES,
  directionVector,
} from "@easygame/shared";
import type { AttackEvent, WeaponId, WeaponTrace } from "./protocol.js";
import {
  OBSTACLES,
  WORLD_HEIGHT,
  WORLD_WIDTH,
  obstacleCollisionBounds,
  type PlayerState,
} from "./world.js";
import { zombieHitRadius, type ZombieState } from "./zombies.js";

export const SMG_RANGE = 1_100;
export const SHOTGUN_RANGE = 520;

export function isWeaponId(value: unknown): value is WeaponId {
  return value === "smg" || value === "shotgun" || value === "rocket";
}

export function fireWeapon(
  attacker: PlayerState,
  zombies: Iterable<ZombieState>,
  now: number,
): AttackEvent | undefined {
  const cooldownMs = WEAPON_COOLDOWN_MS[attacker.weapon];
  if (
    attacker.respawning ||
    attacker.health <= 0 ||
    now - attacker.lastAttackAt < cooldownMs
  ) {
    return undefined;
  }

  attacker.lastAttackAt = now;
  attacker.attackEndsAt = now + Math.min(cooldownMs, 180);
  attacker.attacking = true;

  const living = Array.from(zombies).filter((zombie) => zombie.health > 0);
  const aim = playerAimVector(attacker);
  const origin = weaponMuzzlePosition(attacker);
  const result =
    attacker.weapon === "smg"
      ? fireSmg(origin, aim, living)
      : attacker.weapon === "shotgun"
        ? fireShotgun(origin, aim, living)
        : { hitZombieIds: new Set<string>(), traces: [] };

  return {
    attackerId: attacker.id,
    weapon: attacker.weapon,
    phase: "fire",
    direction: attacker.direction,
    aimX: aim.x,
    aimY: aim.y,
    x: origin.x,
    y: origin.y,
    hitPlayerIds: [],
    hitZombieIds: Array.from(result.hitZombieIds),
    killedZombieIds: [],
    traces: result.traces,
  };
}

function fireSmg(
  origin: { x: number; y: number },
  aim: { x: number; y: number },
  zombies: ZombieState[],
): FireResult {
  const result = traceBallistic(origin, aim, zombies, SMG_RANGE, 4.5);
  if (result.target) {
    result.target.health = Math.max(0, result.target.health - 14);
  }
  return {
    hitZombieIds: new Set(result.target ? [result.target.id] : []),
    traces: [{ endX: result.endX, endY: result.endY, hit: result.impacted }],
  };
}

function fireShotgun(
  origin: { x: number; y: number },
  aim: { x: number; y: number },
  zombies: ZombieState[],
): FireResult {
  const hitZombieIds = new Set<string>();
  const traces: WeaponTrace[] = [];
  for (const angle of [-16, -10, -5, 0, 5, 10, 16]) {
    const result = traceBallistic(origin, aim, zombies, SHOTGUN_RANGE, 3.5, angle);
    if (result.target) {
      result.target.health = Math.max(0, result.target.health - 13);
      hitZombieIds.add(result.target.id);
    }
    traces.push({ endX: result.endX, endY: result.endY, hit: result.impacted });
  }
  return { hitZombieIds, traces };
}

interface FireResult {
  hitZombieIds: Set<string>;
  traces: WeaponTrace[];
}

interface BallisticResult {
  target?: ZombieState;
  endX: number;
  endY: number;
  impacted: boolean;
}

function traceBallistic(
  origin: { x: number; y: number },
  aim: { x: number; y: number },
  zombies: ZombieState[],
  range: number,
  projectileRadius: number,
  angleDegrees = 0,
): BallisticResult {
  const vector = rotatedDirection(aim, angleDegrees);
  let nearestDistance = worldBoundaryDistance(origin, vector, range, projectileRadius);
  let nearestTarget: ZombieState | undefined;
  let impacted = nearestDistance < range;

  for (const obstacle of OBSTACLES) {
    const bounds = obstacleCollisionBounds(obstacle);
    const distance = rayBoxDistance(
      origin,
      vector,
      bounds.minX - projectileRadius,
      bounds.minY - projectileRadius,
      bounds.maxX + projectileRadius,
      bounds.maxY + projectileRadius,
      nearestDistance,
    );
    if (distance < nearestDistance) {
      nearestDistance = distance;
      nearestTarget = undefined;
      impacted = true;
    }
  }

  for (const zombie of zombies) {
    const distance = rayCircleDistance(
      origin,
      vector,
      zombie.x,
      zombie.y,
      zombieHitRadius(zombie.kind) + projectileRadius,
    );
    if (distance >= 0 && distance < nearestDistance) {
      nearestDistance = distance;
      nearestTarget = zombie;
      impacted = true;
    }
  }

  return {
    target: nearestTarget,
    endX: origin.x + vector.x * nearestDistance,
    endY: origin.y + vector.y * nearestDistance,
    impacted,
  };
}

function worldBoundaryDistance(
  origin: { x: number; y: number },
  vector: { x: number; y: number },
  range: number,
  radius: number,
): number {
  let distance = range;
  if (vector.x > 0) distance = Math.min(distance, (WORLD_WIDTH - radius - origin.x) / vector.x);
  if (vector.x < 0) distance = Math.min(distance, (radius - origin.x) / vector.x);
  if (vector.y > 0) distance = Math.min(distance, (WORLD_HEIGHT - radius - origin.y) / vector.y);
  if (vector.y < 0) distance = Math.min(distance, (radius - origin.y) / vector.y);
  return Math.max(0, distance);
}

function rayCircleDistance(
  origin: { x: number; y: number },
  vector: { x: number; y: number },
  centerX: number,
  centerY: number,
  radius: number,
): number {
  const relativeX = centerX - origin.x;
  const relativeY = centerY - origin.y;
  const forward = relativeX * vector.x + relativeY * vector.y;
  if (forward <= 0) return -1;
  const sideSquared = relativeX * relativeX + relativeY * relativeY - forward * forward;
  const radiusSquared = radius * radius;
  if (sideSquared > radiusSquared) return -1;
  return Math.max(0, forward - Math.sqrt(radiusSquared - sideSquared));
}

function rayBoxDistance(
  origin: { x: number; y: number },
  vector: { x: number; y: number },
  minX: number,
  minY: number,
  maxX: number,
  maxY: number,
  limit: number,
): number {
  let near = 0;
  let far = limit;
  for (const [position, direction, minimum, maximum] of [
    [origin.x, vector.x, minX, maxX],
    [origin.y, vector.y, minY, maxY],
  ] as const) {
    if (Math.abs(direction) < 1e-9) {
      if (position < minimum || position > maximum) return Number.POSITIVE_INFINITY;
      continue;
    }
    let first = (minimum - position) / direction;
    let second = (maximum - position) / direction;
    if (first > second) [first, second] = [second, first];
    near = Math.max(near, first);
    far = Math.min(far, second);
    if (near > far) return Number.POSITIVE_INFINITY;
  }
  return near >= 0 && near <= limit ? near : Number.POSITIVE_INFINITY;
}

export function weaponMuzzlePosition(
  attacker: PlayerState,
  weapon: WeaponId = attacker.weapon,
): { x: number; y: number } {
  const aim = playerAimVector(attacker);
  const distance = WEAPON_MUZZLE_DISTANCES[weapon];
  return {
    x: attacker.x + aim.x * distance,
    y: attacker.y + aim.y * distance,
  };
}

export function playerAimVector(attacker: PlayerState): { x: number; y: number } {
  const magnitude = Math.hypot(attacker.aimX, attacker.aimY);
  if (Number.isFinite(magnitude) && magnitude >= 0.1) {
    return { x: attacker.aimX / magnitude, y: attacker.aimY / magnitude };
  }
  return directionVector(attacker.direction);
}

function rotatedDirection(
  aim: { x: number; y: number },
  angleDegrees: number,
): { x: number; y: number } {
  const angle = (angleDegrees * Math.PI) / 180;
  const cosine = Math.cos(angle);
  const sine = Math.sin(angle);
  return {
    x: aim.x * cosine - aim.y * sine,
    y: aim.x * sine + aim.y * cosine,
  };
}
