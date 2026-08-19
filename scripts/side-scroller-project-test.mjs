import assert from "node:assert/strict";
import { access, readFile } from "node:fs/promises";

const root = "unity-side-scroller";
const required = [
  "Assets/Game/Scenes/SideScroller.unity",
  "Assets/Game/Scripts/Core/SideScrollerBootstrap.cs",
  "Assets/Game/Scripts/Player/PlayerInputReader.cs",
  "Assets/Game/Scripts/Player/PlayerMovement.cs",
  "Assets/Game/Scripts/Player/PlayerAnimation.cs",
  "Assets/Game/Scripts/Combat/PlayerCombat.cs",
  "Assets/Game/Scripts/World/SideWorldBuilder.cs",
  "Assets/Game/Scripts/World/SideCameraRig.cs",
  "Assets/Game/Scripts/Data/PlayerMovementConfig.cs",
  "Assets/Game/Scripts/Data/LevelProgressionConfig.cs",
  "Assets/Game/Editor/SideScrollerProjectBuilder.cs",
  "Assets/WebGLTemplates/SideScroller/index.html",
  "Assets/Mirror/version.txt",
  "ROADMAP.md",
  "THIRD_PARTY_ASSETS.md",
  "PrebuiltWebGL/index.html",
  "PrebuiltWebGL/Build/PrebuiltWebGL.data.unityweb",
  "PrebuiltWebGL/Build/PrebuiltWebGL.framework.js.unityweb",
  "PrebuiltWebGL/Build/PrebuiltWebGL.loader.js",
  "PrebuiltWebGL/Build/PrebuiltWebGL.wasm.unityweb",
];
await Promise.all(required.map(file => access(`${root}/${file}`)));

const version = await readFile(`${root}/ProjectSettings/ProjectVersion.txt`, "utf8");
assert.match(version, /6000\.3\.21f1/);

const mirror = await readFile(`${root}/Assets/Mirror/version.txt`, "utf8");
assert.match(mirror, /96\.11\.0/);

const movement = await readFile(`${root}/Assets/Game/Scripts/Player/PlayerMovement.cs`, "utf8");
assert.match(movement, /Rigidbody2D/);
assert.match(movement, /coyoteRemaining/);
assert.match(movement, /jumpBufferRemaining/);
assert.match(movement, /Physics2D\.BoxCast/);
assert.match(movement, /fallGravityMultiplier/);

const world = await readFile(`${root}/Assets/Game/Scripts/World/SideWorldBuilder.cs`, "utf8");
assert.match(world, /Tilemap/);
assert.match(world, /TilemapCollider2D/);
assert.match(world, /FillPlatform/);

const animation = await readFile(`${root}/Assets/Game/Editor/SideScrollerProjectBuilder.cs`, "utf8");
for (const state of ["Idle", "Run", "Jump", "Fall", "Attack", "Hit", "Death"]) {
  assert.match(animation, new RegExp(`\\"${state}\\"`));
}

const template = await readFile(`${root}/PrebuiltWebGL/index.html`, "utf8");
assert.match(template, /viewport-fit=cover/);
assert.match(template, /id="mobile-controls"/);
assert.match(template, /游戏大厅/);

const lobby = await readFile("portal/index.html", "utf8");
assert.match(lobby, /href="\/arena\/"/);
assert.match(lobby, /href="\/side-scroller\/"/);
assert.match(lobby, /选择作战区域/);

process.stdout.write("Side-scroller invariants passed: isolated Unity project, Mirror pin, Tilemap world, responsive movement, Animator graph, mobile WebGL shell, and dual-game lobby are present.\n");
