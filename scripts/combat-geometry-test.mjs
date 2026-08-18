import assert from "node:assert/strict";
import { fireWeapon } from "../server/dist/weapons.js";
import {
  PLAYER_RADIUS,
  WORLD_HEIGHT,
  WORLD_WIDTH,
  positionCollides,
} from "../server/dist/world.js";

assert.equal(WORLD_WIDTH, 3_840);
assert.equal(WORLD_HEIGHT, 2_160);
assert.equal(PLAYER_RADIUS, 22);

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

process.stdout.write(
  "Combat geometry passed: expanded world, inset scenery collision, visible character radii, muzzle-aligned rays, and wall-blocked bullets.\n",
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
    health: 100,
    maxHealth: 100,
    attacking: false,
    kills: 0,
    respawning: false,
    weapon: "smg",
    firing: false,
    input: { up: false, down: false, left: false, right: false },
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
