import assert from "node:assert/strict";
import { access, readFile } from "node:fs/promises";

const root = "unity-client";
const required = [
  "Assets/Scenes/Game.unity",
  "Assets/Scripts/GameBootstrap.cs",
  "Assets/Scripts/CharacterModel.cs",
  "Assets/Scripts/GameWorldController.cs",
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
assert.match(world, /owner\.Muzzle\.position/);

const template = await readFile(`${root}/Assets/WebGLTemplates/EasyGame/index.html`, "utf8");
assert.match(template, /socket\.io\/socket\.io\.js/);
assert.match(template, /createUnityInstance/);
assert.match(template, /id="progress"/);

const server = await readFile("server/src/index.ts", "utf8");
assert.match(server, /Content-Encoding", "gzip/);
assert.match(server, /\.wasm\.unityweb/);
assert.match(server, /application\/wasm/);

process.stdout.write("Unity migration invariants passed: fixed character hierarchy, weapon muzzle sockets, Socket.IO bridge, optimistic input, collision-timed rocket effects, and a deployable WebGL release are present.\n");
