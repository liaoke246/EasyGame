# EasyGame 2D Gameplay Architecture

This project follows a server-authoritative 2D action-game layout. The rules below are release invariants, not visual tweaks.

## Coordinate contract

- A Tilemap cell at row `y` occupies `[y, y + 1]` in world space.
- The client Tilemap and headless server collision are generated from the same `PlatformDefinition` collection.
- Enemy and player spawn data records a feet position on a surface. `ActorGeometry2D` converts it to the rigidbody root position.
- The physics root owns `Rigidbody2D`, `Collider2D`, networking, health, and gameplay state.
- Every sprite renderer is parented under a `Feet Anchor` calculated from the collider. Character and slime sprites use a bottom-centre import pivot.
- Animation code may change frames and horizontal facing, but never invent vertical offsets for grounded states.

## Runtime layers

1. Input: `PlayerInputReader` and `MobileInputBridge` collect intent.
2. Simulation: offline `PlayerMovement` or server `SideScrollerNetworkPlayer` resolves movement and combat.
3. Replication: Mirror SyncVars and the network transform publish authoritative state.
4. Presentation: pixel animators, camera, HUD, and effects consume state without changing physics.

## Network authority

- The server owns positions, jumps, health, damage, kills, deaths, monster AI, and respawning.
- Clients send bounded input intent and never submit damage results.
- PvP and monster attacks use collider-centred overlap queries on the server.
- Local HUD reads synchronized health; only remote players receive an overhead health bar.

## Release gates

- Unity WebGL build succeeds with zero C# errors.
- Linux dedicated-server build succeeds.
- A local headless server is started before release.
- Two WebGL clients connect and validate player count, PvP damage, death, and respawn.
- Player, zombie, and slime feet are visually checked against both floor and platform surfaces.
- Desktop jump and mobile landscape touch controls are exercised in the built WebGL player.
