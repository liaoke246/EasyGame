import Phaser from "phaser";
import {
  HERO_WALK_ATLAS_KEY,
  WEAPON_OVERLAY_ATLAS_KEY,
  heroWalkFrame,
  weaponOverlayFrame,
} from "./game-atlas";
import {
  PLAYER_SPRITE_OFFSET_Y,
  WEAPON_OVERLAY_SCALE,
  cardinalDirectionFromVector,
  directionFromAxes,
  directionVector,
  visualCardinalDirection,
  weaponMuzzleOffset,
  weaponOverlayAngleDegrees,
  weaponVisualDirection,
} from "@easygame/shared";
import { PlayerAnimationController } from "./player-animation";
import type {
  CardinalDirection,
  Direction,
  InputPayload,
  Obstacle,
  PublicPlayer,
  WeaponId,
} from "./types";

type MovementInput = Omit<InputPayload, "attack">;

interface PredictionWorld {
  width: number;
  height: number;
  obstacles: Obstacle[];
}

const PLAYER_SPEED = 205;
const PLAYER_RADIUS = 15;
const MOVE_ACCELERATION = 2_600;
const TURN_ACCELERATION = 4_200;
const STOP_DECELERATION = 3_600;
const MAX_EXTRAPOLATION_SECONDS = 0.14;
const LOCAL_SNAP_DISTANCE = 170;
const LOCAL_MOVING_CORRECTION_RADIUS = 44;
const LOCAL_IDLE_CORRECTION_RADIUS = 1.5;
export class PlayerView {
  readonly container: Phaser.GameObjects.Container;
  private readonly shadow: Phaser.GameObjects.Ellipse;
  private readonly bodySprite: Phaser.GameObjects.Image;
  private readonly weaponSprite: Phaser.GameObjects.Image;
  private readonly nameLabel: Phaser.GameObjects.Text;
  private readonly roleLabel: Phaser.GameObjects.Text;
  private readonly healthTrack: Phaser.GameObjects.Rectangle;
  private readonly healthFill: Phaser.GameObjects.Rectangle;
  private direction: Direction;
  private locomotionDirection: CardinalDirection;
  private weapon: PublicPlayer["weapon"];
  private desiredWeapon: PublicPlayer["weapon"];
  private targetX: number;
  private targetY: number;
  private velocityX = 0;
  private velocityY = 0;
  private serverVelocityX = 0;
  private serverVelocityY = 0;
  private health = 100;
  private maxHealth = 100;
  private respawning = false;
  private lastSnapshotAt = performance.now();
  private localMoving = false;
  private localInputChangedAt = performance.now();
  private estimatedLatencyMs = 0;
  private previousRenderX: number;
  private previousRenderY: number;
  private readonly animation = new PlayerAnimationController();
  private currentBodyFrame = "";
  private currentWeaponFrame = "";
  private lastFootstepAt = 0;
  private weaponSwitchVersion = 0;

