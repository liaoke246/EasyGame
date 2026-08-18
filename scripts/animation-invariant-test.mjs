import assert from "node:assert/strict";
import { access, readFile } from "node:fs/promises";
import {
  DIRECTION_VECTORS,
  PLAYER_WEAPON_PIVOT_Y,
  WEAPON_COOLDOWN_MS,
  WEAPON_MUZZLE_DISTANCES,
  cardinalDirectionFromVector,
  directionAngleDegrees,
  directionFromAxes,
  weaponMuzzleOffset,
  weaponBallisticMuzzleOffset,
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
assert.ok(walkPose.walkPhase >= 0 && walkPose.walkPhase < 1);
assert.equal(aimPose.frame, walkPose.frame, "Aiming must preserve gait frame");
assert.equal(aimPose.walkPhase, walkPose.walkPhase, "Aiming must preserve walk phase");
assert.equal(recoilPose.frame, walkPose.frame, "Firing must preserve gait frame");
assert.equal(
  recoilPose.walkPhase,
  walkPose.walkPhase,
  "Firing must preserve walk phase",
);

const expectedAngles = {
  up: -90,
  "up-right": -45,
  right: 0,
  "down-right": 45,
  down: 90,
  "down-left": 135,
  left: 180,
  "up-left": -135,
};

for (const direction of directions) {
  assert.equal(directionAngleDegrees(direction), expectedAngles[direction]);
}

for (const weapon of ["smg", "shotgun", "rocket"]) {
  for (const direction of directions) {
    const offset = weaponMuzzleOffset(weapon, direction);
    const facing = DIRECTION_VECTORS[direction];
    const barrel = {
      x: offset.x,
      y: offset.y - PLAYER_WEAPON_PIVOT_Y,
    };
    const forward = barrel.x * facing.x + barrel.y * facing.y;
    const cross = barrel.x * facing.y - barrel.y * facing.x;
    assert.ok(Number.isFinite(offset.x) && Number.isFinite(offset.y));
    assert.ok(
      Math.abs(forward - WEAPON_MUZZLE_DISTANCES[weapon]) < 1e-9,
      `${weapon} ${direction} muzzle distance must match the rendered barrel`,
    );
    assert.ok(
      Math.abs(cross) < 1e-9,
      `${weapon} ${direction} barrel and projectile must share one direction`,
    );

    const ballistic = weaponBallisticMuzzleOffset(weapon, direction);
    assert.ok(
      Math.abs(ballistic.x * facing.y - ballistic.y * facing.x) < 1e-9,
      `${weapon} ${direction} authoritative ray must remain on the barrel axis`,
    );
  }
}

const blockModelSource = await readFile("client/src/block-character.ts", "utf8");
assert.match(blockModelSource, /WEAPON_MUZZLE_DISTANCES/);
assert.match(blockModelSource, /weaponMuzzleOffset/);
assert.match(blockModelSource, /readonly weaponLayer/);
assert.match(
  blockModelSource,
  /Phaser\.GameObjects\.Polygon/,
  "Characters must retain separate pseudo-3D top and side planes",
);
assert.match(
  blockModelSource,
  /moveTo\(this\.weaponLayer, 1\)/,
  "Upward aiming must move the weapon behind the body",
);
assert.match(
  blockModelSource,
  /bringToTop\(this\.weaponLayer\)/,
  "Forward aiming must move the weapon in front of the body",
);

const playerViewSource = await readFile("client/src/player-view.ts", "utf8");
assert.match(playerViewSource, /BlockCharacterModel/);
assert.match(playerViewSource, /this\.model\.getMuzzleLocal\(\)/);
assert.doesNotMatch(
  playerViewSource,
  /bodySprite|weaponSprite|HERO_|WEAPON_OVERLAY/,
  "Player rendering must remain one procedural model with one weapon layer",
);

const zombieViewSource = await readFile("client/src/zombie-view.ts", "utf8");
assert.match(zombieViewSource, /BlockCharacterModel/);
assert.doesNotMatch(zombieViewSource, /ZOMBIE_WALK|targets:\s*this\.container[\s\S]{0,160}scale[XY]/);

const worldSceneSource = await readFile("client/src/world-scene.ts", "utf8");
assert.match(
  worldSceneSource,
  /weaponMuzzleOffset\(event\.weapon, event\.direction\)/,
  "Network attack effects must use the shared barrel-tip transform",
);
assert.doesNotMatch(worldSceneSource, /game-atlas|\.load\.image|\.load\.spritesheet/);
assert.match(worldSceneSource, /drawGroundPatches/);
assert.match(worldSceneSource, /drawArenaBarrel/);
assert.match(worldSceneSource, /fillPoints/);

const serverWeaponSource = await readFile("server/src/weapons.ts", "utf8");
assert.match(
  serverWeaponSource,
  /weaponBallisticMuzzleOffset\(weapon, attacker\.direction\)/,
  "Authoritative hit tests must use the shared barrel-tip transform",
);

await assert.rejects(
  access("client/src/game-atlas.ts"),
  undefined,
  "The obsolete raster atlas registry must stay removed",
);

process.stdout.write(
  "Animation invariants passed: procedural block characters, continuous gait, eight-way aim, and shared barrel-tip geometry.\n",
);
