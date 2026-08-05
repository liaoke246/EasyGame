import assert from "node:assert/strict";
import { readdir, readFile } from "node:fs/promises";
import {
  DIRECTION_VECTORS,
  PLAYER_SPRITE_OFFSET_Y,
  WEAPON_COOLDOWN_MS,
  cardinalDirectionFromVector,
  directionFromAxes,
  weaponMuzzleOffset,
  weaponOverlayAngleDegrees,
  weaponVisualDirection,
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
assert.equal(walkPose.frame, 2);
assert.equal(aimPose.frame, walkPose.frame, "Aiming must preserve the gait phase");
assert.equal(
  recoilPose.frame,
  walkPose.frame,
  "Firing must preserve the gait phase",
);

for (const weapon of ["smg", "shotgun", "rocket"]) {
  for (const direction of directions) {
    const offset = weaponMuzzleOffset(weapon, direction);
    const facing = DIRECTION_VECTORS[direction];
    const barrelFromOverlayCenter = {
      x: offset.x,
      y: offset.y - PLAYER_SPRITE_OFFSET_Y,
    };
    assert.ok(Number.isFinite(offset.x) && Number.isFinite(offset.y));
    assert.ok(
      barrelFromOverlayCenter.x * facing.x +
        barrelFromOverlayCenter.y * facing.y >
        7,
      `${weapon} ${direction} muzzle must stay beyond the gun center`,
    );
  }
}

assert.equal(weaponVisualDirection("up-right"), "right");
assert.equal(weaponVisualDirection("down-left"), "left");
assert.equal(weaponOverlayAngleDegrees("up-right"), -45);
assert.equal(weaponOverlayAngleDegrees("down-right"), 45);
assert.equal(weaponOverlayAngleDegrees("up-left"), 45);
assert.equal(weaponOverlayAngleDegrees("down-left"), -45);

const atlasSource = await readFile("client/src/game-atlas.ts", "utf8");
assert.doesNotMatch(
  atlasSource,
  /HERO_WEAPON_WALK|heroWeaponWalkFrame|hero-.*-fire/,
  "Weapons must not be baked into complete character models",
);
assert.match(atlasSource, /HERO_WALK_ATLAS_KEY/);
assert.match(atlasSource, /WEAPON_OVERLAY_ATLAS_KEY/);
assert.match(
  atlasSource,
  /easygame-hero-body-armless-v1/,
  "The body layer must not contain a second pair of arms",
);

const playerViewSource = await readFile("client/src/player-view.ts", "utf8");
assert.doesNotMatch(
  playerViewSource,
  /HERO_WEAPON|heroWeapon|targets:\s*this\.container,[\s\S]{0,160}scale[XY]/,
  "Player actions must preserve one fixed-size character model",
);
assert.match(
  playerViewSource,
  /weaponMuzzleOffset\(this\.weapon, this\.direction\)/,
  "Predicted fire must use the shared barrel-tip transform",
);
assert.doesNotMatch(
  playerViewSource,
  /targets:\s*this\.bodySprite/,
  "Weapon switching and recoil must never replace or tween the complete body",
);

const worldSceneSource = await readFile("client/src/world-scene.ts", "utf8");
assert.match(
  worldSceneSource,
  /weaponMuzzleOffset\(event\.weapon, event\.direction\)/,
  "Network attack effects must use the shared barrel-tip transform",
);
const serverWeaponSource = await readFile("server/src/weapons.ts", "utf8");
assert.match(
  serverWeaponSource,
  /weaponMuzzleOffset\(weapon, attacker\.direction\)/,
  "Authoritative hit tests must use the shared barrel-tip transform",
);

const zombieViewSource = await readFile("client/src/zombie-view.ts", "utf8");
assert.doesNotMatch(
  zombieViewSource,
  /targets:\s*this\.container,[\s\S]{0,160}scale[XY]/,
  "Zombie hit feedback must not resize the character model",
);

const runtimeAssets = await readdir("client/public/assets");
assert.ok(runtimeAssets.includes("easygame-hero-body-armless-v1.webp"));
assert.ok(runtimeAssets.includes("easygame-weapon-overlay-v2.webp"));
assert.equal(runtimeAssets.includes("easygame-hero-walk-v3.webp"), false);
assert.equal(
  runtimeAssets.some((name) => /hero-(smg|shotgun|rocket)-walk/.test(name)),
  false,
  "Baked weapon/body walk sheets must stay out of the runtime bundle",
);

process.stdout.write(
  "Animation invariants passed: stable body model, layered weapons, eight-way aim transforms, and shared barrel-tip geometry.\n",
);
