import Phaser from "phaser";
import { PROJECTILE_VISUAL_ELEVATION } from "@easygame/shared";
import type { PublicRocket } from "./types";

const MAX_EXTRAPOLATION_SECONDS = 0.16;

export class RocketView {
  readonly container: Phaser.GameObjects.Container;
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
    const tail = scene.add
      .rectangle(-15, 0, 10, 10, 0x4f5a4e)
      .setStrokeStyle(2, 0x181a1b);
    const body = scene.add
      .rectangle(0, 0, 30, 11, 0x6d7864)
      .setStrokeStyle(2, 0x181a1b);
    const band = scene.add.rectangle(4, 0, 5, 14, 0xb34335);
    const nose = scene.add
      .triangle(19, 0, 0, -7, 0, 7, 12, 0, 0xc9b46b)
      .setStrokeStyle(2, 0x181a1b);
    this.container = scene.add
      .container(state.x, state.y - PROJECTILE_VISUAL_ELEVATION, [
        tail,
        body,
        band,
        nose,
      ])
      .setRotation(Math.atan2(state.vy, state.vx))
      .setDepth(Math.round(state.y + 230));
  }

  applyState(state: PublicRocket): void {
    this.targetX = state.x;
    this.targetY = state.y;
    this.velocityX = state.vx;
    this.velocityY = state.vy;
    this.lastSnapshotAt = performance.now();
    this.container.setRotation(Math.atan2(state.vy, state.vx));
  }

  update(deltaSeconds: number, time: number): void {
    const age = Math.min(
      (performance.now() - this.lastSnapshotAt) / 1_000,
      MAX_EXTRAPOLATION_SECONDS,
    );
    const smoothing = 1 - Math.exp(-22 * deltaSeconds);
    this.container.x = Phaser.Math.Linear(
      this.container.x,
      this.targetX + this.velocityX * age,
      smoothing,
    );
    this.container.y = Phaser.Math.Linear(
      this.container.y,
      this.targetY + this.velocityY * age - PROJECTILE_VISUAL_ELEVATION,
      smoothing,
    );
    this.container.setDepth(Math.round(this.container.y + 245));

    if (time - this.lastTrailAt > 34) {
      this.lastTrailAt = time;
      const angle = this.container.rotation + Math.PI;
      const flame = this.scene.add
        .rectangle(
          this.container.x + Math.cos(angle) * 17,
          this.container.y + Math.sin(angle) * 17,
          Phaser.Math.Between(4, 9),
          Phaser.Math.Between(3, 5),
          Math.random() > 0.45 ? 0xffbe55 : 0xe95b35,
          0.9,
        )
        .setRotation(this.container.rotation)
        .setBlendMode(Phaser.BlendModes.ADD)
        .setDepth(this.container.depth - 1);
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
    this.container.destroy(true);
  }
}
