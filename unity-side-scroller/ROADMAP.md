# EasyGame 2D Roadmap

Each phase must compile and remain playable before the next phase begins.

## Phase 1 — Foundation

- [x] independent Unity 6 project
- [x] modular folder structure
- [x] Unity Grid + Tilemap test map
- [x] deterministic scene generator
- [x] WebGL build pipeline

## Phase 2 — Player

- [x] Rigidbody2D movement and platform collision
- [x] responsive acceleration/deceleration
- [x] coyote time and jump buffer
- [x] variable-height jump and tuned falling
- [x] left/right facing
- [x] smooth bounded camera
- [x] Animator state graph: Idle, Run, Jump, Fall, Attack, Hit, Death

## Phase 3 — Mirror networking

- [x] Mirror v96.11.0 dependency imported and compilation-verified
- [ ] Linux dedicated-server build support
- [ ] SimpleWebTransport behind `wss://game.liaoke.org/side-scroller-socket`
- [ ] server-authoritative player input and spawn
- [ ] 2–4 player room flow
- [ ] position, facing, animation, and HP synchronization

## Phase 4 — Zombies

- [ ] server-only spawn manager
- [ ] Idle / Patrol / Chase / Attack / Death state machine
- [ ] Normal, Runner, and Tank variants

## Phase 5 — Combat

- [ ] server-authoritative melee hit box
- [ ] pistol/rifle projectile and reload loop
- [ ] damage, knockback, hit reaction

## Phase 6 — Rewards

- [ ] ScriptableObject level curve
- [ ] EXP, level, coin, and rarity drop tables

## Phase 7 — Inventory

- [ ] 20-slot server-owned inventory
- [ ] pickup, stack, potion, equip, and discard

## Phase 8 — UI

- [ ] player HUD, inventory, equipment, and death screen

## Phase 9 — Maps

- [ ] Safe Zone and Zombie Street
- [ ] shop NPC and portal

## Phase 10 — Polish

- [ ] licensed audio, hit effects, damage numbers, shake, and final art pass
