import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import { readFile, readdir } from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const hash = data => createHash('sha256').update(data).digest('hex');
const sourceFolders = ['Assets/Game/Scripts', 'Assets/Game/Editor', 'Assets/WebGLTemplates/SideScroller'];
const sourceFiles = ['ProjectSettings/ProjectVersion.txt', 'Packages/manifest.json', 'Packages/packages-lock.json',
  'Assets/Mirror/version.txt', 'Assets/Game/Resources/Config/PlayerMovement.asset', 'Assets/Game/Resources/Config/LevelProgression.asset'];

async function filesUnder(root, prefix = '') {
  const files = [];
  for (const entry of await readdir(path.join(root, prefix), { withFileTypes: true })) {
    const relative = prefix ? `${prefix}/${entry.name}` : entry.name;
    if (entry.isDirectory()) files.push(...await filesUnder(root, relative));
    else if (entry.isFile()) files.push(relative);
    else throw new Error(`Unsupported release entry: ${relative}`);
  }
  return files.sort();
}

export async function sourceFingerprint(root) {
  const inputs = [...sourceFiles];
  for (const folder of sourceFolders)
    inputs.push(...(await filesUnder(root, folder)).filter(file => /\.(cs|html|js)$/.test(file)));
  let listing = '';
  for (const file of inputs.sort()) {
    // Match C# ReadAllText (BOM removal) and normalize Git's Windows line endings.
    const text = (await readFile(path.join(root, file), 'utf8')).replace(/^\uFEFF/, '').replace(/\r\n/g, '\n');
    listing += `${file}\n${hash(text)}\n`;
  }
  return hash(listing);
}

async function fileHash(file) {
  const digest = createHash('sha256');
  for await (const chunk of createReadStream(file)) digest.update(chunk);
  return digest.digest('hex');
}

export async function verifyRelease(root = path.resolve('unity-side-scroller')) {
  const fingerprint = await sourceFingerprint(root);
  let releaseVersion;
  for (const [folder, target] of [['PrebuiltWebGL', 'WebGL'], ['PrebuiltServerLinux', 'StandaloneLinux64']]) {
    const output = path.join(root, folder);
    const manifest = JSON.parse(await readFile(path.join(output, 'release-manifest.json'), 'utf8'));
    assert.equal(manifest.schemaVersion, 1, `${folder}: unsupported manifest`);
    assert.equal(manifest.target, target, `${folder}: wrong target`);
    assert.equal(manifest.sourceFingerprint, fingerprint, `${folder}: stale build; rebuild Unity from current sources`);
    assert.ok(manifest.version && manifest.unityVersion && manifest.builtAtUtc, `${folder}: incomplete build metadata`);
    releaseVersion ??= manifest.version;
    assert.equal(manifest.version, releaseVersion, 'Client and server versions must match');
    const expected = manifest.files.map(file => file.path).sort();
    assert.ok(expected.length > 0, `${folder}: empty build`);
    const actual = (await filesUnder(output)).filter(file => file !== 'release-manifest.json');
    assert.deepEqual(actual, expected, `${folder}: missing, duplicate or unexpected artifacts`);
    for (const file of manifest.files) {
      assert.ok(!path.isAbsolute(file.path) && !file.path.split('/').includes('..'), 'Unsafe artifact path');
      assert.equal(await fileHash(path.join(output, file.path)), file.sha256, `${folder}/${file.path}: artifact changed after build`);
    }
  }
  return { version: releaseVersion, sourceFingerprint: fingerprint };
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  console.log('Verified matching Unity client/server source and artifact hashes:', await verifyRelease());
}
