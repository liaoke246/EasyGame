using UnityEngine;
using UnityEngine.Tilemaps;

namespace EasyGame.SideScroller.World
{
    public sealed class SideWorldBuilder : MonoBehaviour
    {
        public static readonly Bounds MapBounds = new Bounds(new Vector3(54f, 1f, 0f), new Vector3(136f, 20f, 4f));
        public static readonly Vector3 PlayerSpawn = new Vector3(-7f, -2.15f, 0f);

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
            CreateServerRectangle("Ground Collision", new Vector2(54f, -5f), new Vector2(137f, 3f));
            CreateServerPlatform("Platform -2 to 7", -2, 7, -1);
            CreateServerPlatform("Platform 12 to 20", 12, 20, 1);
            CreateServerPlatform("Platform 25 to 32", 25, 32, -1);
            CreateServerPlatform("Platform 38 to 48", 38, 48, 2);
            CreateServerPlatform("Platform 54 to 61", 54, 61, 0);
            CreateServerPlatform("Platform 67 to 76", 67, 76, 2);
            CreateServerPlatform("Platform 82 to 91", 82, 91, -1);
            CreateServerPlatform("Platform 98 to 108", 98, 108, 1);
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

            for (int x = -14; x <= 122; x++)
            {
                tilemap.SetTile(new Vector3Int(x, -4, 0), surface);
                tilemap.SetTile(new Vector3Int(x, -5, 0), ground);
                tilemap.SetTile(new Vector3Int(x, -6, 0), ground);
            }

            FillPlatform(tilemap, platformLeft, platformMiddle, platformRight, -2, 7, -1);
            FillPlatform(tilemap, platformLeft, platformMiddle, platformRight, 12, 20, 1);
            FillPlatform(tilemap, platformLeft, platformMiddle, platformRight, 25, 32, -1);
            FillPlatform(tilemap, platformLeft, platformMiddle, platformRight, 38, 48, 2);
            FillPlatform(tilemap, platformLeft, platformMiddle, platformRight, 54, 61, 0);
            FillPlatform(tilemap, platformLeft, platformMiddle, platformRight, 67, 76, 2);
            FillPlatform(tilemap, platformLeft, platformMiddle, platformRight, 82, 91, -1);
            FillPlatform(tilemap, platformLeft, platformMiddle, platformRight, 98, 108, 1);
        }

        private static void FillPlatform(Tilemap map, Tile left, Tile middle, Tile right, int startX, int endX, int y)
        {
            for (int x = startX; x <= endX; x++)
            {
                map.SetTile(new Vector3Int(x, y, 0), x == startX ? left : x == endX ? right : middle);
            }
        }

        private static void CreateServerPlatform(string name, int startX, int endX, int y)
        {
            float width = endX - startX + 1f;
            CreateServerRectangle(name, new Vector2((startX + endX) * 0.5f, y), new Vector2(width, 1f));
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
    }
}
