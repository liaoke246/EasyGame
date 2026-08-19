import assert from "node:assert/strict";
import {
  SHOTGUN_RANGE,
  SMG_RANGE,
  fireWeapon,
} from "../server/dist/weapons.js";
import { ROCKET_RANGE } from "../server/dist/rockets.js";
import { zombieHitRadius } from "../server/dist/zombies.js";
import {
  PLAYER_RADIUS,
  WORLD_HEIGHT,
  WORLD_WIDTH,
  positionCollides,
} from "../server/dist/world.js";

assert.equal(WORLD_WIDTH, 3_840);
assert.equal(WORLD_HEIGHT, 2_160);
assert.equal(PLAYER_RADIUS, 22);
assert.equal(SMG_RANGE, 1_100);
assert.equal(SHOTGUN_RANGE, 520);
assert.equal(ROCKET_RANGE, 1_050);
assert.equal(zombieHitRadius("walker"), 34);
assert.equal(zombieHitRadius("runner"), 32);
assert.equal(zombieHitRadius("brute"), 41);

assert.equal(
  positionCollides(106, 100, 1),
  false,
  "A tree's visible crown edge must not behave like a solid trunk",
);
assert.equal(
  positionCollides(140, 130, 1),
  true,
  "The visible tree trunk must remain solid",
);

const blockedShooter = playerAt(300, 400);
const zombieBehindCabin = zombieAt("behind-cabin", 800, 400);
const blockedShot = fireWeapon(blockedShooter, [zombieBehindCabin], 1_000);
assert.ok(blockedShot);
assert.deepEqual(blockedShot.hitZombieIds, []);
assert.equal(blockedShot.traces.length, 1);
assert.equal(blockedShot.traces[0].hit, true);
assert.ok(blockedShot.traces[0].endX < 410, "The cabin must stop the bullet before the zombie");
assert.equal(zombieBehindCabin.health, zombieBehindCabin.maxHealth);

const clearShooter = playerAt(1_500, 1_100);
const visibleZombie = zombieAt("clear-target", 1_720, 1_123);
const clearShot = fireWeapon(clearShooter, [visibleZombie], 1_000);
assert.ok(clearShot);
assert.deepEqual(clearShot.hitZombieIds, [visibleZombie.id]);
assert.equal(clearShot.x, 1_543);
assert.equal(clearShot.y, 1_100);
assert.ok(visibleZombie.health < visibleZombie.maxHealth);

const forgivingShooter = playerAt(2_000, 900);
const grazingZombie = zombieAt("grazing-target", 2_200, 937);
const grazingShot = fireWeapon(forgivingShooter, [grazingZombie], 1_000);
assert.ok(grazingShot);
assert.deepEqual(
  grazingShot.hitZombieIds,
  [grazingZombie.id],
  "A shot through the visible edge of a smoothed zombie must register",
);

const missShooter = playerAt(2_000, 900);
const outsideZombie = zombieAt("outside-target", 2_200, 942);
const outsideShot = fireWeapon(missShooter, [outsideZombie], 1_000);
assert.ok(outsideShot);
assert.deepEqual(
  outsideShot.hitZombieIds,
  [],
  "Aim forgiveness must not turn a clearly outside shot into a hit",
);

const rangeShooter = playerAt(1_000, 1_250);
const distantZombie = zombieAt("distant-target", 1_950, 1_250);
const longShot = fireWeapon(rangeShooter, [distantZombie], 1_000);
assert.ok(longShot);
assert.deepEqual(
  longShot.hitZombieIds,
  [distantZombie.id],
  "The SMG must reach a distant visible target",
);

const analogShooter = playerAt(2_600, 1_050);
analogShooter.direction = "right";
analogShooter.aimX = Math.cos(Math.PI / 6);
analogShooter.aimY = Math.sin(Math.PI / 6);
const analogTarget = zombieAt(
  "analog-target",
  analogShooter.x + analogShooter.aimX * 320,
  analogShooter.y + analogShooter.aimY * 320,
);
const analogShot = fireWeapon(analogShooter, [analogTarget], 1_000);
assert.ok(analogShot);
assert.deepEqual(
  analogShot.hitZombieIds,
  [analogTarget.id],
  "Continuous 30-degree aim must not be snapped to a cardinal or diagonal ray",
);

process.stdout.write(
  "Combat geometry passed: forgiving visible-body hitboxes, longer weapon ranges, muzzle-aligned rays, and wall-blocked bullets.\n",
);

function playerAt(x, y) {
  return {
    id: `player-${x}-${y}`,
    guestToken: "test",
    displayId: "NOVA-27",
    characterId: "ranger",
    roleName: "Ranger",
    color: "#4c956c",
    spawnSkin: "default",
    x,
    y,
    vx: 0,
    vy: 0,
    direction: "right",
    aimX: 1,
    aimY: 0,
    health: 100,
    maxHealth: 100,
    attacking: false,
    kills: 0,
    respawning: false,
    weapon: "smg",
    firing: false,
    input: { up: false, down: false, left: false, right: false, direction: null },
    lastAttackAt: -1_000,
    attackEndsAt: 0,
    knockbackX: 0,
    knockbackY: 0,
    knockbackEndsAt: 0,
  };
}

function zombieAt(id, x, y) {
  return {
    id,
    kind: "walker",
    x,
    y,
    vx: 0,
    vy: 0,
    direction: "left",
    health: 70,
    maxHealth: 70,
    lastAttackAt: -1_000,
  };
}
