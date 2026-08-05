import Phaser from "phaser";
import { BlockCharacterModel } from "./block-character";
import type { CardinalDirection, PublicZombie, ZombieKind } from "./types";

const MAX_EXTRAPOLATION_SECONDS = 0.16;

export class ZombieView {
  readonly container: Phaser.GameObjects.Container;
  private readonly model: BlockCharacterModel;
  private readonly healthFill: Phaser.GameObjects.Rectangle;
  private kind: ZombieKind;
  private direction: CardinalDirection;
  private targetX: number;
  private targetY: number;
  private velocityX = 0;
  private velocityY = 0;
  private health: number;
  private maxHealth: number;
  private lastSnapshotAt = performance.now();
  private previousRenderX: number;
  private previousRenderY: number;
  private walkDistance = 0;

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
    this.previousRenderX = state.x;
    this.previousRenderY = state.y;
    this.model = new BlockCharacterModel(scene, { kind: state.kind });
    this.model.setFacing(state.direction);
    const healthTrack = scene.add
      .rectangle(
        -19,
        state.kind === "brute" ? -105 : -84,
        38,
        5,
        0x241d1d,
        0.9,
      )
      .setOrigin(0, 0.5);
    this.healthFill = scene.add
      .rectangle(
        -18,
        state.kind === "brute" ? -105 : -84,
        36,
        3,
        0xc45d4b,
      )
      .setOrigin(0, 0.5);
    this.container = scene.add.container(state.x, state.y, [
      this.model.container,
      healthTrack,
      this.healthFill,
    ]);
  }

  applyState(state: PublicZombie): void {
    if (state.kind !== this.kind) {
      this.kind = state.kind;
    }
    this.targetX = state.x;
    this.targetY = state.y;
    this.velocityX = state.vx;
    this.velocityY = state.vy;
    this.direction = state.direction;
    this.health = state.health;
    this.maxHealth = state.maxHealth;
    this.lastSnapshotAt = performance.now();
  }

  update(deltaSeconds: number, _time: number): void {
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
    const distanceMoved = Phaser.Math.Distance.Between(
      this.previousRenderX,
      this.previousRenderY,
      this.container.x,
      this.container.y,
    );
    this.previousRenderX = this.container.x;
    this.previousRenderY = this.container.y;
    const moving =
      distanceMoved > 0.02 && Math.hypot(this.velocityX, this.velocityY) > 0.5;
    if (moving) {
      this.walkDistance += distanceMoved;
    }
    const cyclePixels = this.kind === "runner" ? 48 : this.kind === "brute" ? 70 : 60;
    const walkPhase = (this.walkDistance % cyclePixels) / cyclePixels;
    this.model.setFacing(this.direction);
    this.model.setMotion(walkPhase, moving, 0);
    this.healthFill.width =
      36 * Phaser.Math.Clamp(this.health / Math.max(1, this.maxHealth), 0, 1);
  }

  showHit(killed: boolean): void {
    this.model.flashDamage();
    spawnBloodStain(this.scene, this.container.x, this.container.y, killed);
    spawnZombieParticles(this.scene, this.container.x, this.container.y - 18, killed);
    spawnImpactRing(this.scene, this.container.x, this.container.y - 30, killed);
  }

  destroy(): void {
    this.container.destroy(true);
  }

}

function spawnBloodStain(
  scene: Phaser.Scene,
  x: number,
  y: number,
  killed: boolean,
): void {
  const stain = scene.add.graphics().setDepth(Math.round(y - 6));
  stain.fillStyle(0xc91f25, killed ? 0.72 : 0.48);
  stain.fillEllipse(x, y, killed ? 30 : 16, killed ? 13 : 7);
  const drops = killed ? 11 : 5;
  for (let index = 0; index < drops; index += 1) {
    const angle = index * 2.17 + Math.random() * 0.32;
    const distance = Phaser.Math.Between(killed ? 13 : 7, killed ? 40 : 20);
    stain.fillCircle(
      x + Math.cos(angle) * distance,
      y + Math.sin(angle) * distance * 0.58,
      Phaser.Math.Between(1, killed ? 4 : 3),
    );
  }
  scene.tweens.add({
    targets: stain,
    alpha: 0.2,
    duration: 18_000,
    delay: 7_000,
    onComplete: () => stain.destroy(),
  });
}

function spawnImpactRing(
  scene: Phaser.Scene,
  x: number,
  y: number,
  killed: boolean,
): void {
  const ring = scene.add
    .ellipse(x, y, killed ? 42 : 28, killed ? 22 : 15, 0xffffff, 0)
    .setStrokeStyle(killed ? 3 : 2, killed ? 0xf0d67b : 0xe9eee2, 0.8)
    .setDepth(Math.round(y + 180));
  ring.setScale(0.45);
  scene.tweens.add({
    targets: ring,
    scaleX: killed ? 1.65 : 1.25,
    scaleY: killed ? 1.65 : 1.25,
    alpha: 0,
    duration: killed ? 220 : 130,
    ease: "Cubic.easeOut",
    onComplete: () => ring.destroy(),
  });
}

function spawnZombieParticles(
  scene: Phaser.Scene,
  x: number,
  y: number,
  killed: boolean,
): void {
  const colors = [0xd62529, 0x9e151c, 0xe2483c, 0x642126];
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
