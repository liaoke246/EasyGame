import Phaser from "phaser";
import {
  GAME_ATLAS_KEY,
  HERO_WALK_ATLAS_KEY,
  heroWalkFrame,
  weaponFrame,
} from "./game-atlas";
import type {
  Direction,
  InputPayload,
  Obstacle,
  PublicPlayer,
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
  private readonly sprite: Phaser.GameObjects.Image;
  private readonly weaponSprite: Phaser.GameObjects.Image;
  private readonly nameLabel: Phaser.GameObjects.Text;
  private readonly roleLabel: Phaser.GameObjects.Text;
  private readonly healthTrack: Phaser.GameObjects.Rectangle;
  private readonly healthFill: Phaser.GameObjects.Rectangle;
  private direction: Direction;
  private weapon: PublicPlayer["weapon"];
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
  private walkDistance = 0;
  private motionBlend = 0;
  private currentFrame = "";
  private lastFootstepAt = 0;
  private recoilOffset = 0;

  constructor(
    private readonly scene: Phaser.Scene,
    state: PublicPlayer,
    private readonly isLocal: boolean,
  ) {
    this.direction = state.direction;
    this.weapon = state.weapon;
    this.targetX = state.x;
    this.targetY = state.y;
    this.previousRenderX = state.x;
    this.previousRenderY = state.y;

    this.shadow = scene.add.ellipse(0, 2, 40, 14, 0x17251b, 0.3);
    this.sprite = scene.add.image(
      0,
      -52,
      HERO_WALK_ATLAS_KEY,
      heroWalkFrame(state.direction, 0),
    );
    this.sprite.setOrigin(0.5, 0.5).setDisplaySize(128, 128);
    this.weaponSprite = scene.add
      .image(0, -27, GAME_ATLAS_KEY, weaponFrame(state.weapon))
      .setOrigin(0.5, 0.5);
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
      this.sprite,
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
    } else if (!this.localMoving) {
      this.direction = state.direction;
    }
    this.weapon = state.weapon;
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
      this.updateFacingDirection(xAxis, yAxis);
    }

    const nextX = this.container.x + this.velocityX * deltaSeconds;
    if (!positionCollides(nextX, this.container.y, world)) {
      this.container.x = nextX;
    }
    const nextY = this.container.y + this.velocityY * deltaSeconds;
    if (!positionCollides(this.container.x, nextY, world)) {
      this.container.y = nextY;
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
    this.previousRenderX = this.container.x;
    this.previousRenderY = this.container.y;
    const moving =
      distanceMoved > 0.035 || Math.hypot(this.velocityX, this.velocityY) > 5;
    if (moving) {
      this.walkDistance += Math.min(
        distanceMoved,
        PLAYER_SPEED * deltaSeconds * 1.35,
      );
    }
    const targetMotionBlend = moving ? 1 : 0;
    this.motionBlend = Phaser.Math.Linear(
      this.motionBlend,
      targetMotionBlend,
      1 - Math.exp(-18 * deltaSeconds),
    );
    const walkFrame = moving ? Math.floor(this.walkDistance / 12) % 4 : 0;
    const stepWave = moving
      ? Math.sin((this.walkDistance / 12) * (Math.PI / 2)) * this.motionBlend
      : 0;
    this.sprite.y = -52 + Math.abs(stepWave) * -0.65 + this.recoilOffset;
    this.sprite.setAngle(
      this.direction === "left" || this.direction === "right"
        ? stepWave * 0.18
        : 0,
    );
    const nextFrame = heroWalkFrame(this.direction, walkFrame);
    if (nextFrame !== this.currentFrame) {
      this.currentFrame = nextFrame;
      this.sprite.setTexture(HERO_WALK_ATLAS_KEY, nextFrame);
      if (
        this.isLocal &&
        moving &&
        (walkFrame === 1 || walkFrame === 3) &&
        performance.now() - this.lastFootstepAt > 120
      ) {
        this.lastFootstepAt = performance.now();
        this.showFootstep();
      }
    }
    this.shadow.setScale(1 - Math.abs(stepWave) * 0.035, 1);
    this.shadow.setAlpha(0.3 - Math.abs(stepWave) * 0.035);
    this.updateWeaponSprite(stepWave);
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
    this.container.destroy(true);
  }

  showRecoil(strength: number): void {
    this.scene.tweens.killTweensOf(this);
    this.recoilOffset = strength;
    this.scene.tweens.add({
      targets: this,
      recoilOffset: 0,
      duration: 110,
      ease: "Back.easeOut",
    });
  }

  private updateWeaponSprite(stepWave: number): void {
    this.weaponSprite.setTexture(GAME_ATLAS_KEY, weaponFrame(this.weapon));
    this.weaponSprite.setDisplaySize(this.weapon === "rocket" ? 82 : 64, 38);
    this.weaponSprite.setVisible(this.weapon !== "smg");
    const rotation =
      this.direction === "right"
        ? 0
        : this.direction === "left"
          ? Math.PI
          : this.direction === "up"
            ? -Math.PI / 2
            : Math.PI / 2;
    this.weaponSprite.setRotation(rotation);
    const vector = directionVector(this.direction);
    this.weaponSprite.setPosition(
      vector.x * 15 - vector.x * this.recoilOffset,
      -45 + vector.y * 12 + stepWave * 0.45 - vector.y * this.recoilOffset,
    );
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

  private updateFacingDirection(xAxis: number, yAxis: number): void {
    if (xAxis !== 0 && yAxis !== 0) {
      const keepsHorizontalDirection =
        (this.direction === "left" && xAxis < 0) ||
        (this.direction === "right" && xAxis > 0);
      const keepsVerticalDirection =
        (this.direction === "up" && yAxis < 0) ||
        (this.direction === "down" && yAxis > 0);
      if (keepsHorizontalDirection || keepsVerticalDirection) {
        return;
      }
    }

    if (Math.abs(xAxis) > Math.abs(yAxis)) {
      this.direction = xAxis > 0 ? "right" : "left";
    } else {
      this.direction = yAxis > 0 ? "down" : "up";
    }
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

function directionVector(direction: Direction): { x: number; y: number } {
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