  constructor(
    private readonly scene: Phaser.Scene,
    state: PublicPlayer,
    private readonly isLocal: boolean,
  ) {
    this.direction = state.direction;
    this.locomotionDirection = visualCardinalDirection(state.direction);
    this.weapon = state.weapon;
    this.desiredWeapon = state.weapon;
    this.targetX = state.x;
    this.targetY = state.y;
    this.previousRenderX = state.x;
    this.previousRenderY = state.y;

    this.shadow = scene.add.ellipse(0, 2, 40, 14, 0x17251b, 0.3);
    this.bodySprite = scene.add.image(
      0,
      PLAYER_SPRITE_OFFSET_Y,
      HERO_WALK_ATLAS_KEY,
      heroWalkFrame(this.locomotionDirection, 0),
    );
    this.bodySprite.setOrigin(0.5, 0.5).setDisplaySize(128, 128);
    this.weaponSprite = scene.add.image(
      0,
      PLAYER_SPRITE_OFFSET_Y,
      WEAPON_OVERLAY_ATLAS_KEY,
      weaponOverlayFrame(state.weapon, weaponVisualDirection(state.direction)),
    );
    this.weaponSprite
      .setOrigin(0.5, 0.5)
      .setDisplaySize(128, 128)
      .setScale(WEAPON_OVERLAY_SCALE);
    this.nameLabel = scene.add
      .text(0, -104, state.displayId, {
        fontFamily: '"Microsoft YaHei", sans-serif',
        fontSize: "12px",
        color: isLocal ? "#fff5c7" : "#ffffff",
        stroke: "#24372b",
        strokeThickness: 4,
      })
      .setOrigin(0.5, 0.5);
    this.roleLabel = scene.add
      .text(0, -90, state.roleName, {
        fontFamily: '"Microsoft YaHei", sans-serif',
        fontSize: "9px",
        color: "#dce8cf",
        stroke: "#24372b",
        strokeThickness: 3,
      })
      .setOrigin(0.5, 0.5);
    this.healthTrack = scene.add
      .rectangle(-18, -78, 36, 5, 0x233329, 0.9)
      .setOrigin(0, 0.5);
    this.healthFill = scene.add
      .rectangle(-17, -78, 34, 3, 0x78c267)
      .setOrigin(0, 0.5);

    this.container = scene.add.container(state.x, state.y, [
      this.shadow,
      this.bodySprite,
      this.weaponSprite,
      this.healthTrack,
      this.healthFill,
      this.roleLabel,
      this.nameLabel,
    ]);

    if (isLocal) {
      const marker = scene.add
        .triangle(0, -117, 0, 0, 9, 0, 4.5, 7, 0xffe391)
        .setOrigin(0.5);
      this.container.add(marker);
    }
  }

  applyState(state: PublicPlayer, latencyMs = 0): void {
    this.targetX = state.x;
    this.targetY = state.y;
    this.lastSnapshotAt = performance.now();
    this.serverVelocityX = state.vx;
    this.serverVelocityY = state.vy;
    this.estimatedLatencyMs = latencyMs;
    if (!this.isLocal) {
      this.velocityX = state.vx;
      this.velocityY = state.vy;
      this.direction = state.direction;
      if (state.weapon !== this.desiredWeapon) {
        this.equipWeapon(state.weapon);
      }
    } else if (!this.localMoving) {
      this.direction = state.direction;
    }
    this.health = state.health;
    this.maxHealth = state.maxHealth;
    this.respawning = state.respawning;
    this.nameLabel.setText(state.displayId);
    this.roleLabel.setText(state.roleName);

    if (this.isLocal) {
      const distance = Phaser.Math.Distance.Between(
        this.container.x,
        this.container.y,
        state.x,
        state.y,
      );
      if (state.respawning || distance > LOCAL_SNAP_DISTANCE) {
        this.container.setPosition(state.x, state.y);
        this.previousRenderX = state.x;
        this.previousRenderY = state.y;
        this.velocityX = state.vx;
        this.velocityY = state.vy;
      }
    }
  }

