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
  if (!health.ok || pageResponse.status !== 200 || !page.includes("苔原谷")) {
    throw new Error("Production HTTP entry point or health check failed");
  }

  const first = io(url, { transports: ["websocket"], forceNew: true });
  const second = io(url, { transports: ["websocket"], forceNew: true });

  try {
    const [firstWelcome] = await Promise.all([
      onceEvent(first, "welcome"),
      onceEvent(second, "welcome"),
    ]);
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
      (snapshot) => snapshot.players.length === 2,
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
      attack: true,
    });
    const attackEvent = await attackEventPromise;
    if (
      attackEvent.attackerId !== firstWelcome.playerId ||
      !attackEvent.hitPlayerIds.includes(otherPlayer.id)
    ) {
      throw new Error("Server attack did not hit the nearby second player");
    }
    await waitForSnapshot(first, (snapshot) => {
      const player = snapshot.players.find(
        (candidate) => candidate.id === otherPlayer.id,
      );
      return Boolean(player && player.health === 75);
    });

    process.stdout.write(
      `Smoke test passed: two players synchronized; movement ${initialPlayer.x.toFixed(
        1,
      )} → ${movedPlayer.x.toFixed(1)}; latency probe and attack damage synchronized.\n`,
    );
  } finally {
    first.disconnect();
    second.disconnect();
  }
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

function waitForSnapshot(socket, predicate) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      socket.off("snapshot", onSnapshot);
      reject(new Error("Timed out waiting for matching snapshot"));
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
