# EasyGame: Dead Rails

An independent Unity 2D side-scrolling co-op zombie RPG. This project does not
share scenes, gameplay scripts, or art with the existing top-down game in
`../unity-client`.

## Current playable slice

- real Unity Grid and Tilemap collision
- responsive Rigidbody2D movement
- coyote time and jump buffering
- variable-height jump and faster fall
- smooth camera follow with map bounds
- Animator states for idle, run, jump, fall, attack, hit, and death
- keyboard and mobile WebGL controls

Open `Assets/Game/Scenes/SideScroller.unity`. The scene and configuration assets
can be regenerated with **EasyGame 2D > Prepare Project**.

## Controls

- Move: `A/D` or arrow keys
- Jump: `Space`, `W`, or up arrow
- Attack prototype: `J` or left mouse button

## Builds

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
