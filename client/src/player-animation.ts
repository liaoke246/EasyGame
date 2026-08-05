import {
  visualCardinalDirection,
  type CardinalDirection,
  type Direction,
} from "@easygame/shared";

const WALK_PIXELS_PER_FRAME = 25;
const WALK_FRAME_COUNT = 4;
const WALK_CYCLE_PIXELS = WALK_PIXELS_PER_FRAME * WALK_FRAME_COUNT;
const AIM_RAISE_MS = 90;
const AIM_LOWER_MS = 100;
const SHOT_HOLD_MS = 220;

export interface PlayerAnimationPose {
  state: "idle" | "walk" | "aim" | "recoil";
  direction: CardinalDirection;
  frame: number;
  bodyOffsetY: number;
  recoil: number;
  contactPose: boolean;
}

export class PlayerAnimationController {
  private walkDistance = 0;
  private triggerHeld = false;
  private aimStartedAt = Number.NEGATIVE_INFINITY;
  private aimReleasedAt = Number.NEGATIVE_INFINITY;
  private firedAt = Number.NEGATIVE_INFINITY;
  private recoilStrength = 0;
  private recoilDurationMs = 1;

  advanceMovement(distance: number, moving: boolean): void {
    if (moving && Number.isFinite(distance) && distance > 0) {
      this.walkDistance += distance;
    }
  }

  setTriggerHeld(held: boolean, now: number): void {
    if (held === this.triggerHeld) {
      return;
    }
    this.triggerHeld = held;
    if (held) {
      this.aimStartedAt = now;
      this.aimReleasedAt = Number.NEGATIVE_INFINITY;
    } else {
      this.aimReleasedAt = now;
    }
  }

  fire(strength: number, durationMs: number, now: number): void {
    this.firedAt = now;
    this.recoilStrength = Math.max(0, strength);
    this.recoilDurationMs = Math.max(1, durationMs);
  }

  sample(
    now: number,
    aimDirection: Direction,
    locomotionDirection: CardinalDirection,
    moving: boolean,
  ): PlayerAnimationPose {
    const locomotionFrame =
      Math.floor(this.walkDistance / WALK_PIXELS_PER_FRAME) % WALK_FRAME_COUNT;
    const sinceShot = now - this.firedAt;
    const recoilProgress = clamp(sinceShot / this.recoilDurationMs, 0, 1);
    const recoil = this.recoilStrength * (1 - recoilProgress) ** 2;
    const combatActive =
      this.triggerHeld ||
      sinceShot < SHOT_HOLD_MS ||
      now - this.aimReleasedAt < AIM_LOWER_MS;
    const aimBlend = this.aimBlend(now, sinceShot);
    const walkBodyOffset = moving
      ? -Math.abs(
          Math.sin((this.walkDistance / WALK_CYCLE_PIXELS) * Math.PI * 2),
        ) * 1.25
      : 0;

    if (combatActive) {
      return {
        state: recoil > 0.08 ? "recoil" : "aim",
        direction: visualCardinalDirection(aimDirection),
        frame: moving ? locomotionFrame : 0,
        bodyOffsetY: walkBodyOffset - aimBlend * 0.25,
        recoil,
        contactPose: false,
      };
    }

    if (!moving) {
      return {
        state: "idle",
        direction: locomotionDirection,
        frame: 0,
        bodyOffsetY: 0,
        recoil: 0,
        contactPose: false,
      };
    }

    const frame = locomotionFrame;
    return {
      state: "walk",
      direction: locomotionDirection,
      frame,
      bodyOffsetY: walkBodyOffset,
      recoil: 0,
      contactPose: frame === 0 || frame === 2,
    };
  }

  private aimBlend(now: number, sinceShot: number): number {
    if (this.triggerHeld) {
      return clamp((now - this.aimStartedAt) / AIM_RAISE_MS, 0, 1);
    }
    if (sinceShot < SHOT_HOLD_MS) {
      return 1;
    }
    return clamp(1 - (now - this.aimReleasedAt) / AIM_LOWER_MS, 0, 1);
  }
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.max(minimum, Math.min(maximum, value));
}
