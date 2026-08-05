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
export const PLAYER_SPRITE_OFFSET_Y = -52;
export const WEAPON_OVERLAY_SCALE = 0.68;

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

// These are measured barrel-tip pixels in the 128 px weapon-overlay cells.
// Keeping the source-space anchors next to the overlay transform means the
// renderer, predicted effects, and authoritative hit tests cannot drift apart.
const WEAPON_MUZZLE_PIXELS: Record<
  WeaponId,
  Record<CardinalDirection, Vector2>
> = {
  smg: {
    down: { x: 75, y: 108 },
    up: { x: 57, y: 41 },
    right: { x: 91, y: 74 },
    left: { x: 20, y: 74 },
  },
  shotgun: {
    down: { x: 74, y: 101 },
    up: { x: 56, y: 19 },
    right: { x: 99, y: 68 },
    left: { x: 12, y: 68 },
  },
  rocket: {
    down: { x: 75, y: 86 },
    up: { x: 56, y: 8 },
    right: { x: 109, y: 52 },
    left: { x: 1, y: 52 },
  },
};

export function directionVector(direction: Direction): Vector2 {
  return DIRECTION_VECTORS[direction];
}

export function weaponVisualDirection(
  direction: Direction,
): CardinalDirection {
  return visualCardinalDirection(direction);
}

export function weaponOverlayAngleDegrees(direction: Direction): number {
  switch (direction) {
    case "up-right":
      return -45;
    case "down-right":
      return 45;
    case "up-left":
      return 45;
    case "down-left":
      return -45;
    default:
      return 0;
  }
}

export function weaponMuzzleOffset(
  weapon: WeaponId,
  direction: Direction,
): Vector2 {
  const visualDirection = weaponVisualDirection(direction);
  const sourcePoint = WEAPON_MUZZLE_PIXELS[weapon][visualDirection];
  const sourceOffset = {
    x: (sourcePoint.x - 64) * WEAPON_OVERLAY_SCALE,
    y: (sourcePoint.y - 64) * WEAPON_OVERLAY_SCALE,
  };
  const radians = (weaponOverlayAngleDegrees(direction) * Math.PI) / 180;
  const cosine = Math.cos(radians);
  const sine = Math.sin(radians);
  return {
    x: sourceOffset.x * cosine - sourceOffset.y * sine,
    y:
      PLAYER_SPRITE_OFFSET_Y +
      sourceOffset.x * sine +
      sourceOffset.y * cosine,
  };
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