  predictMovement(
    input: MovementInput,
    deltaSeconds: number,
    world: PredictionWorld,
  ): void {
    if (!this.isLocal || this.respawning) {
      this.localMoving = false;
      return;
    }

    let xAxis = Number(input.right) - Number(input.left);
    let yAxis = Number(input.down) - Number(input.up);
    if (xAxis !== 0 && yAxis !== 0) {
      xAxis *= Math.SQRT1_2;
      yAxis *= Math.SQRT1_2;
    }

    const wasMoving = this.localMoving;
    this.localMoving = xAxis !== 0 || yAxis !== 0;
    if (wasMoving !== this.localMoving) {
      this.localInputChangedAt = performance.now();
    }

    const desiredVelocityX = xAxis * PLAYER_SPEED;
    const desiredVelocityY = yAxis * PLAYER_SPEED;
    const reversing =
      this.velocityX * desiredVelocityX + this.velocityY * desiredVelocityY < 0;
    const acceleration = this.localMoving
      ? reversing
        ? TURN_ACCELERATION
        : MOVE_ACCELERATION
      : STOP_DECELERATION;
    const velocityStep = acceleration * deltaSeconds;
    this.velocityX = moveToward(
      this.velocityX,
      desiredVelocityX,
      velocityStep,
    );
    this.velocityY = moveToward(
      this.velocityY,
      desiredVelocityY,
      velocityStep,
    );

    const speed = Math.hypot(this.velocityX, this.velocityY);
    if (speed > PLAYER_SPEED) {
      const scale = PLAYER_SPEED / speed;
      this.velocityX *= scale;
      this.velocityY *= scale;
    }

    if (this.localMoving) {
      this.direction = directionFromAxes(xAxis, yAxis);
    }

    const nextX = this.container.x + this.velocityX * deltaSeconds;
    if (!positionCollides(nextX, this.container.y, world)) {
      this.container.x = nextX;
    } else {
      this.velocityX = 0;
    }
    const nextY = this.container.y + this.velocityY * deltaSeconds;
    if (!positionCollides(this.container.x, nextY, world)) {
      this.container.y = nextY;
    } else {
      this.velocityY = 0;
    }
  }

  update(deltaSeconds: number, _time: number): void {
    const snapshotAgeSeconds = Math.min(
      (performance.now() - this.lastSnapshotAt) / 1_000,
      MAX_EXTRAPOLATION_SECONDS,
    );
    const latencyLeadSeconds = this.isLocal
      ? Math.min(this.estimatedLatencyMs / 2_000 + 0.03, 0.11)
      : 0;
    const projectedX =
      this.targetX +
      this.serverVelocityX * (snapshotAgeSeconds + latencyLeadSeconds);
    const projectedY =
      this.targetY +
      this.serverVelocityY * (snapshotAgeSeconds + latencyLeadSeconds);

    if (this.isLocal) {
      this.applyLocalCorrection(projectedX, projectedY, deltaSeconds);
    } else {
      const smoothing = 1 - Math.exp(-14 * deltaSeconds);
      this.container.x = Phaser.Math.Linear(
        this.container.x,
        projectedX,
        smoothing,
      );
      this.container.y = Phaser.Math.Linear(
        this.container.y,
        projectedY,
        smoothing,
      );
    }
    this.container.setDepth(Math.round(this.container.y));

    const distanceMoved = Phaser.Math.Distance.Between(
      this.previousRenderX,
      this.previousRenderY,
      this.container.x,
      this.container.y,
    );
    const movementX = this.container.x - this.previousRenderX;
    const movementY = this.container.y - this.previousRenderY;
    this.previousRenderX = this.container.x;
    this.previousRenderY = this.container.y;
    const now = performance.now();
    const velocityForFacingX = this.isLocal
      ? this.velocityX
      : this.serverVelocityX;
    const velocityForFacingY = this.isLocal
      ? this.velocityY
      : this.serverVelocityY;
    const authoritativeSpeed = Math.hypot(
      velocityForFacingX,
      velocityForFacingY,
    );
    const renderedSpeed = distanceMoved / Math.max(deltaSeconds, 0.001);
    const moving =
      !this.respawning && authoritativeSpeed > 8 && renderedSpeed > 4;
    const followsVelocity =
      movementX * velocityForFacingX + movementY * velocityForFacingY >= 0;
    if (moving && followsVelocity) {
      this.locomotionDirection = cardinalDirectionFromVector(
        movementX,
        movementY,
        this.locomotionDirection,
      );
    }
    this.animation.advanceMovement(
      Math.min(distanceMoved, PLAYER_SPEED * deltaSeconds * 1.35),
      moving,
    );
    const pose = this.animation.sample(
      now,
      this.direction,
      this.locomotionDirection,
      moving,
    );
    const nextBodyFrame = heroWalkFrame(pose.direction, pose.frame);
    const weaponDirection = weaponVisualDirection(this.direction);
    const nextWeaponFrame = weaponOverlayFrame(this.weapon, weaponDirection);
    const vector = directionVector(this.direction);
    this.bodySprite.setPosition(0, PLAYER_SPRITE_OFFSET_Y + pose.bodyOffsetY);
    this.bodySprite.setAngle(0);
    this.weaponSprite.setPosition(
      -vector.x * pose.recoil * 0.42,
      PLAYER_SPRITE_OFFSET_Y +
        pose.bodyOffsetY -
        vector.y * pose.recoil * 0.42,
    );
    this.weaponSprite.setAngle(weaponOverlayAngleDegrees(this.direction));
    if (nextBodyFrame !== this.currentBodyFrame) {
      this.currentBodyFrame = nextBodyFrame;
      this.bodySprite.setFrame(nextBodyFrame);
      if (
        pose.state === "walk" &&
        this.isLocal &&
        pose.contactPose &&
        now - this.lastFootstepAt > 165
      ) {
        this.lastFootstepAt = now;
        this.showFootstep();
      }
    }
    if (nextWeaponFrame !== this.currentWeaponFrame) {
      this.currentWeaponFrame = nextWeaponFrame;
      this.weaponSprite.setFrame(nextWeaponFrame);
    }
    this.shadow.setScale(1);
    this.shadow.setAlpha(0.3);
    this.container.setAlpha(this.respawning ? 0.24 : 1);

    const healthRatio = Phaser.Math.Clamp(
      this.health / Math.max(1, this.maxHealth),
      0,
      1,
    );
    this.healthFill.width = 34 * healthRatio;
    this.healthFill.setFillStyle(
      healthRatio > 0.55 ? 0x78c267 : healthRatio > 0.25 ? 0xe1a94d : 0xd85b4f,
    );
  }

