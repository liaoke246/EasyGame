import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { sourceFingerprint, verifyRelease } from './verify-side-scroller-release.mjs';

const digest = value => createHash('sha256').update(value).digest('hex');

async function writeFixtureFile(root, relative, contents) {
  const destination = path.join(root, relative);
  await mkdir(path.dirname(destination), { recursive: true });
  await writeFile(destination, contents);
}

async function fixture(t) {
  const temporaryParent = path.resolve(os.tmpdir());
  const root = await mkdtemp(path.join(temporaryParent, 'easygame-release-test-'));
  t.after(async () => {
    // Cleanup is confined to the unique directory created by this fixture.
    assert.equal(path.dirname(root), temporaryParent);
    assert.ok(path.basename(root).startsWith('easygame-release-test-'));
    await rm(root, { recursive: true, force: true });
  });

  const sources = {
    'ProjectSettings/ProjectVersion.txt': 'm_EditorVersion: 6000.0.1f1\n',
    'Packages/manifest.json': '{"dependencies":{}}\n',
    'Packages/packages-lock.json': '{"dependencies":{}}\n',
    'Assets/Mirror/version.txt': 'fixture-mirror\n',
    'Assets/Game/Resources/Config/PlayerMovement.asset': 'moveSpeed: 5.6\n',
    'Assets/Game/Resources/Config/LevelProgression.asset': 'maxLevel: 20\n',
    'Assets/Game/Scripts/Player/Movement.cs': 'public class Movement {}\n',
    'Assets/Game/Editor/Build.cs': 'public class Build {}\n',
    'Assets/WebGLTemplates/SideScroller/index.html': '<html>Ready</html>\n',
    'Assets/Plugins/WebGL/SkillHud.jslib': 'mergeInto(LibraryManager.library, {});\n',
    'Assets/Game/Resources/Effects/CombatSprite.shader': 'Shader "Fixture" {}\n',
  };
  for (const [relative, contents] of Object.entries(sources))
    await writeFixtureFile(root, relative, contents);

  const fingerprint = await sourceFingerprint(root);
  const builds = [
    ['PrebuiltWebGL', 'WebGL', {
      'index.html': '<html>Playable client</html>\n',
      'Build/game.wasm': Buffer.from([0, 97, 115, 109, 1]),
    }],
    ['PrebuiltServerLinux', 'StandaloneLinux64', {
      'DeadRailsServer.x86_64': Buffer.from([127, 69, 76, 70, 1]),
      'DeadRailsServer_Data/Managed/Game.dll': Buffer.from([77, 90, 1, 2, 3]),
    }],
  ];
  for (const [folder, target, artifacts] of builds) {
    for (const [relative, contents] of Object.entries(artifacts))
      await writeFixtureFile(root, `${folder}/${relative}`, contents);
    const manifest = {
      schemaVersion: 1,
      target,
      sourceFingerprint: fingerprint,
      version: '0.4.0',
      unityVersion: '6000.0.1f1',
      builtAtUtc: '2026-09-05T00:00:00Z',
      files: Object.entries(artifacts).map(([relative, contents]) => ({ path: relative, sha256: digest(contents) })),
    };
    await writeFixtureFile(root, `${folder}/release-manifest.json`, JSON.stringify(manifest));
  }
  return { root, fingerprint };
}

test('matching client and server artifacts verify against their source snapshot', async t => {
  const { root, fingerprint } = await fixture(t);
  assert.deepEqual(await verifyRelease(root), { version: '0.4.0', sourceFingerprint: fingerprint });
});

test('a source edit invalidates previously built client and server artifacts', async t => {
  const { root } = await fixture(t);
  await writeFixtureFile(root, 'Assets/Game/Scripts/Player/Movement.cs', 'public class Movement { public float Speed = 8; }\n');
  await assert.rejects(verifyRelease(root), /stale build; rebuild Unity from current sources/);
});

test('a WebGL native bridge edit also invalidates the release fingerprint', async t => {
  const { root } = await fixture(t);
  await writeFixtureFile(root, 'Assets/Plugins/WebGL/SkillHud.jslib', 'mergeInto(LibraryManager.library, { changed: function() {} });\n');
  await assert.rejects(verifyRelease(root), /stale build; rebuild Unity from current sources/);
});

test('a combat shader edit invalidates the release fingerprint', async t => {
  const { root } = await fixture(t);
  await writeFixtureFile(root, 'Assets/Game/Resources/Effects/CombatSprite.shader', 'Shader "Changed" {}\n');
  await assert.rejects(verifyRelease(root), /stale build; rebuild Unity from current sources/);
});

for (const [folder, artifact] of [
  ['PrebuiltWebGL', 'Build/game.wasm'],
  ['PrebuiltServerLinux', 'DeadRailsServer.x86_64'],
]) {
  test(`${folder}: changed binary content is rejected`, async t => {
    const { root } = await fixture(t);
    const filename = path.join(root, folder, artifact);
    const bytes = await readFile(filename);
    bytes[bytes.length - 1] ^= 0xff;
    await writeFile(filename, bytes);
    await assert.rejects(verifyRelease(root), /artifact changed after build/);
  });

  test(`${folder}: an undeclared artifact is rejected`, async t => {
    const { root } = await fixture(t);
    await writeFixtureFile(root, `${folder}/unexpected.bin`, 'extra artifact');
    await assert.rejects(verifyRelease(root), /missing, duplicate or unexpected artifacts/);
  });

  test(`${folder}: a missing artifact is rejected`, async t => {
    const { root } = await fixture(t);
    await rm(path.join(root, folder, artifact));
    await assert.rejects(verifyRelease(root), /missing, duplicate or unexpected artifacts/);
  });
}

test('different client and server release versions are rejected', async t => {
  const { root } = await fixture(t);
  const filename = path.join(root, 'PrebuiltServerLinux/release-manifest.json');
  const manifest = JSON.parse(await readFile(filename, 'utf8'));
  manifest.version = '0.5.0';
  await writeFile(filename, JSON.stringify(manifest));
  await assert.rejects(verifyRelease(root), /Client and server versions must match/);
});

test('source text keeps its fingerprint across Git line-ending and UTF-8 BOM conversions', async t => {
  const { root, fingerprint } = await fixture(t);
  await writeFixtureFile(root, 'Assets/Game/Scripts/Player/Movement.cs', '\uFEFFpublic class Movement {}\r\n');
  assert.equal(await sourceFingerprint(root), fingerprint);
  assert.equal((await verifyRelease(root)).version, '0.4.0');
});
