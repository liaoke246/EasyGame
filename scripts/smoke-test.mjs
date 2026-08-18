import { spawn } from "node:child_process";
import process from "node:process";
import { io } from "socket.io-client";

const testPort = 3197;
const server = spawn(process.execPath, ["server/dist/index.js"], {
  cwd: process.cwd(),
  env: {
    ...process.env,
    PORT: String(testPort),
    DISABLE_PERSISTENCE: "1",
    TEST_MODE: "1",
  },
  stdio: ["ignore", "pipe", "pipe"],
});

const timeout = setTimeout(() => {
  finish(new Error("Smoke test timed out"));
}, 12_000);

let finished = false;

server.once("error", finish);
server.stderr.on("data", (chunk) => process.stderr.write(chunk));
server.stdout.on("data", async (chunk) => {
  const output = String(chunk);
  process.stdout.write(output);
  if (!output.includes("server listening")) {
    return;
  }

  try {
    await runMultiplayerCheck();
    finish();
  } catch (error) {
    finish(error);
  }
});

async function runMultiplayerCheck() {
  const url = `http://localhost:${testPort}`;
  const healthResponse = await fetch(`${url}/health`);
  const health = await healthResponse.json();
  const pageResponse = await fetch(url);
  const page = await pageResponse.text();
  if (!health.ok || pageResponse.status !== 200 || !page.includes("方块危机")) {
    throw new Error("Production HTTP entry point or health check failed");
  }

  const first = io(url, { transports: ["websocket"], forceNew: true });
  const second = io(url, { transports: ["websocket"], forceNew: true });

  try {
    const [firstWelcome, secondWelcome] = await Promise.all([
      onceEvent(first, "welcome"),
      onceEvent(second, "welcome"),
    ]);
    if (
      firstWelcome.world.width !== 3_840 ||
      firstWelcome.world.height !== 2_160
    ) {
      throw new Error("Expanded authoritative world dimensions were not synchronized");
    }
    for (const welcome of [firstWelcome, secondWelcome]) {
      if (!/^[A-Z]+-(?:[0-9]{2}|[A-F0-9]{4})$/.test(welcome.identity.displayId)) {
        throw new Error(`Random callsign was not WebGL-safe: ${welcome.identity.displayId}`);
      }
    }
    const probe = { sequence: 7, clientSentAt: Date.now() };
    const pongPromise = onceEvent(first, "network:pong");
    first.emit("network:ping", probe);
    const pong = await pongPromise;
    if (
      pong.sequence !== probe.sequence ||
      pong.clientSentAt !== probe.clientSentAt ||
      !Number.isFinite(pong.serverTime)
    ) {
      throw new Error("Network latency probe returned invalid data");
    }
    const twoPlayerSnapshot = await waitForSnapshot(
      first,
      (snapshot) => snapshot.players.length === 2 && snapshot.zombies.length > 0,
    );
    const initialPlayer = twoPlayerSnapshot.players.find(
      (player) => player.id === firstWelcome.playerId,
    );
    if (!initialPlayer) {
      throw new Error("First player was missing from the shared snapshot");
    }
    const otherPlayer = twoPlayerSnapshot.players.find(
      (player) => player.id !== firstWelcome.playerId,
    );
    if (!otherPlayer) {
      throw new Error("Second player was missing from the shared snapshot");
    }
    const synchronizedSkins = new Set(
      twoPlayerSnapshot.players.map((player) => player.spawnSkin),
    );
    if (!synchronizedSkins.has("usagi") || !synchronizedSkins.has("default")) {
      throw new Error("Spawn-randomized Usagi appearance was not synchronized");
    }
    const targetZombie = twoPlayerSnapshot.zombies[0];
    if (!targetZombie) {
      throw new Error("The test zombie was missing from the shared snapshot");
    }

    const zombieMotionSnapshot = await waitForSnapshot(first, (snapshot) => {
      const zombie = snapshot.zombies.find(
        (candidate) => candidate.id === targetZombie.id,
      );
      return Boolean(zombie && Math.hypot(zombie.x - targetZombie.x, zombie.y - targetZombie.y) > 2);
    });
    const movedZombie = zombieMotionSnapshot.zombies.find(
      (zombie) => zombie.id === targetZombie.id,
    );
    if (!movedZombie) {
      throw new Error("Moving zombie was missing from the snapshot");
    }
    const zombieVector = cardinalVector(movedZombie.direction);
    const zombieMovementX = movedZombie.x - targetZombie.x;
    const zombieMovementY = movedZombie.y - targetZombie.y;
    if (zombieMovementX * zombieVector.x + zombieMovementY * zombieVector.y <= 0) {
      throw new Error("Zombie animation direction opposed its authoritative movement");
    }

    const moveRight = otherPlayer.x > initialPlayer.x;
    first.emit("input", {
      up: false,
      down: false,
      left: !moveRight,
      right: moveRight,
    });
    const movedSnapshot = await waitForSnapshot(first, (snapshot) => {
      const player = snapshot.players.find(
        (candidate) => candidate.id === firstWelcome.playerId,
      );
      return Boolean(
        player &&
          Math.abs(player.x - initialPlayer.x) > 20 &&
          Math.abs(player.x - otherPlayer.x) < 55,
      );
    });
    const movedPlayer = movedSnapshot.players.find(
      (player) => player.id === firstWelcome.playerId,
    );
    if (!movedPlayer) {
      throw new Error("Moved player was missing from the snapshot");
    }

    first.emit("input", {
      up: false,
      down: false,
      left: false,
      right: false,
    });

    const attackEventPromise = onceEvent(first, "attack");
    first.emit("input", {
      up: false,
      down: false,
      left: false,
      right: false,
      fire: true,
      weapon: "smg",
    });
    const attackEvent = await attackEventPromise;
    if (
      attackEvent.attackerId !== firstWelcome.playerId ||
      attackEvent.weapon !== "smg" ||
      !attackEvent.hitZombieIds.includes(targetZombie.id)
    ) {
      throw new Error("Server weapon fire did not hit the nearby zombie");
    }
    first.emit("input", {
      up: false,
      down: false,
      left: false,
      right: false,
      fire: false,
      weapon: "smg",
    });
    await waitForSnapshot(first, (snapshot) => {
      const zombie = snapshot.zombies.find(
        (candidate) => candidate.id === targetZombie.id,
      );
      return !zombie || zombie.health < targetZombie.health;
    });

    await delay(650);
    const shotgunEventPromise = onceEvent(first, "attack");
    first.emit("input", {
      up: false,
      down: false,
      left: false,
      right: false,
      fire: true,
      weapon: "shotgun",
    });
    const shotgunEvent = await shotgunEventPromise;
    first.emit("input", {
      up: false,
      down: false,
      left: false,
      right: false,
      fire: false,
      weapon: "shotgun",
    });
    if (shotgunEvent.weapon !== "shotgun" || shotgunEvent.traces.length !== 7) {
      throw new Error("Shotgun did not produce seven authoritative pellets");
    }

    await delay(1_100);
    const rocketLaunchPromise = waitForEvent(
      first,
      "attack",
      (event) => event.weapon === "rocket" && event.phase === "fire",
    );
    const rocketImpactPromise = waitForEvent(
      first,
      "attack",
      (event) => event.weapon === "rocket" && event.phase === "impact",
    );
    first.emit("input", {
      up: false,
      down: false,
      left: false,
      right: false,
      fire: true,
      weapon: "rocket",
    });
    const rocketLaunch = await rocketLaunchPromise;
    const rocketLaunchedAt = performance.now();
    first.emit("input", {
      up: false,
      down: false,
      left: false,
      right: false,
      fire: false,
      weapon: "rocket",
    });
    if (
      rocketLaunch.hitZombieIds.length !== 0 ||
      rocketLaunch.traces.length !== 0
    ) {
      throw new Error("Rocket launch applied damage before projectile collision");
    }

    const rocketImpact = await rocketImpactPromise;
    if (
      rocketImpact.traces.length !== 1 ||
      performance.now() - rocketLaunchedAt < 25
    ) {
      throw new Error("Rocket collision did not produce an explosion event");
    }

    await delay(140);
    const diagonalFirePromise = waitForEvent(
      first,
      "attack",
      (event) => event.weapon === "smg" && event.phase === "fire",
    );
    first.emit("input", {
      up: true,
      down: false,
      left: false,
      right: true,
      fire: true,
      weapon: "smg",
    });
    const diagonalFire = await diagonalFirePromise;
    first.emit("input", {
      up: false,
      down: false,
      left: false,
      right: false,
      fire: false,
      weapon: "smg",
    });
    if (diagonalFire.direction !== "up-right") {
      throw new Error("Diagonal input did not produce a 45-degree shot");
    }

    process.stdout.write(
      `Smoke test passed: two players synchronized; movement ${initialPlayer.x.toFixed(
        1,
      )} → ${movedPlayer.x.toFixed(1)}; forward-only zombies, all three weapons, and 45-degree fire synchronized.\n`,
    );
  } finally {
    first.disconnect();
    second.disconnect();
  }
}

