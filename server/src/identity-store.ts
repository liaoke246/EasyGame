import { randomInt, randomUUID } from "node:crypto";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import type { PlayerIdentity } from "./protocol.js";
import { CHARACTER_OPTIONS } from "./world.js";

interface StoredIdentity extends PlayerIdentity {
  guestToken: string;
  createdAt: string;
}

const packageRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
);
const dataDirectory =
  process.env.DATA_DIR ?? path.join(packageRoot, "data");
const identityFile = path.join(dataDirectory, "players.json");
const identities = new Map<string, StoredIdentity>();
let persistenceQueue = Promise.resolve();

export async function initializeIdentityStore(): Promise<void> {
  if (process.env.DISABLE_PERSISTENCE === "1") {
    return;
  }

  await mkdir(dataDirectory, { recursive: true });

  try {
    const contents = await readFile(identityFile, "utf8");
    const stored = JSON.parse(contents) as StoredIdentity[];
    for (const identity of stored) {
      identities.set(identity.guestToken, identity);
    }
  } catch (error) {
    const code = (error as NodeJS.ErrnoException).code;
    if (code !== "ENOENT") {
      console.warn("Could not read the player identity store:", error);
    }
  }
}

export async function getOrCreateIdentity(
  proposedToken: unknown,
): Promise<StoredIdentity> {
  if (
    typeof proposedToken === "string" &&
    /^[0-9a-f-]{36}$/i.test(proposedToken)
  ) {
    const existing = identities.get(proposedToken);
    if (existing) {
      const character = CHARACTER_OPTIONS.find(
        (candidate) => candidate.characterId === existing.characterId,
      );
      const legacyDisplayId = /^(?:旅人|TRAVELER)-/i.test(existing.displayId);
      if (
        character &&
        (existing.roleName !== character.roleName ||
          existing.color !== character.color ||
          legacyDisplayId)
      ) {
        existing.roleName = character.roleName;
        existing.color = character.color;
        if (legacyDisplayId) {
          existing.displayId = createUniqueDisplayId();
        }
        await persistIdentities();
      }
      return existing;
    }
  }

  const guestToken = randomUUID();
  const character =
    CHARACTER_OPTIONS[randomInt(0, CHARACTER_OPTIONS.length)];
  const identity: StoredIdentity = {
    guestToken,
    displayId: createUniqueDisplayId(),
    characterId: character.characterId,
    roleName: character.roleName,
    color: character.color,
    createdAt: new Date().toISOString(),
  };

  identities.set(guestToken, identity);
  await persistIdentities();
  return identity;
}

function createUniqueDisplayId(): string {
  const existingIds = new Set(
    Array.from(identities.values(), (identity) => identity.displayId),
  );

  const callsigns = [
    "NOVA",
    "EMBER",
    "BOLT",
    "MOSS",
    "VIPER",
    "PIXEL",
    "ROOK",
    "ECHO",
    "COMET",
    "LUNAR",
    "RAVEN",
    "MAPLE",
  ];

  for (let attempt = 0; attempt < 100; attempt += 1) {
    const candidate = `${callsigns[randomInt(callsigns.length)]}-${randomInt(10, 100)}`;
    if (!existingIds.has(candidate)) {
      return candidate;
    }
  }

  return `SCOUT-${randomUUID().slice(0, 4).toUpperCase()}`;
}

async function persistIdentities(): Promise<void> {
  if (process.env.DISABLE_PERSISTENCE === "1") {
    return;
  }

  persistenceQueue = persistenceQueue.then(() =>
    writeFile(
      identityFile,
      `${JSON.stringify(Array.from(identities.values()), null, 2)}\n`,
      "utf8",
    ),
  );
  await persistenceQueue;
}
