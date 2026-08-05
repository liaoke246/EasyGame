import Phaser from "phaser";
import { GAME_ATLAS_KEY } from "./game-atlas";
import type { PublicRocket } from "./types";

const MAX_EXTRAPOLATION_SECONDS = 0.12;

export class RocketView {
  readonly image: Phaser.GameObjects.Image;
  private targetX: number;
  private targetY: number;
  private velocityX: number;
  private velocityY: number;
  private lastSnapshotAt = performance.now();
  private lastTrailAt = 0;

  constructor(
    private readonly scene: Phaser.Scene,
    state: PublicRocket,
  ) {
    this.targetX = state.x;
    this.targetY = state.y;
    this.velocityX = state.vx;
    this.velocityY = state.vy;
    this.image = scene.add
      .image(state.x, state.y - 14, GAME_ATLAS_KEY, "rocket-projectile")
      .setDisplaySize(42, 28)
      .setRotation(Math.atan2(state.vy, state.vx))
      .setDepth(Math.round(state.y + 230));
  }

  applyState(state: PublicRocket): void {
    this.targetX = state.x;
    this.targetY = state.y;
    this.velocityX = state.vx;
    this.velocityY = state.vy;
    this.lastSnapshotAt = performance.now();
    this.image.setRotation(Math.atan2(state.vy, state.vx));
  }

  update(deltaSeconds: number, time: number): void {
    const age = Math.min(
      (performance.now() - this.lastSnapshotAt) / 1_000,
      MAX_EXTRAPOLATION_SECONDS,
    );
    const smoothing = 1 - Math.exp(-18 * deltaSeconds);
    this.image.x = Phaser.Math.Linear(
      this.image.x,
      this.targetX + this.velocityX * age,
      smoothing,
    );
    this.image.y = Phaser.Math.Linear(
      this.image.y,
      this.targetY + this.velocityY * age - 14,
      smoothing,
    );
    this.image.setDepth(Math.round(this.image.y + 245));

    if (time - this.lastTrailAt > 34) {
      this.lastTrailAt = time;
      const angle = this.image.rotation + Math.PI;
      const flame = this.scene.add
        .circle(
          this.image.x + Math.cos(angle) * 16,
          this.image.y + Math.sin(angle) * 16,
          Phaser.Math.Between(2, 5),
          Math.random() > 0.45 ? 0xffbe55 : 0xe95b35,
          0.9,
        )
        .setBlendMode(Phaser.BlendModes.ADD)
        .setDepth(this.image.depth - 1);
      this.scene.tweens.add({
        targets: flame,
        x: flame.x + Math.cos(angle) * Phaser.Math.Between(8, 18),
        y: flame.y + Math.sin(angle) * Phaser.Math.Between(8, 18),
        alpha: 0,
        scale: 0.2,
        duration: 180,
        onComplete: () => flame.destroy(),
      });
    }
  }

  destroy(): void {
    this.image.destroy();
  }
}
