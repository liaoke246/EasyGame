import {
  PROJECTILE_VISUAL_ELEVATION,
  WEAPON_MUZZLE_OFFSETS,
  directionVector,
} from "@easygame/shared";
import type { AttackEvent, WeaponId, WeaponTrace } from "./protocol.js";
import type { PlayerState } from "./world.js";
import type { ZombieState } from "./zombies.js";

export const WEAPON_COOLDOWN_MS: Record<WeaponId, number> = {
  smg: 95,
  shotgun: 620,
  rocket: 1_050,
};

// Player and zombie positions sit on the ground plane, while the weapon art is
// drawn above it. Converting the visual muzzle back to the ground plane keeps
// authoritative ray tests aligned with the sprite without aiming at its feet.

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
  const result =
    attacker.weapon === "smg"
      ? fireSmg(attacker, living)
      : attacker.weapon === "shotgun"
        ? fireShotgun(attacker, living)
        : { hitZombieIds: new Set<string>(), traces: [] };

  return {
    attackerId: attacker.id,
    weapon: attacker.weapon,
    phase: "fire",
    direction: attacker.direction,
    x: attacker.x,
    y: attacker.y,
    hitPlayerIds: [],
    hitZombieIds: Array.from(result.hitZombieIds),
    killedZombieIds: [],
    traces: result.traces,
  };
}

function fireSmg(
  attacker: PlayerState,
  zombies: ZombieState[],
): FireResult {
  const origin = weaponMuzzlePosition(attacker);
  const hit = nearestRayTarget(origin, attacker, zombies, 520, 18);
  if (hit) {
    hit.health = Math.max(0, hit.health - 14);
  }
  const end = hit ?? rayEnd(origin, attacker, 520, 0);
  return {
    hitZombieIds: new Set(hit ? [hit.id] : []),
    traces: [{ endX: end.x, endY: end.y, hit: Boolean(hit) }],
  };
}

function fireShotgun(
  attacker: PlayerState,
  zombies: ZombieState[],
): FireResult {
  const origin = weaponMuzzlePosition(attacker);
  const hitZombieIds = new Set<string>();
  const traces: WeaponTrace[] = [];
  for (const angle of [-16, -10, -5, 0, 5, 10, 16]) {
    const hit = nearestRayTarget(origin, attacker, zombies, 310, 16, angle);
    if (hit) {
      hit.health = Math.max(0, hit.health - 13);
      hitZombieIds.add(hit.id);
    }
    const end = hit ?? rayEnd(origin, attacker, 310, angle);
    traces.push({ endX: end.x, endY: end.y, hit: Boolean(hit) });
  }
  return { hitZombieIds, traces };
}

interface FireResult {
  hitZombieIds: Set<string>;
  traces: WeaponTrace[];
}

function nearestRayTarget(
  origin: { x: number; y: number },
  attacker: PlayerState,
  zombies: ZombieState[],
  range: number,
  halfWidth: number,
  angleDegrees = 0,
): ZombieState | undefined {
  const vector = rotatedDirection(attacker, angleDegrees);
  let nearest: ZombieState | undefined;
  let nearestForward = range + 1;
  for (const zombie of zombies) {
    const relativeX = zombie.x - origin.x;
    const relativeY = zombie.y - origin.y;
    const forward = relativeX * vector.x + relativeY * vector.y;
    const side = Math.abs(relativeX * -vector.y + relativeY * vector.x);
    if (
      forward > 0 &&
      forward <= range &&
      side <= halfWidth &&
      forward < nearestForward
    ) {
      nearest = zombie;
      nearestForward = forward;
    }
  }
  return nearest;
}

function rayEnd(
  origin: { x: number; y: number },
  attacker: PlayerState,
  range: number,
  angleDegrees: number,
): { x: number; y: number } {
  const vector = rotatedDirection(attacker, angleDegrees);
  return {
    x: origin.x + vector.x * range,
    y: origin.y + vector.y * range,
  };
}

export function weaponMuzzlePosition(
  attacker: PlayerState,
  weapon: WeaponId = attacker.weapon,
): { x: number; y: number } {
  const offset = WEAPON_MUZZLE_OFFSETS[weapon][attacker.direction];
  return {
    x: attacker.x + offset.x,
    y: attacker.y + offset.y + PROJECTILE_VISUAL_ELEVATION,
  };
}

function rotatedDirection(
  attacker: PlayerState,
  angleDegrees: number,
): { x: number; y: number } {
  const facing = directionVector(attacker.direction);
  const angle = (angleDegrees * Math.PI) / 180;
  const cosine = Math.cos(angle);
  const sine = Math.sin(angle);
  return {
    x: facing.x * cosine - facing.y * sine,
    y: facing.x * sine + facing.y * cosine,
  };
}
