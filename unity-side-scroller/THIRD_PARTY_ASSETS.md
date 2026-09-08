# Third-party assets and dependencies

Every external dependency or asset used by the side-scroller must be listed here.
Unknown-origin or extracted copyrighted assets are not allowed.

| Name | Author | Source | License | Use |
| --- | --- | --- | --- | --- |
| Mirror v96.11.0 | Mirror Networking contributors | https://github.com/MirrorNetworking/Mirror/releases/tag/v96.11.0 | MIT | Multiplayer framework and SimpleWebTransport |
| FREE 2D Pixel Art Male and Female Character | GandalfHardcore | https://gandalfhardcore.itch.io/2d-pixel-art-male-and-female-character | Custom free-use license | Player, infected warrior, and three-color slime animation frames |
| FREE Pixel Art Sidescroller Asset Pack 32x32 Overworld | GandalfHardcore | https://gandalfhardcore.itch.io/free-pixel-art-sidescroller-asset-pack-32x32-overworld | Custom free-use license | Terrain, five-layer background, props, and HUD |

The GandalfHardcore license permits use and modification in commercial and
non-commercial games, but prohibits repackaging or redistributing the source
assets. Raw and derived PNG files are therefore gitignored and are not part of
this public repository. Run `scripts/install-side-scroller-art.ps1` to download
them from the author's itch.io pages and generate Unity-ready frames locally.
The deployed WebGL files contain the art only as part of the finished game.
