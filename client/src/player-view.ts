import Phaser from "phaser";
import {
  GAME_ATLAS_KEY,
  heroFrame,
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

const PLAYER_SPEED = 190;
const PLAYER_RADIUS = 15;
const MAX_EXTRAPOLATION_SECONDS = 0.12;
const LOCAL_SNAP_DISTANCE = 120;

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
  private health = 100;
  private maxHealth = 100;
  private respawning = false;
  private lastSnapshotAt = performance.now();
  private localMoving = false;

  constructor(
    scene: Phaser.Scene,
    state: PublicPlayer,
    private readonly isLocal: boolean,
  ) {
    this.direction = state.direction;
    this.weapon = state.weapon;
    this.targetX = state.x;
    this.targetY = state.y;

    this.shadow = scene.add.ellipse(0, 2, 40, 14, 0x17251b, 0.3);
    this.sprite = scene.add.image(
      0,
      -31,
      GAME_ATLAS_KEY,
      heroFrame(state.direction),
    );
    this.sprite.setOrigin(0.5, 0.5).setDisplaySize(76, 96);
    this.weaponSprite = scene.add
      .image(0, -27, GAME_ATLAS_KEY, weaponFrame(state.weapon))
      .setOrigin(0.5, 0.5);
    this.nameLabel = scene.add
      .text(0, -94, state.displayId, {
        fontFamily: '"Microsoft YaHei", sans-serif',
        fontSize: "12px",
        color: isLocal ? "#fff5c7" : "#ffffff",
        stroke: "#24372b",
        strokeThickness: 4,
      })
      .setOrigin(0.5, 0.5);
    this.roleLabel = scene.add
      .text(0, -80, state.roleName, {
        fontFamily: '"Microsoft YaHei", sans-serif',
        fontSize: "9px",
        color: "#dce8cf",
        stroke: "#24372b",
        strokeThickness: 3,
      })
      .setOrigin(0.5, 0.5);
    this.healthTrack = scene.add
      .rectangle(-18, -68, 36, 5, 0x233329, 0.9)
      .setOrigin(0, 0.5);
    this.healthFill = scene.add
      .rectangle(-17, -68, 34, 3, 0x78c267)
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
        .triangle(0, -107, 0, 0, 9, 0, 4.5, 7, 0xffe391)
        .setOrigin(0.5);
      this.container.add(marker);
    }
  }

  applyState(state: PublicPlayer): void {
    this.targetX = state.x;
    this.targetY = state.y;
    this.lastSnapshotAt = performance.now();
    this.velocityX = state.vx;
    this.velocityY = state.vy;
    this.direction = state.direction;
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

    this.localMoving = xAxis !== 0 || yAxis !== 0;
    if (!this.localMoving) {
      this.velocityX = 0;
      this.velocityY = 0;
      return;
    }

    this.velocityX = xAxis * PLAYER_SPEED;
    this.velocityY = yAxis * PLAYER_SPEED;
    if (Math.abs(xAxis) > Math.abs(yAxis)) {
      this.direction = xAxis > 0 ? "right" : "left";
    } else {
      this.direction = yAxis > 0 ? "down" : "up";
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

  update(deltaSeconds: number, time: number): void {
    const snapshotAgeSeconds = Math.min(
      (performance.now() - this.lastSnapshotAt) / 1_000,
      MAX_EXTRAPOLATION_SECONDS,
    );
    const projectedX = this.targetX + this.velocityX * snapshotAgeSeconds;
    const projectedY = this.targetY + this.velocityY * snapshotAgeSeconds;

    if (!this.isLocal || !this.localMoving) {
      const response = this.isLocal ? 18 : 13;
      const smoothing = 1 - Math.exp(-response * deltaSeconds);
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

    const moving =
      Math.abs(this.velocityX) > 0.1 || Math.abs(this.velocityY) > 0.1;
    this.sprite.y = -31 + (moving ? Math.sin(time / 85) * 1.5 : 0);
    this.sprite.setTexture(GAME_ATLAS_KEY, heroFrame(this.direction));
    this.updateWeaponSprite();
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

  private updateWeaponSprite(): void {
    this.weaponSprite.setTexture(GAME_ATLAS_KEY, weaponFrame(this.weapon));
    this.weaponSprite.setDisplaySize(this.weapon === "rocket" ? 82 : 64, 38);
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
    this.weaponSprite.setPosition(vector.x * 15, -28 + vector.y * 12);
  }
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
