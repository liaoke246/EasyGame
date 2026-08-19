import assert from "node:assert/strict";
import { access, readFile } from "node:fs/promises";

const root = "unity-client";
const required = [
  "Assets/Scenes/Game.unity",
  "Assets/Scripts/GameBootstrap.cs",
  "Assets/Scripts/CharacterModel.cs",
  "Assets/Scripts/GameWorldController.cs",
  "Assets/Scripts/WorldHealthBar.cs",
  "Assets/Scripts/WebSocketBridge.cs",
  "Assets/Plugins/WebGL/EasyGameSocket.jslib",
  "Assets/WebGLTemplates/EasyGame/index.html",
  "Assets/Editor/WebBuild.cs",
  "Packages/manifest.json",
  "ProjectSettings/ProjectVersion.txt",
  "PrebuiltWebGL/index.html",
  "PrebuiltWebGL/Build/unity-webgl.data.unityweb",
  "PrebuiltWebGL/Build/unity-webgl.framework.js.unityweb",
  "PrebuiltWebGL/Build/unity-webgl.loader.js",
  "PrebuiltWebGL/Build/unity-webgl.wasm.unityweb",
];

await Promise.all(required.map(path => access(`${root}/${path}`)));

const model = await readFile(`${root}/Assets/Scripts/CharacterModel.cs`, "utf8");
assert.match(model, /leftArm = BuildArm/);
assert.match(model, /rightArm = BuildArm/);
assert.match(model, /RightHandSocket/);
assert.match(model, /AddMuzzle\(root/);
assert.match(model, /RequestWeapon/);
assert.match(model, /TriggerFire/);
assert.match(model, /BuildUsagiBody/);
assert.match(model, /WhiteTail/);
assert.doesNotMatch(model, /Resources\.Load|SpriteRenderer/);

const bridge = await readFile(`${root}/Assets/Plugins/WebGL/EasyGameSocket.jslib`, "utf8");
for (const eventName of ["welcome", "snapshot", "attack", "notification", "network:pong"]) {
  assert.match(bridge, new RegExp(`socket\\.on\\(\\"${eventName.replace(":", "\\:")}\\"`));
}
assert.match(bridge, /easygame\.guestToken/);
assert.match(bridge, /transports: \["websocket", "polling"\]/);

const world = await readFile(`${root}/Assets/Scripts/GameWorldController.cs`, "utf8");
assert.match(world, /SimulateLocal/);
assert.match(world, /OptimisticFire/);
assert.match(world, /attack\.phase == "impact" && attack\.weapon == "rocket"/);
assert.match(world, /attacker\.Muzzle\.position/);
assert.match(world, /rocket\.Initialize\(state, map\.ServerHeight\)/);
assert.match(world, /OnMobileInput/);
assert.match(world, /mobileMovement/);
assert.match(world, /mobileAim/);
assert.match(world, /mobileFire/);
assert.match(world, /TryGetPointerAim/);
assert.match(world, /aimX = lastAim\.x/);

assert.match(model, /AimRotation/);
assert.match(model, /Quaternion\.LookRotation\(continuousAim\.normalized/);

const healthBar = await readFile(`${root}/Assets/Scripts/WorldHealthBar.cs`, "utf8");
assert.match(healthBar, /Character Name/);
assert.match(healthBar, /SetValue/);

const effects = await readFile(`${root}/Assets/Scripts/Effects.cs`, "utf8");
assert.match(effects, /TracerFx/);
assert.match(effects, /line\.SetPosition\(0, origin\)/);
assert.match(effects, /line\.SetPosition\(1, destination\)/);
assert.doesNotMatch(effects, /Vector3\.Lerp\(origin, destination/);

const rocketVisual = await readFile(`${root}/Assets/Scripts/RocketVisual.cs`, "utf8");
assert.match(rocketVisual, /lateralError/);
assert.match(rocketVisual, /alongError/);
assert.doesNotMatch(rocketVisual, /Vector3\.Lerp\(transform\.position, targetPosition/);

const template = await readFile(`${root}/Assets/WebGLTemplates/EasyGame/index.html`, "utf8");
assert.match(template, /socket\.io\/socket\.io\.js/);
assert.match(template, /createUnityInstance/);
assert.match(template, /id="progress"/);
assert.match(template, /id="mobile-controls"/);
assert.match(template, /data-mobile-weapon="rocket"/);
assert.match(template, /OnMobileInput/);
assert.match(template, /pointerdown/);

const prebuiltPage = await readFile(`${root}/PrebuiltWebGL/index.html`, "utf8");
for (const mobileMarker of [
  'id="move-stick"',
  'id="aim-stick"',
  'data-mobile-weapon="rocket"',
  '"OnMobileInput"',
]) {
  assert.match(prebuiltPage, new RegExp(mobileMarker));
}

const server = await readFile("server/src/index.ts", "utf8");
assert.match(server, /Content-Encoding", "gzip/);
assert.match(server, /\.wasm\.unityweb/);
assert.match(server, /application\/wasm/);

process.stdout.write("Unity migration invariants passed: fixed character hierarchy, weapon muzzle sockets, Socket.IO bridge, optimistic input, collision-timed rocket effects, and a deployable WebGL release are present.\n");