  destroy(): void {
    this.scene.tweens.killTweensOf(this.bodySprite);
    this.scene.tweens.killTweensOf(this.weaponSprite);
    this.container.destroy(true);
  }

  equipWeapon(weapon: WeaponId): void {
    this.desiredWeapon = weapon;
    if (weapon === this.weapon && this.weaponSprite.alpha >= 0.99) {
      return;
    }

    this.weaponSwitchVersion += 1;
    const switchVersion = this.weaponSwitchVersion;
    this.scene.tweens.killTweensOf(this.weaponSprite);
    this.scene.tweens.add({
      targets: this.weaponSprite,
      alpha: 0.18,
      duration: 70,
      ease: "Quad.easeIn",
      onComplete: () => {
        if (switchVersion !== this.weaponSwitchVersion) {
          return;
        }
        this.weapon = this.desiredWeapon;
        this.currentWeaponFrame = "";
        this.scene.tweens.add({
          targets: this.weaponSprite,
          alpha: 1,
          duration: 95,
          ease: "Quad.easeOut",
        });
      },
    });
  }

  getMuzzlePosition(): { x: number; y: number } {
    const offset = weaponMuzzleOffset(this.weapon, this.direction);
    return {
      x: this.container.x + offset.x + this.weaponSprite.x,
      y:
        this.container.y +
        offset.y +
        (this.weaponSprite.y - PLAYER_SPRITE_OFFSET_Y),
    };
  }

  getAimDirection(): Direction {
    return this.direction;
  }

  canAct(): boolean {
    return !this.respawning && this.health > 0;
  }

  setTriggerHeld(held: boolean): void {
    if (!this.isLocal) {
      return;
    }
    this.animation.setTriggerHeld(held, performance.now());
  }

  showRecoil(strength: number, firedWeapon: WeaponId): void {
    if (firedWeapon !== this.weapon) {
      this.forceWeapon(firedWeapon);
    }
    this.animation.fire(
      strength,
      firedWeapon === "rocket" ? 190 : firedWeapon === "shotgun" ? 145 : 85,
      performance.now(),
    );
  }

