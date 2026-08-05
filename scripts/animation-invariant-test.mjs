import assert from "node:assert/strict";
import { readdir, readFile } from "node:fs/promises";
import {
  DIRECTION_VECTORS,
  WEAPON_MUZZLE_OFFSETS,
  directionFromAxes,
} from "../shared/dist/index.js";

const directions = [
  "up",
  "up-right",
  "right",
  "down-right",
  "down",
  "down-left",
  "left",
  "up-left",
];

for (const direction of directions) {
  const vector = DIRECTION_VECTORS[direction];
  assert.ok(vector, `Missing vector for ${direction}`);
  assert.ok(
    Math.abs(Math.hypot(vector.x, vector.y) - 1) < 1e-9,
    `${direction} vector must stay normalized`,
  );
}

assert.equal(directionFromAxes(1, -1), "up-right");
assert.equal(directionFromAxes(-1, 1), "down-left");

for (const [weapon, offsets] of Object.entries(WEAPON_MUZZLE_OFFSETS)) {
  assert.deepEqual(
    Object.keys(offsets).sort(),
    [...directions].sort(),
    `${weapon} must define one shared muzzle anchor per direction`,
  );
}

const atlasSource = await readFile("client/src/game-atlas.ts", "utf8");
assert.doesNotMatch(
  atlasSource,
  /hero-.*-fire|HERO_WEAPON_FIRE/,
  "Shooting must not swap the complete character model",
);

const playerViewSource = await readFile("client/src/player-view.ts", "utf8");
assert.doesNotMatch(
  playerViewSource,
  /HERO_WEAPON_FIRE|heroWeaponFireFrame|targets:\s*this\.container,[\s\S]{0,160}scale[XY]/,
  "Player actions must preserve one fixed-size character model",
);

const zombieViewSource = await readFile("client/src/zombie-view.ts", "utf8");
assert.doesNotMatch(
  zombieViewSource,
  /targets:\s*this\.container,[\s\S]{0,160}scale[XY]/,
  "Zombie hit feedback must not resize the character model",
);

const runtimeAssets = await readdir("client/public/assets");
assert.equal(
  runtimeAssets.some((name) => /hero-.*-fire|weapon-overlay|hero-walk-v[23]/.test(name)),
  false,
  "Legacy model-swapping assets must stay out of the runtime bundle",
);

process.stdout.write(
  "Animation invariants passed: fixed-size models, normalized eight-way vectors, and shared muzzle anchors.\n",
);
