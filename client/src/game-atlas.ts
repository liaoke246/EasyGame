import Phaser from "phaser";
import type { Direction, WeaponId, ZombieKind } from "./types";

export const GAME_ATLAS_KEY = "easygame-atlas-v1";

const FRAMES: Record<string, [number, number, number, number]> = {
  "hero-down": [70, 55, 240, 300],
  "hero-up": [350, 55, 245, 300],
  "hero-right": [635, 55, 245, 300],
  "hero-left": [940, 55, 250, 300],
  "zombie-walker": [65, 390, 245, 315],
  "zombie-runner": [355, 390, 265, 315],
  "zombie-brute": [645, 380, 320, 335],
  "rocket-projectile": [995, 475, 205, 155],
  "weapon-smg": [60, 710, 260, 180],
  "weapon-shotgun": [345, 710, 310, 180],
  "weapon-rocket": [645, 700, 430, 190],
  "explosion-0": [40, 930, 145, 235],
  "explosion-1": [175, 925, 205, 245],
  "explosion-2": [365, 915, 220, 255],
  "explosion-3": [570, 910, 230, 265],
  "explosion-4": [785, 910, 245, 265],
  "explosion-5": [1010, 910, 235, 265],
};

export function preloadGameAtlas(scene: Phaser.Scene): void {
  scene.load.image(GAME_ATLAS_KEY, "/assets/easygame-atlas-v1.png");
}

export function registerGameAtlasFrames(scene: Phaser.Scene): void {
  const texture = scene.textures.get(GAME_ATLAS_KEY);
  for (const [name, [x, y, width, height]] of Object.entries(FRAMES)) {
    if (!texture.has(name)) {
      texture.add(name, 0, x, y, width, height);
    }
  }
}

export function heroFrame(direction: Direction): string {
  return `hero-${direction}`;
}

export function zombieFrame(kind: ZombieKind): string {
  return `zombie-${kind}`;
}

export function weaponFrame(weapon: WeaponId): string {
  return `weapon-${weapon}`;
}

export function explosionFrame(index: number): string {
  return `explosion-${Phaser.Math.Clamp(index, 0, 5)}`;
}
