import cors from "cors";
import express from "express";
import { existsSync } from "node:fs";
import { createServer } from "node:http";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Server } from "socket.io";
import { getOrCreateIdentity, initializeIdentityStore } from "./identity-store.js";
import type {
  ClientToServerEvents,
  InputPayload,
  ServerToClientEvents,
  WorldSnapshot,
} from "./protocol.js";
import {
  OBSTACLES,
  RESPAWN_DELAY_MS,
  SNAPSHOT_RATE,
  TICK_RATE,
  WORLD_HEIGHT,
  WORLD_WIDTH,
  createPlayer,
  randomSpawn,
  toPublicPlayer,
  updatePlayerMovement,
  type PlayerState,
} from "./world.js";
import { fireWeapon, isWeaponId } from "./weapons.js";
import {
  createZombie,
  toPublicZombie,
  updateZombie,
  type ZombieState,
} from "./zombies.js";

const port = Number(process.env.PORT ?? 3001);
const host = process.env.HOST ?? "0.0.0.0";
const app = express();
const httpServer = createServer(app);
const io = new Server<ClientToServerEvents, ServerToClientEvents>(httpServer, {
  cors: {
    origin: process.env.CLIENT_ORIGIN?.split(",") ?? true,
    credentials: true,
  },
});
const players = new Map<string, PlayerState>();
const zombies = new Map<string, ZombieState>();

app.use(cors());
app.use(express.json());
app.get("/health", (_request, response) => {
  response.json({
    ok: true,
    players: players.size,
    zombies: zombies.size,
    uptimeSeconds: Math.round(process.uptime()),
  });
});

const packageRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
);
const clientDistribution = path.resolve(packageRoot, "../client/dist");

if (existsSync(clientDistribution)) {
  app.use(express.static(clientDistribution));
  app.get("*", (request, response, next) => {
    if (request.path.startsWith("/socket.io")) {
      next();
      return;
    }
    response.sendFile(path.join(clientDistribution, "index.html"));
  });
}

await initializeIdentityStore();

io.on("connection", async (socket) => {
  const identity = await getOrCreateIdentity(socket.handshake.auth.guestToken);
  const player = createPlayer(socket.id, identity.guestToken, identity);
  players.set(socket.id, player);

  socket.emit("welcome", {
    playerId: socket.id,
    guestToken: identity.guestToken,
    identity: {
      displayId: identity.displayId,
      characterId: identity.characterId,
      roleName: identity.roleName,
      color: identity.color,
    },
    world: {
      width: WORLD_WIDTH,
      height: WORLD_HEIGHT,
      obstacles: OBSTACLES,
    },
  });

  socket.emit("snapshot", createSnapshot());
  io.emit("notification", {
    kind: "join",
    text: `${identity.displayId} 来到了苔原谷`,
  });

  socket.on("input", (payload) => {
    applyInput(player, payload);
  });

  socket.on("network:ping", (payload) => {
    if (
      !Number.isSafeInteger(payload.sequence) ||
      !Number.isFinite(payload.clientSentAt)
    ) {
      return;
    }
    socket.emit("network:pong", {
      sequence: payload.sequence,
      clientSentAt: payload.clientSentAt,
      serverTime: Date.now(),
    });
  });

  socket.on("disconnect", () => {
    players.delete(socket.id);
    io.emit("notification", {
      kind: "leave",
      text: `${player.displayId} 离开了苔原谷`,
    });
  });
});

let tickCount = 0;
let previousTick = performance.now();
let nextZombieSpawnAt = 0;
let zombieSpawnIndex = 0;

setInterval(() => {
  const now = performance.now();
  const deltaSeconds = Math.min((now - previousTick) / 1_000, 0.1);
  previousTick = now;

  for (const player of players.values()) {
    player.attacking = now < player.attackEndsAt;
    updatePlayerMovement(player, deltaSeconds, now);
    if (player.firing) {
      performWeaponFire(player, now);
    }
  }

  spawnZombies(now);
  for (const zombie of zombies.values()) {
    const damagedPlayer = updateZombie(
      zombie,
      players.values(),
      deltaSeconds,
      now,
    );
    if (damagedPlayer?.health === 0 && !damagedPlayer.respawning) {
      defeatPlayerByZombie(damagedPlayer);
    }
  }

  tickCount += 1;
  if (tickCount % Math.max(1, Math.round(TICK_RATE / SNAPSHOT_RATE)) === 0) {
    io.emit("snapshot", createSnapshot());
  }
}, 1_000 / TICK_RATE);

httpServer.listen(port, host, () => {
  console.log(`EasyGame server listening on http://${host}:${port}`);
});

function applyInput(player: PlayerState, payload: InputPayload): void {
  player.input.up = payload.up === true;
  player.input.down = payload.down === true;
  player.input.left = payload.left === true;
  player.input.right = payload.right === true;
  player.firing = payload.fire === true || payload.attack === true;
  if (isWeaponId(payload.weapon)) {
    player.weapon = payload.weapon;
  }
}

function performWeaponFire(attacker: PlayerState, now: number): void {
  const event = fireWeapon(attacker, zombies.values(), now);
  if (!event) {
    return;
  }

  for (const zombieId of event.hitZombieIds) {
    const zombie = zombies.get(zombieId);
    if (zombie && zombie.health <= 0) {
      event.killedZombieIds.push(zombieId);
      zombies.delete(zombieId);
      attacker.kills += 1;
    }
  }
  io.emit("attack", event);

  if (event.killedZombieIds.length > 0) {
    io.emit("notification", {
      kind: "defeat",
      text: `${attacker.displayId} 清除了 ${event.killedZombieIds.length} 只僵尸`,
    });
  }
}

function defeatPlayerByZombie(victim: PlayerState): void {
  victim.respawning = true;
  victim.firing = false;
  victim.vx = 0;
  victim.vy = 0;
  victim.input = { up: false, down: false, left: false, right: false };

  io.emit("notification", {
    kind: "defeat",
    text: `${victim.displayId} 被僵尸包围了`,
  });

  setTimeout(() => {
    if (!players.has(victim.id)) {
      return;
    }
    const spawn = randomSpawn();
    victim.x = spawn.x;
    victim.y = spawn.y;
    victim.health = victim.maxHealth;
    victim.respawning = false;
    victim.knockbackEndsAt = 0;
  }, RESPAWN_DELAY_MS);
}

function spawnZombies(now: number): void {
  if (players.size === 0) {
    zombies.clear();
    return;
  }
  const targetCount =
    process.env.TEST_MODE === "1" ? 1 : Math.min(30, 6 + players.size * 4);
  if (zombies.size >= targetCount || now < nextZombieSpawnAt) {
    return;
  }
  const zombie = createZombie(zombieSpawnIndex);
  zombieSpawnIndex += 1;
  zombies.set(zombie.id, zombie);
  nextZombieSpawnAt = now + (process.env.TEST_MODE === "1" ? 50 : 900);
}

function createSnapshot(): WorldSnapshot {
  return {
    serverTime: Date.now(),
    players: Array.from(players.values(), toPublicPlayer),
    zombies: Array.from(zombies.values(), toPublicZombie),
  };
}
