# EasyGame: Dead Rails

An independent Unity 2D side-scrolling co-op zombie RPG. This project does not
share scenes, gameplay scripts, or art with the existing top-down game in
`../unity-client`.

## Current playable slice

- Unity Grid/Tilemap artwork with one shared continuous collision factory for client and server
- shared fixed-step Rigidbody2D movement motor
- coyote time and jump buffering
- variable-height jump and faster fall
- smooth camera follow with map bounds
- whole-character licensed sprite sequences for idle, walk/run, jump, fall, attack, hit, and death
- player names and warrior/ranger/slime selection; monsters, PvP, XP and respawning
- keyboard and mobile WebGL controls

Open `Assets/Game/Scenes/SideScroller.unity`. The scene and configuration assets
can be regenerated with **EasyGame 2D > Prepare Project**.

## Controls

- Move: `A/D` or arrow keys
- Jump: `Space`, `W`, or up arrow
- Attack prototype: `J` or left mouse button

## Builds

Builds run the Unity regression suite first and produce matching source/artifact manifests. After building **both** WebGL and Linux server, run `npm run test:side-scroller` and `npm run build:web-release` from the repository root. A source edit invalidates the old build manifests and blocks publication until rebuilt. See [GAMEPLAY_ARCHITECTURE.md](GAMEPLAY_ARCHITECTURE.md) for invariants and remaining limitations.

Use **EasyGame 2D > Build Web Client**, or run:

```powershell
& 'D:\Unity\Editors\6000.3.21f1\Editor\Unity.exe' `
  -batchmode -quit `
  -projectPath 'D:\game\unity-side-scroller' `
  -executeMethod EasyGame.SideScroller.Editor.SideScrollerProjectBuilder.BuildWebGLCommandLine `
  -buildOutput 'D:\game\unity-side-scroller\PrebuiltWebGL'
```

Networking is pinned to Mirror v96.11.0. WebGL clients will use Mirror's
SimpleWebTransport. The server-authoritative Windows build has been verified
with two simultaneous WebGL clients. A production Linux server build is stored
in `PrebuiltServerLinux` and runs headlessly as an independent systemd service.
