import assert from "node:assert/strict";
import { readdir, readFile } from "node:fs/promises";
import {
  DIRECTION_VECTORS,
  WEAPON_COOLDOWN_MS,
  WEAPON_MUZZLE_OFFSETS,
  cardinalDirectionFromVector,
  directionFromAxes,
} from "../shared/dist/index.js";
import { PlayerAnimationController } from "../client/src/player-animation.ts";

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
assert.equal(cardinalDirectionFromVector(1, 0.95, "right"), "right");
assert.equal(cardinalDirectionFromVector(1, 0.95, "down"), "down");
assert.equal(cardinalDirectionFromVector(1.3, 0.9, "down"), "right");
assert.deepEqual(WEAPON_COOLDOWN_MS, { smg: 95, shotgun: 620, rocket: 1_050 });

const playerAnimation = new PlayerAnimationController();
playerAnimation.advanceMovement(50, true);
const walkPose = playerAnimation.sample(1_000, "right", "right", true);
playerAnimation.setTriggerHeld(true, 1_000);
const aimPose = playerAnimation.sample(1_045, "right", "right", true);
playerAnimation.fire(9, 190, 1_050);
const recoilPose = playerAnimation.sample(1_060, "right", "right", true);
assert.equal(walkPose.frame, 4);
assert.equal(aimPose.frame, walkPose.frame, "Aiming must preserve the gait phase");
assert.equal(
  recoilPose.frame,
  walkPose.frame,
  "Firing must preserve the gait phase",
);

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
