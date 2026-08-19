using EasyGame.SideScroller.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace EasyGame.SideScroller.World
{
    public sealed class SideWorldBuilder : MonoBehaviour
    {
        private const int WorldStartX = -14;
        private const int WorldEndX = 122;
        private const int FloorSurfaceRow = -4;
        private const int FloorBottomRow = -6;

        private static readonly PlatformDefinition[] Platforms =
        {
            new PlatformDefinition(-2, 7, -1),
            new PlatformDefinition(12, 20, 1),
            new PlatformDefinition(25, 32, -1),
            new PlatformDefinition(38, 48, 2),
            new PlatformDefinition(54, 61, 0),
            new PlatformDefinition(67, 76, 2),
            new PlatformDefinition(82, 91, -1),
            new PlatformDefinition(98, 108, 1),
        };

        public static readonly Bounds MapBounds = new Bounds(new Vector3(54.5f, 1f, 0f), new Vector3(137f, 20f, 4f));
        public const float FloorSurfaceY = FloorSurfaceRow + 1f;
        public const float PlayerSpawnSpacing = 2.25f;
        public static readonly Vector3 PlayerSpawnFeet = new Vector3(-7f, FloorSurfaceY, 0f);
        public static readonly Vector3 PlayerSpawn = ActorGeometry2D.RootPositionForFeet(PlayerSpawnFeet, ActorGeometry2D.HumanoidFeetLocalY);

        private bool built;

        public void Build()
        {
            if (built)
            {
                return;
            }

            built = true;
            CreateBackground();
            CreateTilemap();
            CreateLandmarks();
        }

        public void BuildServerCollision()
        {
            if (built)
            {
                return;
            }

            built = true;
            float groundWidth = WorldEndX - WorldStartX + 1f;
            float groundHeight = FloorSurfaceRow - FloorBottomRow + 1f;
            CreateServerRectangle(
                "Ground Collision",
                new Vector2((WorldStartX + WorldEndX + 1f) * 0.5f, (FloorBottomRow + FloorSurfaceRow + 1f) * 0.5f),
                new Vector2(groundWidth, groundHeight));
            foreach (PlatformDefinition platform in Platforms)
            {
                CreateServerPlatform(platform);
            }
        }

        private static void CreateBackground()
        {
            const float backgroundY = 0.72f;
            const float backgroundScale = 1.18f;
            CreateRepeatedBackground("Sky", "background-5", backgroundY, backgroundScale, 5f, -30);
            CreateRepeatedBackground("Mountains", "background-4", backgroundY, backgroundScale, 4.8f, -29);
            CreateRepeatedBackground("Distant Pines", "background-3", backgroundY, backgroundScale, 4.6f, -28);
            CreateRepeatedBackground("Middle Pines", "background-2", backgroundY, backgroundScale, 4.4f, -27);
            CreateRepeatedBackground("Foreground Pines", "background-1", backgroundY, backgroundScale, 4.2f, -26);
        }

        private static void CreateTilemap()
        {
            GameObject gridObject = new GameObject("World Grid", typeof(Grid));
            Grid grid = gridObject.GetComponent<Grid>();
            grid.cellSize = Vector3.one;

            GameObject mapObject = new GameObject("Collision Tilemap", typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D));
            mapObject.transform.SetParent(gridObject.transform, false);
            Tilemap tilemap = mapObject.GetComponent<Tilemap>();
            TilemapRenderer renderer = mapObject.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = 0;

            Tile ground = ScriptableObject.CreateInstance<Tile>();
            ground.name = "Runtime Ground Tile";
            ground.sprite = LoadWorldSprite("ground-fill");
            ground.color = Color.white;
            ground.colliderType = Tile.ColliderType.Grid;

            Tile surface = ScriptableObject.CreateInstance<Tile>();
            surface.name = "Runtime Surface Tile";
            surface.sprite = LoadWorldSprite("ground-top");
            surface.color = Color.white;
            surface.colliderType = Tile.ColliderType.Grid;

            Tile platformLeft = CreateRuntimeTile("Platform Left", "ground-left");
            Tile platformMiddle = CreateRuntimeTile("Platform Middle", "ground-top");
            Tile platformRight = CreateRuntimeTile("Platform Right", "ground-right");

            for (int x = WorldStartX; x <= WorldEndX; x++)
            {
                tilemap.SetTile(new Vector3Int(x, FloorSurfaceRow, 0), surface);
                for (int y = FloorBottomRow; y < FloorSurfaceRow; y++)
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), ground);
                }
            }

            foreach (PlatformDefinition platform in Platforms)
            {
                FillPlatform(tilemap, platformLeft, platformMiddle, platformRight, platform.StartX, platform.EndX, platform.Row);
            }
        }

        private static void FillPlatform(Tilemap map, Tile left, Tile middle, Tile right, int startX, int endX, int y)
        {
            for (int x = startX; x <= endX; x++)
            {
                map.SetTile(new Vector3Int(x, y, 0), x == startX ? left : x == endX ? right : middle);
            }
        }

        private static void CreateServerPlatform(PlatformDefinition platform)
        {
            float width = platform.EndX - platform.StartX + 1f;
            Vector2 center = new Vector2((platform.StartX + platform.EndX + 1f) * 0.5f, platform.Row + 0.5f);
            CreateServerRectangle($"Platform {platform.StartX} to {platform.EndX}", center, new Vector2(width, 1f));
        }

        private static void CreateServerRectangle(string name, Vector2 position, Vector2 size)
        {
            GameObject collision = new GameObject(name, typeof(BoxCollider2D));
            collision.transform.position = position;
            collision.GetComponent<BoxCollider2D>().size = size;
        }

        private static void CreateLandmarks()
        {
            CreateProp("Safe Camp", "small-tent", new Vector3(-10.3f, -2.5f, -0.1f), 3);
            CreateProp("Ruined Shrine", "angel-statue", new Vector3(118.5f, -2.5f, -0.1f), 3);

            float[] treePositions = { -12f, 10f, 22f, 35f, 50f, 64f, 79f, 94f, 109f, 121f };
            foreach (float x in treePositions)
            {
                CreateProp($"Pine {x}", "large-pine-tree", new Vector3(x, -0.75f, 0.2f), -2);
            }

            float[] grassPositions = { -5f, 7f, 20f, 33f, 48f, 62f, 78f, 92f, 108f };
            foreach (float x in grassPositions)
            {
                CreateProp($"Tall Grass {x}", "tall-grass", new Vector3(x, -3f, -0.05f), 2);
            }
        }

        private static Tile CreateRuntimeTile(string name, string spriteName)
        {
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = name;
            tile.sprite = LoadWorldSprite(spriteName);
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.Grid;
            return tile;
        }

        private static Sprite LoadWorldSprite(string name)
        {
            return Resources.Load<Sprite>($"ThirdParty/GandalfHardcore/World/{name}");
        }

        private static void CreateRepeatedBackground(string name, string spriteName, float y, float scale, float z, int order)
        {
            Sprite sprite = Resources.Load<Sprite>($"ThirdParty/GandalfHardcore/World/Backgrounds/{spriteName}");
            if (sprite == null)
            {
                Debug.LogError($"Missing licensed pixel background sprite: {spriteName}. Run scripts/install-side-scroller-art.ps1.");
                return;
            }

            float width = sprite.bounds.size.x * scale;
            int count = Mathf.CeilToInt(137f / width) + 2;
            for (int index = 0; index < count; index++)
            {
                GameObject layer = new GameObject($"{name} {index + 1}");
                layer.transform.position = new Vector3(-14f + index * width, y, z);
                layer.transform.localScale = Vector3.one * scale;
                SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = order;
            }
        }

        private static void CreateProp(string name, string spriteName, Vector3 position, int order)
        {
            Sprite sprite = LoadWorldSprite(spriteName);
            if (sprite == null)
            {
                return;
            }

            GameObject item = new GameObject(name);
            item.transform.position = position;
            SpriteRenderer renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
        }

        private readonly struct PlatformDefinition
        {
            public PlatformDefinition(int startX, int endX, int row)
            {
                StartX = startX;
                EndX = endX;
                Row = row;
            }

            public int StartX { get; }
            public int EndX { get; }
            public int Row { get; }
        }
    }
}
