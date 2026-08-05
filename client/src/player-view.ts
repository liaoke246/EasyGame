import Phaser from "phaser";
import type {
  CharacterId,
  Direction,
  PublicPlayer,
} from "./types";

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
    this.velocityX = state.vx;
    this.velocityY = state.vy;
    this.direction = state.direction;
    this.health = state.health;
    this.maxHealth = state.maxHealth;
    this.respawning = state.respawning;
    this.nameLabel.setText(state.displayId);
    this.roleLabel.setText(state.roleName);
  }

  update(deltaSeconds: number, time: number): void {
    this.targetX += this.velocityX * deltaSeconds;
    this.targetY += this.velocityY * deltaSeconds;

    const smoothing = this.isLocal ? 0.32 : 0.2;
    this.container.x = Phaser.Math.Linear(
      this.container.x,
      this.targetX,
      smoothing,
    );
    this.container.y = Phaser.Math.Linear(
      this.container.y,
      this.targetY,
      smoothing,
    );
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