function cardinalVector(direction) {
  switch (direction) {
    case "up":
      return { x: 0, y: -1 };
    case "down":
      return { x: 0, y: 1 };
    case "left":
      return { x: -1, y: 0 };
    case "right":
      return { x: 1, y: 0 };
    default:
      throw new Error(`Unexpected zombie direction: ${direction}`);
  }
}

function delay(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

function onceEvent(socket, eventName) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(
      () => reject(new Error(`Timed out waiting for ${eventName}`)),
      4_000,
    );
    socket.once(eventName, (payload) => {
      clearTimeout(timer);
      resolve(payload);
    });
    socket.once("connect_error", (error) => {
      clearTimeout(timer);
      reject(error);
    });
  });
}

function waitForEvent(socket, eventName, predicate) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      socket.off(eventName, onEvent);
      reject(new Error(`Timed out waiting for matching ${eventName}`));
    }, 4_000);
    const onEvent = (payload) => {
      if (!predicate(payload)) {
        return;
      }
      clearTimeout(timer);
      socket.off(eventName, onEvent);
      resolve(payload);
    };
    socket.on(eventName, onEvent);
  });
}

function waitForSnapshot(socket, predicate, description = "matching snapshot") {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      socket.off("snapshot", onSnapshot);
      reject(new Error(`Timed out waiting for ${description}`));
    }, 5_000);
    const onSnapshot = (snapshot) => {
      if (!predicate(snapshot)) {
        return;
      }
      clearTimeout(timer);
      socket.off("snapshot", onSnapshot);
      resolve(snapshot);
    };
    socket.on("snapshot", onSnapshot);
  });
}

function finish(error) {
  if (finished) {
    return;
  }
  finished = true;
  clearTimeout(timeout);
  server.kill();
  if (error) {
    console.error(error);
    process.exitCode = 1;
  }
}
