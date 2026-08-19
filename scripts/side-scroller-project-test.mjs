import assert from "node:assert/strict";
import { access, readFile } from "node:fs/promises";

const root = "unity-side-scroller";
const required = [
  "Assets/Game/Scenes/SideScroller.unity",
  "Assets/Game/Scripts/Core/SideScrollerBootstrap.cs",
  "Assets/Game/Scripts/Player/PlayerInputReader.cs",
  "Assets/Game/Scripts/Player/PlayerMovement.cs",
  "Assets/Game/Scripts/Player/PlayerAnimation.cs",
  "Assets/Game/Scripts/Core/PixelCharacterAnimator.cs",
  "Assets/Game/Scripts/Core/PixelSlimeAnimator.cs",
  "Assets/Game/Scripts/Core/GroundProbe2D.cs",
  "Assets/Game/Scripts/Core/ActorGeometry2D.cs",
  "Assets/Game/Scripts/Core/MobileInputBridge.cs",
  "Assets/Game/Scripts/Combat/PlayerCombat.cs",
  "Assets/Game/Scripts/World/SideWorldBuilder.cs",
  "Assets/Game/Scripts/World/SideCameraRig.cs",
  "Assets/Game/Scripts/Data/PlayerMovementConfig.cs",
  "Assets/Game/Scripts/Data/LevelProgressionConfig.cs",
  "Assets/Game/Scripts/Network/SideScrollerNetworkManager.cs",
  "Assets/Game/Scripts/Network/SideScrollerNetworkPlayer.cs",
  "Assets/Game/Scripts/Network/SideScrollerNetworkTransform.cs",
  "Assets/Game/Scripts/Enemies/SideScrollerNetworkZombie.cs",
  "Assets/Game/Scripts/Enemies/SideScrollerNetworkSlime.cs",
  "Assets/Game/Scripts/UI/PixelHudDrawing.cs",
  "Assets/Game/Scripts/UI/NetworkStatusHud.cs",
  "Assets/Game/Prefabs/NetworkPlayer.prefab",
  "Assets/Game/Editor/SideScrollerProjectBuilder.cs",
  "Assets/WebGLTemplates/SideScroller/index.html",
  "Assets/Mirror/version.txt",
  "ROADMAP.md",
  "THIRD_PARTY_ASSETS.md",
  "GAMEPLAY_ARCHITECTURE.md",
  "PrebuiltWebGL/index.html",
  "PrebuiltWebGL/Build/PrebuiltWebGL.data.unityweb",
  "PrebuiltWebGL/Build/PrebuiltWebGL.framework.js.unityweb",
  "PrebuiltWebGL/Build/PrebuiltWebGL.loader.js",
  "PrebuiltWebGL/Build/PrebuiltWebGL.wasm.unityweb",
];
await Promise.all(required.map(file => access(`${root}/${file}`)));
await access("scripts/install-side-scroller-art.ps1");

const version = await readFile(`${root}/ProjectSettings/ProjectVersion.txt`, "utf8");
assert.match(version, /6000\.3\.21f1/);

const mirror = await readFile(`${root}/Assets/Mirror/version.txt`, "utf8");
assert.match(mirror, /96\.11\.0/);

const movement = await readFile(`${root}/Assets/Game/Scripts/Player/PlayerMovement.cs`, "utf8");
assert.match(movement, /Rigidbody2D/);
assert.match(movement, /coyoteRemaining/);
assert.match(movement, /jumpBufferRemaining/);
assert.match(movement, /GroundProbe2D\.Check/);
assert.match(movement, /fallGravityMultiplier/);

const world = await readFile(`${root}/Assets/Game/Scripts/World/SideWorldBuilder.cs`, "utf8");
assert.match(world, /Tilemap/);
assert.match(world, /TilemapCollider2D/);
assert.match(world, /FillPlatform/);
assert.match(world, /BuildServerCollision/);
assert.match(world, /BoxCollider2D/);
assert.match(world, /GandalfHardcore/);
assert.match(world, /ground-top/);
assert.match(world, /PlatformDefinition\[] Platforms/);
assert.match(world, /platform\.Row \+ 0\.5f/);

const bootstrap = await readFile(`${root}/Assets/Game/Scripts/Core/SideScrollerBootstrap.cs`, "utf8");
assert.match(bootstrap, /Utils\.IsHeadless\(\)/);
assert.match(bootstrap, /private void Awake\(\)/);
assert.match(bootstrap, /world\.BuildServerCollision\(\)/);
assert.match(bootstrap, /initialized before network startup/);

