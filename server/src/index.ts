import cors from "cors";
import express from "express";
import { existsSync } from "node:fs";
import { createServer } from "node:http";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Server } from "socket.io";
import { getOrCreateIdentity, initializeIdentityStore } from "./identity-store.js";
import type {
  AttackEvent,
  ClientToServerEvents,
  InputPayload,
  ServerToClientEvents,
  WorldSnapshot,
} from "./protocol.js";
import {
  ATTACK_COOLDOWN_MS,
  ATTACK_DURATION_MS,
  OBSTACLES,
  RESPAWN_DELAY_MS,
  SNAPSHOT_RATE,
  TICK_RATE,
  WORLD_HEIGHT,
  WORLD_WIDTH,
  canHit,
  createPlayer,
  directionVector,
  randomSpawn,
  toPublicPlayer,
  updatePlayerMovement,
  type PlayerState,
} from "./world.js";

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

app.use(cors());
app.use(express.json());
app.get("/health", (_request, response) => {
  response.json({
    ok: true,
    players: players.size,
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
    if (payload.attack) {
      performAttack(player);
    }
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

setInterval(() => {
  const now = performance.now();
  const deltaSeconds = Math.min((now - previousTick) / 1_000, 0.1);
  previousTick = now;

  for (const player of players.values()) {
    player.attacking = now < player.attackEndsAt;
    updatePlayerMovement(player, deltaSeconds, now);
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
}

function performAttack(attacker: PlayerState): void {
  const now = performance.now();
  if (
    attacker.respawning ||
    attacker.health <= 0 ||
    now - attacker.lastAttackAt < ATTACK_COOLDOWN_MS
  ) {
    return;
  }

  attacker.lastAttackAt = now;
  attacker.attackEndsAt = now + ATTACK_DURATION_MS;
  attacker.attacking = true;

  const hitPlayers: PlayerState[] = [];
  const facing = directionVector(attacker.direction);

  for (const victim of players.values()) {
    if (!canHit(attacker, victim)) {
      continue;
    }

    hitPlayers.push(victim);
    victim.health = Math.max(0, victim.health - 25);
    victim.knockbackX = facing.x * 280;
    victim.knockbackY = facing.y * 280;
    victim.knockbackEndsAt = now + 160;

    if (victim.health === 0) {
      defeatPlayer(attacker, victim);
    }
  }

  const event: AttackEvent = {
    attackerId: attacker.id,
    direction: attacker.direction,
    x: attacker.x,
    y: attacker.y,
    hitPlayerIds: hitPlayers.map((player) => player.id),
  };
  io.emit("attack", event);

  if (hitPlayers.length > 0) {
    io.emit("notification", {
      kind: "hit",
      text: `${attacker.displayId} 命中了 ${hitPlayers
        .map((player) => player.displayId)
        .join("、")}`,
    });
  }
}

function defeatPlayer(attacker: PlayerState, victim: PlayerState): void {
  victim.respawning = true;
  victim.vx = 0;
  victim.vy = 0;
  victim.input = { up: false, down: false, left: false, right: false };
  attacker.kills += 1;

  io.emit("notification", {
    kind: "defeat",
    text: `${attacker.displayId} 击倒了 ${victim.displayId}`,
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

function createSnapshot(): WorldSnapshot {
  return {
    serverTime: Date.now(),
    players: Array.from(players.values(), toPublicPlayer),
  };
}
