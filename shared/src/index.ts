export type CardinalDirection = "up" | "down" | "left" | "right";

export type Direction =
  | CardinalDirection
  | "up-left"
  | "up-right"
  | "down-left"
  | "down-right";

export type WeaponId = "smg" | "shotgun" | "rocket";

export interface Vector2 {
  x: number;
  y: number;
}

export const PROJECTILE_VISUAL_ELEVATION = 40;

export const WEAPON_COOLDOWN_MS: Record<WeaponId, number> = {
  smg: 95,
  shotgun: 620,
  rocket: 1_050,
};

export const DIRECTION_VECTORS: Record<Direction, Vector2> = {
  up: { x: 0, y: -1 },
  "up-right": { x: Math.SQRT1_2, y: -Math.SQRT1_2 },
  right: { x: 1, y: 0 },
  "down-right": { x: Math.SQRT1_2, y: Math.SQRT1_2 },
  down: { x: 0, y: 1 },
  "down-left": { x: -Math.SQRT1_2, y: Math.SQRT1_2 },
  left: { x: -1, y: 0 },
  "up-left": { x: -Math.SQRT1_2, y: -Math.SQRT1_2 },
};

export const WEAPON_MUZZLE_OFFSETS: Record<
  WeaponId,
  Record<Direction, Vector2>
> = {
  smg: {
    up: { x: 0, y: -91 },
    "up-right": { x: 42, y: -49 },
    right: { x: 42, y: -49 },
    "down-right": { x: 42, y: -49 },
    down: { x: 22, y: -47 },
    "down-left": { x: -42, y: -49 },
    left: { x: -42, y: -49 },
    "up-left": { x: -42, y: -49 },
  },
  shotgun: {
    up: { x: 0, y: -94 },
    "up-right": { x: 49, y: -50 },
    right: { x: 49, y: -50 },
    "down-right": { x: 49, y: -50 },
    down: { x: 28, y: -52 },
    "down-left": { x: -49, y: -50 },
    left: { x: -49, y: -50 },
    "up-left": { x: -49, y: -50 },
  },
  rocket: {
    up: { x: 0, y: -96 },
    "up-right": { x: 57, y: -51 },
    right: { x: 57, y: -51 },
    "down-right": { x: 57, y: -51 },
    down: { x: 32, y: -48 },
    "down-left": { x: -57, y: -51 },
    left: { x: -57, y: -51 },
    "up-left": { x: -57, y: -51 },
  },
};

export function directionVector(direction: Direction): Vector2 {
  return DIRECTION_VECTORS[direction];
}

export function directionFromAxes(x: number, y: number): Direction {
  if (x !== 0 && y !== 0) {
    return `${y > 0 ? "down" : "up"}-${x > 0 ? "right" : "left"}`;
  }
  if (x !== 0) {
    return x > 0 ? "right" : "left";
  }
  return y > 0 ? "down" : "up";
}

export function cardinalDirectionFromVector(
  x: number,
  y: number,
  fallback: CardinalDirection = "down",
): CardinalDirection {
  if (Math.hypot(x, y) < 0.0001) {
    return fallback;
  }

  const horizontalMagnitude = Math.abs(x);
  const verticalMagnitude = Math.abs(y);
  const switchAxisRatio = 1.2;
  const wasHorizontal = fallback === "left" || fallback === "right";
  if (wasHorizontal) {
    if (verticalMagnitude > horizontalMagnitude * switchAxisRatio) {
      return y > 0 ? "down" : "up";
    }
    return x > 0 ? "right" : "left";
  }
  if (horizontalMagnitude > verticalMagnitude * switchAxisRatio) {
    return x > 0 ? "right" : "left";
  }
  return y > 0 ? "down" : "up";
}

export function visualCardinalDirection(
  direction: Direction,
): CardinalDirection {
  if (direction.endsWith("left")) {
    return "left";
  }
  if (direction.endsWith("right")) {
    return "right";
  }
  return direction as CardinalDirection;
}
