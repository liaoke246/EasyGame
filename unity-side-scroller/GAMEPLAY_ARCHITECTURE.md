# EasyGame 2D Gameplay Architecture

This project follows a server-authoritative 2D action-game layout. The rules below are release invariants, not visual tweaks.

## Coordinate contract

- A Tilemap cell at row `y` occupies `[y, y + 1]` in world space.
- The client Tilemap and headless server collision are generated from the same `PlatformDefinition` collection.
- Enemy and player spawn data records a feet position on a surface. `ActorGeometry2D` converts it to the rigidbody root position.
- The physics root owns `Rigidbody2D`, `Collider2D`, networking, health, and gameplay state.
- Every sprite renderer is parented under a `Feet Anchor` calculated from the collider. Character and slime sprites use a bottom-centre import pivot.
- Animation code may change frames and horizontal facing, but never invent vertical offsets for grounded states.
- Raised platforms use `PlatformEffector2D` one-way collision on both the visual client world and the headless server, so actors can jump through from below and land on the same authoritative top surface.

## Runtime layers

1. Input: `PlayerInputReader` and `MobileInputBridge` collect intent.
2. Simulation: offline `PlayerMovement` and server `SideScrollerNetworkPlayer` both delegate to `PlayerMotor2D` at a fixed physics timestep. They never maintain separate jump formulas.
3. Replication: Mirror SyncVars and the network transform publish authoritative state.
4. Presentation: pixel animators, camera, HUD, and effects consume state without changing physics.

## Player profile and avatars

- The WebGL shell collects a sanitized 1–14 character name and one avatar before Unity starts.
- The local client submits that profile once through a Mirror command; the server validates it and replicates the result through SyncVars.
- Warrior and ranger use a humanoid capsule; the playable slime uses a short horizontal capsule. All player colliders share the same root-to-feet offset and movement configuration. Changing profile cannot move the feet through the floor. Avatar PvP balance is not yet a competitive-game guarantee.

## Network authority

- The server owns positions, jumps, health, damage, kills, deaths, monster AI, and respawning.
- Clients send bounded input intent and never submit damage results.
- Movement input is finite, clamped, sequenced, and expires after 350 ms without a fresh packet. Jump edges are reliable and consumed on a physics step; coyote time/buffers advance only on that step.
- Grounding uses downward rays, support normals and static/kinematic support. Walls, platform interiors, rising actors and other dynamic actors cannot grant a ground jump.
- PvP and monster attacks use reusable scene-local queries, exclude triggers/self, deduplicate compound bodies and reject targets behind solid terrain. Player attacks have windup, hit, recovery and cooldown; being hit or dying cancels pending damage.
- Dead actors disable both collision and simulation; respawn restores both and resets transient state. Clients animate from replicated motion/speed, not a non-simulated rigidbody velocity.
- Local HUD reads synchronized health; only remote players receive an overhead health bar.
- Every IMGUI overlay restores global GUI state after drawing. XP is consumed into real levels, not merely wrapped by the progress bar.

## Combat actions (0.5.0)

- `CombatActions2D` owns the shared windup/contact/recovery, box size, PvE/PvP damage, impulse and cooldown definitions. `CombatActionClock` rejects invalid IDs, overlapping casts and cooldown bypasses, and consumes contact exactly once. Cancel does not refund a spent cooldown.
- Monster proximity starts anticipation rather than dealing damage. Direction locks until recovery; the server re-queries targets at contact, so dodging, terrain and interruptions remain meaningful.
- Attack ID and start timestamp are replicated. Every avatar samples its existing whole-body sprite strip by action phase; no added arm layer, scale animation, or collider movement is used for presentation. Pixel ribbon meshes are visual only.
- Hitstun/death cancel pending contact. Rising hits modify authoritative rigidbody velocity only after accepted damage; PvP launch is reduced and spawn immunity also blocks the impulse.
- WebGL buttons share the same mobile input edge path as movement. A minimal `.jslib` bridge sends cooldown state to the accessible HTML hotbar, and is included in source fingerprints. Offline mode previews the same three skills without network targets.

## Release gates

- Unity WebGL build succeeds with zero C# errors.
- Linux dedicated-server build succeeds.
- A local headless server is started before release.
- Two WebGL clients connect and validate player count, PvP damage, death, and respawn.
- Player, zombie, and slime feet are visually checked against both floor and platform surfaces.
- Desktop jump and mobile landscape touch controls are exercised in the built WebGL player.

## Automated verification and known boundaries

`EasyGame.SideScroller.Editor.ReleaseVerification.RunRegressionTests` runs actual Unity 2D physics in isolated editor preview scenes, combat queries, sprite-state checks and network-input/progression rules. The build entry points run this suite before producing artifacts. These are executable regression scenarios, not assertions that source code contains a keyword.

Both WebGL and Linux outputs contain `release-manifest.json`. It records the same normalized source fingerprint plus hashes of every output file. `scripts/verify-side-scroller-release.mjs` rejects stale sources, altered/missing/extra artifacts, or mismatched client/server versions before assembling a release. Generated scenes/prefabs are recreated from the fingerprinted project builder. Raw licensed art is installed separately and not redistributed in this repository.

Web shell behavior tests execute the actual template script with denied storage, failed loading, viewport changes and multiple pointer ownership. They do not emulate a real iPhone GPU/Safari engine. Real-device performance and internet latency still require device/network testing.

Movement remains server-authoritative with interpolation, not client prediction/reconciliation. This deliberately avoids two competing physics authorities but still incurs round-trip input latency. Competitive combat balancing, durable character saves, reconnect session restoration and a full content progression loop are separate unfinished product work.
