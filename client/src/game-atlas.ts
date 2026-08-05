import Phaser from "phaser";
import type { Direction, WeaponId, ZombieKind } from "./types";

export const GAME_ATLAS_KEY = "easygame-atlas-v2";
export const ENVIRONMENT_ATLAS_KEY = "easygame-environment-v2";
export const HERO_WALK_ATLAS_KEY = "easygame-hero-walk-v2";
export const TERRAIN_GRASS_KEY = "terrain-grass-v2";
export const TERRAIN_DIRT_KEY = "terrain-dirt-v2";
export const TERRAIN_WILD_KEY = "terrain-wild-v2";
export const TERRAIN_SOIL_KEY = "terrain-soil-v2";

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

const ENVIRONMENT_FRAMES: Record<string, [number, number, number, number]> = {
  "ground-grass": [18, 18, 290, 290],
  "ground-dirt": [327, 18, 291, 290],
  "ground-wild": [636, 18, 291, 290],
  "ground-soil": [946, 18, 290, 290],
  "prop-oak": [18, 315, 292, 315],
  "prop-pine": [327, 315, 291, 315],
  "prop-rock": [636, 315, 291, 315],
  "prop-stump": [946, 315, 290, 315],
  "prop-cabin": [18, 625, 292, 335],
  "prop-cabin-side": [327, 625, 291, 335],
  "prop-garden": [636, 625, 291, 335],
  "prop-pond": [946, 625, 290, 335],
  "prop-fence": [18, 950, 292, 286],
  "prop-crates": [327, 950, 291, 286],
  "prop-flowers": [636, 950, 291, 286],
  "prop-lantern": [946, 950, 290, 286],
};

export function preloadGameAtlas(scene: Phaser.Scene): void {
  scene.load.image(GAME_ATLAS_KEY, "/assets/easygame-atlas-v2.webp");
  scene.load.image(
    ENVIRONMENT_ATLAS_KEY,
    "/assets/easygame-environment-v2.webp",
  );
  scene.load.image(
    HERO_WALK_ATLAS_KEY,
    "/assets/easygame-hero-walk-v2.webp",
  );
  scene.load.image(TERRAIN_GRASS_KEY, "/assets/terrain-grass-v2.webp");
  scene.load.image(TERRAIN_DIRT_KEY, "/assets/terrain-dirt-v2.webp");
  scene.load.image(TERRAIN_WILD_KEY, "/assets/terrain-wild-v2.webp");
  scene.load.image(TERRAIN_SOIL_KEY, "/assets/terrain-soil-v2.webp");
}

export function registerGameAtlasFrames(scene: Phaser.Scene): void {
  const texture = scene.textures.get(GAME_ATLAS_KEY);
  for (const [name, [x, y, width, height]] of Object.entries(FRAMES)) {
    if (!texture.has(name)) {
      texture.add(name, 0, x, y, width, height);
    }
  }

  const environment = scene.textures.get(ENVIRONMENT_ATLAS_KEY);
  for (const [name, [x, y, width, height]] of Object.entries(
    ENVIRONMENT_FRAMES,
  )) {
    if (!environment.has(name)) {
      environment.add(name, 0, x, y, width, height);
    }
  }

  const walk = scene.textures.get(HERO_WALK_ATLAS_KEY);
  const cellSize = 128;
  const directions: Direction[] = ["down", "up", "right", "left"];
  directions.forEach((direction, row) => {
    for (let frame = 0; frame < 4; frame += 1) {
      const name = heroWalkFrame(direction, frame);
      if (!walk.has(name)) {
        walk.add(
          name,
          0,
          frame * cellSize,
          row * cellSize,
          cellSize,
          cellSize,
        );
      }
    }
  });
}

export function heroFrame(direction: Direction): string {
  return `hero-${direction}`;
}

export function heroWalkFrame(direction: Direction, frame: number): string {
  return `hero-walk-${direction}-${frame % 4}`;
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
