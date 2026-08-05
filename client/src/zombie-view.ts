import Phaser from "phaser";
import { GAME_ATLAS_KEY, zombieFrame } from "./game-atlas";
import type { Direction, PublicZombie, ZombieKind } from "./types";

const MAX_EXTRAPOLATION_SECONDS = 0.16;

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
    const shadow = scene.add.ellipse(
      0,
      3,
      state.kind === "brute" ? 52 : 38,
      state.kind === "brute" ? 18 : 13,
      0x17251b,
      0.34,
    );
    this.sprite = scene.add
      .image(0, state.kind === "brute" ? -38 : -32, GAME_ATLAS_KEY, zombieFrame(state.kind))
      .setDisplaySize(
        state.kind === "brute" ? 94 : state.kind === "runner" ? 68 : 72,
        state.kind === "brute" ? 104 : 92,
      );
    const healthTrack = scene.add
      .rectangle(-19, state.kind === "brute" ? -84 : -75, 38, 5, 0x241d1d, 0.9)
      .setOrigin(0, 0.5);
    this.healthFill = scene.add
      .rectangle(-18, state.kind === "brute" ? -84 : -75, 36, 3, 0xc45d4b)
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
      this.sprite.setTexture(GAME_ATLAS_KEY, zombieFrame(state.kind));
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
    const smoothing = 1 - Math.exp(-16 * deltaSeconds);
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
    const stepWave = moving ? Math.sin(time / 88) : 0;
    this.sprite.y =
      (this.kind === "brute" ? -38 : -32) +
      stepWave * (this.kind === "runner" ? 2 : 1.25);
    this.sprite.x = stepWave * (this.kind === "brute" ? 0.7 : 1.25);
    this.sprite.setAngle(stepWave * (this.kind === "runner" ? 2.2 : 1.2));
    this.sprite.setTexture(GAME_ATLAS_KEY, zombieFrame(this.kind));
    this.sprite.setFlipX(this.direction === "left");
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
