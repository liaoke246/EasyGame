# EasyGame Unity client

This folder contains the new Unity 6 WebGL client. The existing Phaser client is
kept as a production fallback while the migration is validated.

## Open locally

1. Install Unity `6000.3.21f1` with Web Build Support.
2. Open this folder in Unity Hub.
3. Open `Assets/Scenes/Game.unity` and press Play for the offline visual demo.
4. Run `EasyGame > Build Web Client` to build into `../client/dist`.

The WebGL build connects to the existing Socket.IO server through a small browser
bridge. No third-party Unity networking package is required.
