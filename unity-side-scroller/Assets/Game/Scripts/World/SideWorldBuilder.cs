using EasyGame.SideScroller.Core;
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

        private static void CreateBackground()
        {
            CreateRectangle("Sky", new Vector3(54f, 1f, 4f), new Vector3(136f, 20f, 1f), new Color(0.055f, 0.105f, 0.15f), -20);
            CreateRectangle("Distant City", new Vector3(54f, -0.2f, 3f), new Vector3(136f, 5.2f, 1f), new Color(0.09f, 0.16f, 0.19f), -15);

            for (int index = 0; index < 14; index++)
            {
                float x = -8f + index * 10f;
                float height = 2.2f + (index % 4) * 0.75f;
                CreateRectangle($"Building {index + 1}", new Vector3(x, -0.6f + height * 0.5f, 2f), new Vector3(6.8f, height, 1f), new Color(0.12f, 0.2f, 0.22f), -13);
                for (int window = 0; window < 3; window++)
                {
                    CreateRectangle($"Window {index + 1}-{window + 1}", new Vector3(x - 2f + window * 2f, 0.15f, 1.8f), new Vector3(0.38f, 0.2f, 1f), new Color(0.72f, 0.54f, 0.2f, 0.55f), -12);
                }
            }
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
            ground.sprite = RuntimeSpriteFactory.White;
            ground.color = new Color(0.24f, 0.31f, 0.22f);
            ground.colliderType = Tile.ColliderType.Grid;

            Tile surface = ScriptableObject.CreateInstance<Tile>();
            surface.name = "Runtime Surface Tile";
            surface.sprite = RuntimeSpriteFactory.White;
            surface.color = new Color(0.43f, 0.52f, 0.28f);
            surface.colliderType = Tile.ColliderType.Grid;

            for (int x = -14; x <= 122; x++)
            {
                tilemap.SetTile(new Vector3Int(x, -4, 0), surface);
                tilemap.SetTile(new Vector3Int(x, -5, 0), ground);
                tilemap.SetTile(new Vector3Int(x, -6, 0), ground);
            }

            FillPlatform(tilemap, surface, -2, 7, -1);
            FillPlatform(tilemap, surface, 12, 20, 1);
            FillPlatform(tilemap, surface, 25, 32, -1);
            FillPlatform(tilemap, surface, 38, 48, 2);
            FillPlatform(tilemap, surface, 54, 61, 0);
            FillPlatform(tilemap, surface, 67, 76, 2);
            FillPlatform(tilemap, surface, 82, 91, -1);
            FillPlatform(tilemap, surface, 98, 108, 1);
        }

        private static void FillPlatform(Tilemap map, Tile tile, int startX, int endX, int y)
        {
            for (int x = startX; x <= endX; x++)
            {
                map.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }

        private static void CreateLandmarks()
        {
            CreateRectangle("Safe Zone Beacon", new Vector3(-8.5f, -2.25f, -0.1f), new Vector3(0.18f, 2.4f, 1f), new Color(0.28f, 0.9f, 0.72f), 2);
            CreateRectangle("Street Exit Beacon", new Vector3(118f, -2.25f, -0.1f), new Vector3(0.18f, 2.4f, 1f), new Color(0.95f, 0.52f, 0.19f), 2);
        }

        private static GameObject CreateRectangle(string name, Vector3 position, Vector3 scale, Color color, int order)
        {
            GameObject item = new GameObject(name);
            item.transform.position = position;
            item.transform.localScale = scale;
            SpriteRenderer renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.White;
            renderer.color = color;
            renderer.sortingOrder = order;
            return item;
        }
    }
}
