import Phaser from "phaser";
import type { Direction, PublicZombie, ZombieKind } from "./types";

const MAX_EXTRAPOLATION_SECONDS = 0.12;

export class ZombieView {
  readonly container: Phaser.GameObjects.Container;
  private readonly sprite: Phaser.GameObjects.Image;
  private readonly healthFill: Phaser.GameObjects.Rectangle;
  private kind: ZombieKind;
  private direction: Direction;
  private targetX: number;
  private targetY: number;
  private velocityX = 0;
  private velocityY = 0;
  private health: number;
  private maxHealth: number;
  private lastSnapshotAt = performance.now();

  constructor(
    private readonly scene: Phaser.Scene,
    state: PublicZombie,
  ) {
    this.kind = state.kind;
    this.direction = state.direction;
    this.targetX = state.x;
    this.targetY = state.y;
    this.health = state.health;
    this.maxHealth = state.maxHealth;
    ensureZombieTextures(scene, state.kind);

    const shadow = scene.add.ellipse(0, 3, 31, 11, 0x17251b, 0.3);
    this.sprite = scene.add.image(0, -19, zombieTexture(state.kind, state.direction));
    const healthTrack = scene.add
      .rectangle(-19, -48, 38, 5, 0x241d1d, 0.9)
      .setOrigin(0, 0.5);
    this.healthFill = scene.add
      .rectangle(-18, -48, 36, 3, 0xc45d4b)
      .setOrigin(0, 0.5);
    this.container = scene.add.container(state.x, state.y, [
      shadow,
      this.sprite,
      healthTrack,
      this.healthFill,
    ]);
  }

  applyState(state: PublicZombie): void {
    if (state.kind !== this.kind) {
      this.kind = state.kind;
      ensureZombieTextures(this.scene, state.kind);
    }
    this.direction = state.direction;
    this.targetX = state.x;
    this.targetY = state.y;
    this.velocityX = state.vx;
    this.velocityY = state.vy;
    this.health = state.health;
    this.maxHealth = state.maxHealth;
    this.lastSnapshotAt = performance.now();
  }

  update(deltaSeconds: number, time: number): void {
    const age = Math.min(
      (performance.now() - this.lastSnapshotAt) / 1_000,
      MAX_EXTRAPOLATION_SECONDS,
    );
    const smoothing = 1 - Math.exp(-12 * deltaSeconds);
    this.container.x = Phaser.Math.Linear(
      this.container.x,
      this.targetX + this.velocityX * age,
      smoothing,
    );
    this.container.y = Phaser.Math.Linear(
      this.container.y,
      this.targetY + this.velocityY * age,
      smoothing,
    );
    this.container.setDepth(Math.round(this.container.y));
    const moving = Math.hypot(this.velocityX, this.velocityY) > 1;
    this.sprite.y = -19 + (moving ? Math.sin(time / 105) * 1.2 : 0);
    this.sprite.setTexture(zombieTexture(this.kind, this.direction));
    this.healthFill.width =
      36 * Phaser.Math.Clamp(this.health / Math.max(1, this.maxHealth), 0, 1);
  }

  showHit(killed: boolean): void {
    this.sprite.setTintFill(0xf8f1d4);
    this.scene.time.delayedCall(65, () => this.sprite.clearTint());
    spawnZombieParticles(this.scene, this.container.x, this.container.y - 18, killed);
    this.scene.tweens.add({
      targets: this.container,
      scaleX: killed ? 1.16 : 1.06,
      scaleY: killed ? 0.82 : 0.94,
      yoyo: true,
      duration: killed ? 110 : 65,
      ease: "Quad.easeOut",
    });
  }

  destroy(): void {
    this.container.destroy(true);
  }
}

function zombieTexture(kind: ZombieKind, direction: Direction): string {
  return `zombie-${kind}-${direction}`;
}

function ensureZombieTextures(scene: Phaser.Scene, kind: ZombieKind): void {
  for (const direction of ["up", "down", "left", "right"] as Direction[]) {
    const key = zombieTexture(kind, direction);
    if (scene.textures.exists(key)) {
      continue;
    }
    const graphics = scene.make.graphics({ x: 0, y: 0 });
    const palette =
      kind === "runner"
        ? { skin: 0x8faf70, shirt: 0x7c4f3e, dark: 0x3a4936 }
        : kind === "brute"
          ? { skin: 0x6f8d58, shirt: 0x514c65, dark: 0x2f3a2c }
          : { skin: 0x839c65, shirt: 0x67504a, dark: 0x344231 };
    const wide = kind === "brute";
    graphics.fillStyle(0x2c3330, 1);
    graphics.fillRect(wide ? 5 : 8, 35, 8, 9);
    graphics.fillRect(wide ? 23 : 21, 35, 8, 9);
    graphics.fillStyle(palette.shirt, 1);
    graphics.fillRect(wide ? 4 : 7, 20, wide ? 28 : 22, 18);
    graphics.fillStyle(palette.skin, 1);
    graphics.fillRect(wide ? 8 : 10, 6, wide ? 20 : 16, 16);
    graphics.fillStyle(palette.dark, 1);
    graphics.fillRect(wide ? 7 : 9, 4, wide ? 22 : 18, 6);
    graphics.fillStyle(0xc9d08b, 1);
    if (direction === "down") {
      graphics.fillRect(12, 13, 3, 3);
      graphics.fillRect(wide ? 22 : 20, 13, 3, 3);
    } else if (direction === "left") {
      graphics.fillRect(10, 13, 3, 3);
    } else if (direction === "right") {
      graphics.fillRect(wide ? 24 : 21, 13, 3, 3);
    }
    graphics.fillStyle(palette.skin, 1);
    graphics.fillRect(1, 22, 6, 15);
    graphics.fillRect(wide ? 30 : 29, 22, 6, 15);
    graphics.generateTexture(key, 36, 48);
    graphics.destroy();
  }
}

function spawnZombieParticles(
  scene: Phaser.Scene,
  x: number,
  y: number,
  killed: boolean,
): void {
  const colors = [0x9fbd73, 0x5d7849, 0xb74e45, 0xe4c46b];
  const count = killed ? 14 : 6;
  for (let index = 0; index < count; index += 1) {
    const angle = (Math.PI * 2 * index) / count + Math.random() * 0.35;
    const distance = killed ? Phaser.Math.Between(26, 62) : Phaser.Math.Between(12, 28);
    const particle = scene.add
      .rectangle(x, y, Phaser.Math.Between(2, 5), Phaser.Math.Between(2, 5), colors[index % colors.length], 0.95)
      .setDepth(Math.round(y + 180));
    scene.tweens.add({
      targets: particle,
      x: x + Math.cos(angle) * distance,
      y: y + Math.sin(angle) * distance + Phaser.Math.Between(-10, 8),
      alpha: 0,
      scale: 0.25,
      duration: Phaser.Math.Between(220, 430),
      ease: "Quad.easeOut",
      onComplete: () => particle.destroy(),
    });
  }
}