const animation = await readFile(`${root}/Assets/Game/Editor/SideScrollerProjectBuilder.cs`, "utf8");
for (const state of ["Idle", "Run", "Jump", "Fall", "Attack", "Hit", "Death"]) {
  assert.match(animation, new RegExp(`\\"${state}\\"`));
}
assert.match(animation, /SimpleWebTransport/);
assert.match(animation, /StandaloneLinux64/);
assert.match(animation, /StandaloneBuildSubtarget\.Server/);
assert.match(animation, /HeadlessStartOptions\.AutoStartServer/);

const networkPlayer = await readFile(`${root}/Assets/Game/Scripts/Network/SideScrollerNetworkPlayer.cs`, "utf8");
assert.match(networkPlayer, /\[Command\(channel = Channels\.Unreliable\)\]/);
assert.match(networkPlayer, /\[SyncVar\]/);
assert.match(networkPlayer, /\[ClientRpc\]/);
assert.match(networkPlayer, /if \(!isServer/);
assert.match(networkPlayer, /ResolveAttackHits/);
assert.match(networkPlayer, /SideScrollerNetworkSlime/);
assert.match(networkPlayer, /ServerRespawn/);
assert.match(networkPlayer, /attacker\.kills\+\+/);

const characterAnimator = await readFile(`${root}/Assets/Game/Scripts/Core/PixelCharacterAnimator.cs`, "utf8");
assert.match(characterAnimator, /GandalfHardcore/);
assert.match(characterAnimator, /LoadSequence\("walk", 8\)/);
assert.match(characterAnimator, /LoadSequence\("attack", 6\)/);
assert.match(characterAnimator, /death.*10/);

const zombie = await readFile(`${root}/Assets/Game/Scripts/Enemies/SideScrollerNetworkZombie.cs`, "utf8");
assert.match(zombie, /NetworkBehaviour/);
assert.match(zombie, /ApplyDamage/);

const slime = await readFile(`${root}/Assets/Game/Scripts/Enemies/SideScrollerNetworkSlime.cs`, "utf8");
assert.match(slime, /NetworkBehaviour/);
assert.match(slime, /GroundProbe2D\.Check/);
assert.match(slime, /DealContactDamage/);

const groundProbe = await readFile(`${root}/Assets/Game/Scripts/Core/GroundProbe2D.cs`, "utf8");
assert.match(groundProbe, /OverlapBoxAll/);
assert.match(groundProbe, /hit == bodyCollider/);

const actorGeometry = await readFile(`${root}/Assets/Game/Scripts/Core/ActorGeometry2D.cs`, "utf8");
assert.match(actorGeometry, /HumanoidFeetLocalY/);
assert.match(actorGeometry, /SlimeFeetLocalY/);
assert.match(actorGeometry, /FeetLocalPosition/);

const runtimeVisual = await readFile(`${root}/Assets/Game/Scripts/Core/RuntimePlayerVisual.cs`, "utf8");
assert.match(runtimeVisual, /Feet Anchor/);
assert.match(runtimeVisual, /ActorGeometry2D\.FeetLocalPosition/);

assert.match(animation, /spritePivot = requiredPivot/);

const networkManager = await readFile(`${root}/Assets/Game/Scripts/Network/SideScrollerNetworkManager.cs`, "utf8");
assert.match(networkManager, /side-scroller-socket/);
assert.match(networkManager, /NetworkTime\.rtt/);
assert.match(networkManager, /RootPositionForFeet/);

const template = await readFile(`${root}/PrebuiltWebGL/index.html`, "utf8");
assert.match(template, /viewport-fit=cover/);
assert.match(template, /id="mobile-controls"/);
assert.match(template, /SetMobileControl/);
assert.match(template, /游戏大厅/);

const lobby = await readFile("portal/index.html", "utf8");
assert.match(lobby, /href="\/arena\/"/);
assert.match(lobby, /href="\/side-scroller\/"/);
assert.match(lobby, /选择作战区域/);

process.stdout.write("Side-scroller invariants passed: isolated Unity project, Mirror pin, Tilemap world, responsive movement, Animator graph, mobile WebGL shell, and dual-game lobby are present.\n");
