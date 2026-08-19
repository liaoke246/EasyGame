import assert from "node:assert/strict";
import { access, readFile } from "node:fs/promises";

const root = "unity-client";
const required = [
  "Assets/Scenes/Game.unity",
  "Assets/Scripts/GameBootstrap.cs",
  "Assets/Scripts/CharacterModel.cs",
  "Assets/Scripts/TopDownArt.cs",
  "Assets/Scripts/GameWorldController.cs",
  "Assets/Scripts/WorldHealthBar.cs",
  "Assets/Scripts/WebSocketBridge.cs",
  "Assets/Plugins/WebGL/EasyGameSocket.jslib",
  "Assets/WebGLTemplates/EasyGame/index.html",
  "Assets/Editor/WebBuild.cs",
  "Packages/manifest.json",
  "ProjectSettings/ProjectVersion.txt",
  "Assets/Resources/Art/KenneyTopdown/License.txt",
  "Assets/Resources/Art/KenneyTopdown/Characters/Survivor1/survivor1_machine.png",
  "Assets/Resources/Art/KenneyTopdown/Characters/Zombie1/zoimbie1_hold.png",
  "Assets/Resources/Art/KenneyTopdown/Tiles/tile_01.png",
  "PrebuiltWebGL/index.html",
  "PrebuiltWebGL/Build/unity-webgl.data.unityweb",
  "PrebuiltWebGL/Build/unity-webgl.framework.js.unityweb",
  "PrebuiltWebGL/Build/unity-webgl.loader.js",
  "PrebuiltWebGL/Build/unity-webgl.wasm.unityweb",
];

await Promise.all(required.map(path => access(`${root}/${path}`)));

const model = await readFile(`${root}/Assets/Scripts/CharacterModel.cs`, "utf8");
assert.match(model, /Unified Character Sprite/);
assert.match(model, /TopDownArt\.LoadSprite/);
assert.match(model, /Complete2DCharacter/);
assert.match(model, /VisualFactory\.Empty\(transform, "Muzzle"/);
assert.match(model, /RequestWeapon/);
assert.match(model, /TriggerFire/);
assert.match(model, /spawnSkin == "usagi"/);
assert.match(model, /SpriteRenderer/);
assert.doesNotMatch(model, /BuildArm|BuildLeg|RightHandSocket/);

const topDownArt = await readFile(`${root}/Assets/Scripts/TopDownArt.cs`, "utf8");
assert.match(topDownArt, /Resources\.Load<Texture2D>/);
assert.match(topDownArt, /Sprite\.Create/);
assert.match(topDownArt, /CreateTiledPlane/);

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

process.stdout.write("Unity invariants passed: unified 2D sprites, muzzle-aligned weapons, top-down licensed art, Socket.IO bridge, optimistic input, collision-timed rocket effects, and a deployable WebGL release are present.\n");
