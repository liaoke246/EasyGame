import Phaser from "phaser";
import type {
  CharacterId,
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
  private readonly nameLabel: Phaser.GameObjects.Text;
  private readonly roleLabel: Phaser.GameObjects.Text;
  private readonly healthTrack: Phaser.GameObjects.Rectangle;
  private readonly healthFill: Phaser.GameObjects.Rectangle;
  private characterId: CharacterId;
  private color: string;
  private direction: Direction;
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
    private readonly scene: Phaser.Scene,
    state: PublicPlayer,
    private readonly isLocal: boolean,
  ) {
    this.characterId = state.characterId;
    this.color = state.color;
    this.direction = state.direction;
    this.targetX = state.x;
    this.targetY = state.y;

    ensureCharacterTextures(scene, state.characterId, state.color);

    this.shadow = scene.add.ellipse(0, 2, 29, 10, 0x24372b, 0.24);
    this.sprite = scene.add.image(
      0,
      -19,
      textureKey(state.characterId, state.direction),
    );
    this.sprite.setOrigin(0.5, 0.5);
    this.nameLabel = scene.add
      .text(0, -59, state.displayId, {
        fontFamily: '"Microsoft YaHei", sans-serif',
        fontSize: "12px",
        color: isLocal ? "#fff5c7" : "#ffffff",
        stroke: "#24372b",
        strokeThickness: 4,
      })
      .setOrigin(0.5, 0.5);
    this.roleLabel = scene.add
      .text(0, -45, state.roleName, {
        fontFamily: '"Microsoft YaHei", sans-serif',
        fontSize: "9px",
        color: "#dce8cf",
        stroke: "#24372b",
        strokeThickness: 3,
      })
      .setOrigin(0.5, 0.5);
    this.healthTrack = scene.add
      .rectangle(-18, -35, 36, 5, 0x233329, 0.9)
      .setOrigin(0, 0.5);
    this.healthFill = scene.add
      .rectangle(-17, -35, 34, 3, 0x78c267)
      .setOrigin(0, 0.5);

    this.container = scene.add.container(state.x, state.y, [
      this.shadow,
      this.sprite,
      this.healthTrack,
      this.healthFill,
      this.roleLabel,
      this.nameLabel,
    ]);

    if (isLocal) {
      const marker = scene.add
        .triangle(0, -72, 0, 0, 9, 0, 4.5, 7, 0xffe391)
        .setOrigin(0.5);
      this.container.add(marker);
    }
  }

  applyState(state: PublicPlayer): void {
    if (state.characterId !== this.characterId || state.color !== this.color) {
      this.characterId = state.characterId;
      this.color = state.color;
      ensureCharacterTextures(this.scene, state.characterId, state.color);
    }

    this.targetX = state.x;
    this.targetY = state.y;
    this.lastSnapshotAt = performance.now();
    this.velocityX = state.vx;
    this.velocityY = state.vy;
    this.direction = state.direction;
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
    this.sprite.y = -19 + (moving ? Math.sin(time / 85) * 1.5 : 0);
    this.sprite.setTexture(textureKey(this.characterId, this.direction));
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

function textureKey(
  characterId: CharacterId,
  direction: Direction,
): string {
  return `character-${characterId}-${direction}`;
}

function ensureCharacterTextures(
  scene: Phaser.Scene,
  characterId: CharacterId,
  color: string,
): void {
  const directions: Direction[] = ["up", "down", "left", "right"];
  for (const direction of directions) {
    const key = textureKey(characterId, direction);
    if (scene.textures.exists(key)) {
      continue;
    }

    const graphics = scene.make.graphics({ x: 0, y: 0 });
    const primary = Phaser.Display.Color.HexStringToColor(color).color;
    const dark = shadeColor(primary, 0.68);
    const skin = 0xf2c89f;
    const hair = characterId === "smith" ? 0x4b3a33 : 0x6b4731;

    graphics.fillStyle(0x39443e, 1);
    graphics.fillRect(7, 35, 7, 8);
    graphics.fillRect(19, 35, 7, 8);
    graphics.fillStyle(dark, 1);
    graphics.fillRect(6, 19, 21, 18);
    graphics.fillStyle(primary, 1);
    graphics.fillRect(8, 17, 17, 19);
    graphics.fillStyle(skin, 1);
    graphics.fillRect(9, 7, 15, 13);

    drawCharacterHeadwear(graphics, characterId, primary, dark, hair);
    drawFace(graphics, direction);

    graphics.fillStyle(skin, 1);
    if (direction === "left") {
      graphics.fillRect(4, 21, 5, 10);
    } else if (direction === "right") {
      graphics.fillRect(24, 21, 5, 10);
    } else {
      graphics.fillRect(4, 22, 4, 9);
      graphics.fillRect(25, 22, 4, 9);
    }

    graphics.generateTexture(key, 34, 46);
    graphics.destroy();
  }
}

function drawCharacterHeadwear(
  graphics: Phaser.GameObjects.Graphics,
  characterId: CharacterId,
  primary: number,
  dark: number,
  hair: number,
): void {
  switch (characterId) {
    case "farmer":
      graphics.fillStyle(0xe0b553, 1);
      graphics.fillRect(4, 5, 25, 5);
      graphics.fillRect(8, 1, 17, 7);
      graphics.fillStyle(0xb77d36, 1);
      graphics.fillRect(8, 6, 17, 2);
      break;
    case "ranger":
      graphics.fillStyle(dark, 1);
      graphics.fillRect(7, 3, 20, 7);
      graphics.fillTriangle(24, 3, 32, 6, 24, 9);
      graphics.fillStyle(primary, 1);
      graphics.fillRect(6, 8, 22, 3);
      break;
    case "herbalist":
      graphics.fillStyle(0xe9dfcf, 1);
      graphics.fillRect(7, 3, 20, 7);
      graphics.fillStyle(primary, 1);
      graphics.fillRect(5, 7, 24, 4);
      graphics.fillRect(21, 1, 4, 5);
      break;
    case "smith":
      graphics.fillStyle(hair, 1);
      graphics.fillRect(8, 3, 18, 7);
      graphics.fillRect(7, 7, 4, 9);
      graphics.fillRect(23, 7, 4, 9);
      graphics.fillStyle(0xbcc3c5, 1);
      graphics.fillRect(9, 1, 16, 3);
      break;
  }
}

function drawFace(
  graphics: Phaser.GameObjects.Graphics,
  direction: Direction,
): void {
  graphics.fillStyle(0x4d3b34, 1);
  if (direction === "down") {
    graphics.fillRect(12, 12, 2, 2);
    graphics.fillRect(19, 12, 2, 2);
  } else if (direction === "left") {
    graphics.fillRect(10, 12, 2, 2);
  } else if (direction === "right") {
    graphics.fillRect(21, 12, 2, 2);
  }
}

function shadeColor(color: number, factor: number): number {
  const source = Phaser.Display.Color.IntegerToColor(color);
  return Phaser.Display.Color.GetColor(
    Math.round(source.red * factor),
    Math.round(source.green * factor),
    Math.round(source.blue * factor),
  );
}