  private forceWeapon(weapon: WeaponId): void {
    this.weaponSwitchVersion += 1;
    this.scene.tweens.killTweensOf(this.weaponSprite);
    this.desiredWeapon = weapon;
    this.weapon = weapon;
    this.currentWeaponFrame = "";
    this.weaponSprite.setAlpha(1);
  }

  private applyLocalCorrection(
    projectedX: number,
    projectedY: number,
    deltaSeconds: number,
  ): void {
    const errorX = projectedX - this.container.x;
    const errorY = projectedY - this.container.y;
    const errorDistance = Math.hypot(errorX, errorY);
    if (errorDistance > LOCAL_SNAP_DISTANCE) {
      this.container.setPosition(projectedX, projectedY);
      this.previousRenderX = projectedX;
      this.previousRenderY = projectedY;
      return;
    }

    const transitionGraceMs = Math.min(this.estimatedLatencyMs + 80, 220);
    if (performance.now() - this.localInputChangedAt < transitionGraceMs) {
      return;
    }

    const localSpeed = Math.hypot(this.velocityX, this.velocityY);
    const serverSpeed = Math.hypot(
      this.serverVelocityX,
      this.serverVelocityY,
    );
    const velocityAlignment =
      localSpeed > 1 && serverSpeed > 1
        ? (this.velocityX * this.serverVelocityX +
            this.velocityY * this.serverVelocityY) /
          (localSpeed * serverSpeed)
        : 1;
    const serverDisagrees = velocityAlignment < 0.15;
    const correctionRadius =
      this.localMoving && !serverDisagrees
        ? LOCAL_MOVING_CORRECTION_RADIUS
        : LOCAL_IDLE_CORRECTION_RADIUS;
    if (errorDistance <= correctionRadius) {
      return;
    }

    const correctionSpeed =
      this.localMoving && !serverDisagrees ? 62 : serverDisagrees ? 420 : 300;
    const correctionDistance = Math.min(
      errorDistance - correctionRadius,
      correctionSpeed * deltaSeconds,
    );
    this.container.x += (errorX / errorDistance) * correctionDistance;
    this.container.y += (errorY / errorDistance) * correctionDistance;
  }

  private showFootstep(): void {
    const side = this.direction === "left" || this.direction === "right";
    for (let index = 0; index < 2; index += 1) {
      const dust = this.scene.add
        .ellipse(
          this.container.x + Phaser.Math.Between(-4, 4),
          this.container.y + Phaser.Math.Between(-1, 2),
          side ? 5 : 7,
          3,
          0xc7aa72,
          0.18,
        )
        .setDepth(Math.round(this.container.y - 2));
      this.scene.tweens.add({
        targets: dust,
        x: dust.x + Phaser.Math.Between(-7, 7),
        y: dust.y + Phaser.Math.Between(1, 5),
        scaleX: 1.8,
        scaleY: 1.35,
        alpha: 0,
        duration: 210 + index * 35,
        ease: "Quad.easeOut",
        onComplete: () => dust.destroy(),
      });
    }
  }
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

function positionCollides(
  x: number,
  y: number,
  world: PredictionWorld,
): boolean {
  if (
    x < PLAYER_RADIUS ||
    y < PLAYER_RADIUS ||
    x > world.width - PLAYER_RADIUS ||
    y > world.height - PLAYER_RADIUS
  ) {
    return true;
  }

  return world.obstacles.some((obstacle) => {
    const closestX = Phaser.Math.Clamp(x, obstacle.x, obstacle.x + obstacle.width);
    const closestY = Phaser.Math.Clamp(y, obstacle.y, obstacle.y + obstacle.height);
    const deltaX = x - closestX;
    const deltaY = y - closestY;
    return deltaX * deltaX + deltaY * deltaY < PLAYER_RADIUS * PLAYER_RADIUS;
  });
}
