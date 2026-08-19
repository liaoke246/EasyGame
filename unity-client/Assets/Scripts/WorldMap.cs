using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyGame
{
    public sealed class WorldMap : MonoBehaviour
    {
        private readonly List<Rect> blockedAreas = new List<Rect>();
        private float width;
        private float height;

        public float ServerHeight { get; private set; }

        public void Build(WorldDefinition world)
        {
            ClearChildren();
            blockedAreas.Clear();
            ServerHeight = world.height;
            width = world.width * GameCoordinates.WorldScale;
            height = world.height * GameCoordinates.WorldScale;

            TopDownArt.CreateTiledPlane(
                transform,
                "Illustrated Grass Ground",
                "Tiles/tile_01",
                new Vector3(width * 0.5f, -0.12f, height * 0.5f),
                new Vector2(width, height),
                0.64f,
                0,
                new Color(0.82f, 0.95f, 0.82f));
            DrawGroundDetails();
            DrawBoundary();

            if (world.obstacles == null)
            {
                return;
            }
            foreach (ObstacleState obstacle in world.obstacles)
            {
                Rect rect = ToRect(obstacle);
                blockedAreas.Add(ToCollisionRect(obstacle));
                DrawObstacle(obstacle, rect);
            }
        }

        public bool IsBlocked(Vector3 worldPosition, float radius)
        {
            if (worldPosition.x < radius || worldPosition.z < radius || worldPosition.x > width - radius || worldPosition.z > height - radius)
            {
                return true;
            }
            foreach (Rect rect in blockedAreas)
            {
                float nearestX = Mathf.Clamp(worldPosition.x, rect.xMin, rect.xMax);
                float nearestZ = Mathf.Clamp(worldPosition.z, rect.yMin, rect.yMax);
                float dx = worldPosition.x - nearestX;
                float dz = worldPosition.z - nearestZ;
                if (dx * dx + dz * dz < radius * radius)
                {
                    return true;
                }
            }
            return false;
        }

        private Rect ToRect(ObstacleState obstacle)
        {
            float x = obstacle.x * GameCoordinates.WorldScale;
            float z = (ServerHeight - obstacle.y - obstacle.height) * GameCoordinates.WorldScale;
            return new Rect(x, z, obstacle.width * GameCoordinates.WorldScale, obstacle.height * GameCoordinates.WorldScale);
        }

        private Rect ToCollisionRect(ObstacleState obstacle)
        {
            float maximumInset = Mathf.Max(0f, Mathf.Min(obstacle.width, obstacle.height) * 0.5f - 1f);
            float inset = Mathf.Clamp(obstacle.hitboxInset, 0f, maximumInset);
            float x = (obstacle.x + inset) * GameCoordinates.WorldScale;
            float z = (ServerHeight - obstacle.y - obstacle.height + inset) * GameCoordinates.WorldScale;
            return new Rect(
                x,
                z,
                (obstacle.width - inset * 2f) * GameCoordinates.WorldScale,
                (obstacle.height - inset * 2f) * GameCoordinates.WorldScale);
        }

        private void DrawGroundDetails()
        {
            System.Random random = new System.Random(8127);
            string[] smallDetails =
            {
                "Tiles/tile_134", "Tiles/tile_210", "Tiles/tile_213", "Tiles/tile_235",
                "Tiles/tile_237", "Tiles/tile_238", "Tiles/tile_240", "Tiles/tile_264"
            };
            int detailCount = Mathf.Clamp(Mathf.RoundToInt(width * height * 0.14f), 80, 150);
            for (int index = 0; index < detailCount; index++)
            {
                float x = 0.4f + (float)random.NextDouble() * (width - 0.8f);
                float z = 0.4f + (float)random.NextDouble() * (height - 0.8f);
                float size = 0.16f + (float)random.NextDouble() * 0.32f;
                TopDownArt.CreateWorldSprite(
                    transform,
                    $"Ground Detail {index}",
                    smallDetails[index % smallDetails.Length],
                    new Vector3(x, -0.075f, z),
                    new Vector2(size, size),
                    1,
                    random.Next(0, 360),
                    new Color(0.82f, 0.93f, 0.78f, 0.82f));
            }
        }

        private void DrawBoundary()
        {
            Color border = new Color(0.24f, 0.16f, 0.08f);
            TopDownArt.CreateColorPlane(transform, "North Boundary", new Vector3(width * 0.5f, -0.055f, height + 0.08f), new Vector2(width + 0.3f, 0.16f), border, 3);
            TopDownArt.CreateColorPlane(transform, "South Boundary", new Vector3(width * 0.5f, -0.055f, -0.08f), new Vector2(width + 0.3f, 0.16f), border, 3);
            TopDownArt.CreateColorPlane(transform, "West Boundary", new Vector3(-0.08f, -0.055f, height * 0.5f), new Vector2(0.16f, height), border, 3);
            TopDownArt.CreateColorPlane(transform, "East Boundary", new Vector3(width + 0.08f, -0.055f, height * 0.5f), new Vector2(0.16f, height), border, 3);
        }

        private void DrawObstacle(ObstacleState obstacle, Rect rect)
        {
            Vector3 center = new Vector3(rect.center.x, 0f, rect.center.y);
            switch (obstacle.type)
            {
                case "cabin": DrawCabin(obstacle.id, center, rect.width, rect.height); break;
                case "pond": DrawPond(obstacle.id, center, rect.width, rect.height); break;
                case "tree": DrawTree(obstacle.id, center, rect.width, rect.height); break;
                case "garden": DrawGarden(obstacle.id, center, rect.width, rect.height); break;
                default: DrawRock(obstacle.id, center, rect.width, rect.height); break;
            }
        }

        private void DrawCabin(string id, Vector3 center, float sizeX, float sizeZ)
        {
            Transform root = VisualFactory.Empty(transform, id, center);
            TopDownArt.CreateColorPlane(root, "Cabin Drop Shadow", new Vector3(0.12f, -0.062f, -0.12f), new Vector2(sizeX * 1.1f, sizeZ * 1.1f), new Color(0.06f, 0.08f, 0.065f, 0.38f), 3);
            TopDownArt.CreateTiledPlane(root, "Wood Roof", "Tiles/tile_42", new Vector3(0f, -0.045f, 0f), new Vector2(sizeX, sizeZ), 0.64f, 4, new Color(0.82f, 0.72f, 0.52f));
            Color trim = new Color(0.43f, 0.23f, 0.09f);
            TopDownArt.CreateColorPlane(root, "North Roof Trim", new Vector3(0f, -0.037f, sizeZ * 0.48f), new Vector2(sizeX, 0.13f), trim, 5);
            TopDownArt.CreateColorPlane(root, "South Roof Trim", new Vector3(0f, -0.037f, -sizeZ * 0.48f), new Vector2(sizeX, 0.13f), trim, 5);
            TopDownArt.CreateColorPlane(root, "West Roof Trim", new Vector3(-sizeX * 0.48f, -0.037f, 0f), new Vector2(0.13f, sizeZ), trim, 5);
            TopDownArt.CreateColorPlane(root, "East Roof Trim", new Vector3(sizeX * 0.48f, -0.037f, 0f), new Vector2(0.13f, sizeZ), trim, 5);
            TopDownArt.CreateWorldSprite(root, "Chimney", "Tiles/tile_129", new Vector3(sizeX * 0.27f, -0.026f, sizeZ * 0.18f), new Vector2(0.52f, 0.52f), 6, -8f);
            TopDownArt.CreateColorPlane(root, "Porch", new Vector3(0f, -0.025f, -sizeZ * 0.49f), new Vector2(sizeX * 0.34f, 0.34f), new Color(0.62f, 0.39f, 0.16f), 6);
        }

        private void DrawPond(string id, Vector3 center, float sizeX, float sizeZ)
        {
            Transform root = VisualFactory.Empty(transform, id, center);
            TopDownArt.CreateColorPlane(root, "Pond Bank", new Vector3(0f, -0.068f, 0f), new Vector2(sizeX * 1.1f, sizeZ * 1.1f), new Color(0.28f, 0.42f, 0.24f), 2);
            TopDownArt.CreateTiledPlane(root, "Illustrated Water", "Tiles/tile_19", new Vector3(0f, -0.052f, 0f), new Vector2(sizeX, sizeZ), 0.64f, 3, new Color(0.78f, 0.94f, 1f));
            for (int index = 0; index < 10; index++)
            {
                float angle = index / 10f * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * sizeX * 0.51f;
                float z = Mathf.Sin(angle) * sizeZ * 0.51f;
                TopDownArt.CreateWorldSprite(root, $"Bank Stone {index}", "Tiles/tile_237", new Vector3(x, -0.035f, z), new Vector2(0.28f, 0.24f), 4, index * 31f);
            }
        }

        private void DrawTree(string id, Vector3 center, float sizeX, float sizeZ)
        {
            Transform root = VisualFactory.Empty(transform, id, center);
            float canopy = Mathf.Max(0.78f, Mathf.Max(sizeX, sizeZ) * 1.3f);
            TopDownArt.CreateWorldSprite(root, "Tree Shadow", "Tiles/tile_243", new Vector3(0.1f, -0.055f, -0.1f), new Vector2(canopy * 0.95f, canopy * 0.72f), 2, 0f, new Color(0.04f, 0.08f, 0.05f, 0.42f));
            TopDownArt.CreateWorldSprite(root, "Tree Canopy", "Tiles/tile_183", new Vector3(0f, -0.026f, 0f), new Vector2(canopy, canopy), 5, 0f, new Color(0.88f, 1f, 0.8f));
            TopDownArt.CreateWorldSprite(root, "Tree Highlight", "Tiles/tile_235", new Vector3(-canopy * 0.18f, -0.018f, canopy * 0.2f), new Vector2(canopy * 0.32f, canopy * 0.32f), 6, 0f, new Color(0.82f, 1f, 0.76f, 0.72f));
        }

        private void DrawRock(string id, Vector3 center, float sizeX, float sizeZ)
        {
            Transform root = VisualFactory.Empty(transform, id, center);
            TopDownArt.CreateWorldSprite(root, "Rock Shadow", "Tiles/tile_243", new Vector3(0.07f, -0.055f, -0.08f), new Vector2(sizeX * 1.05f, sizeZ * 0.85f), 2, 0f, new Color(0.05f, 0.06f, 0.055f, 0.42f));
            TopDownArt.CreateWorldSprite(root, "Illustrated Rock", "Tiles/tile_239", new Vector3(0f, -0.03f, 0f), new Vector2(sizeX * 1.18f, sizeZ * 1.18f), 5, 18f);
        }

        private void DrawGarden(string id, Vector3 center, float sizeX, float sizeZ)
        {
            Transform root = VisualFactory.Empty(transform, id, center);
            TopDownArt.CreateTiledPlane(root, "Garden Soil", "Tiles/tile_13", new Vector3(0f, -0.054f, 0f), new Vector2(sizeX, sizeZ), 0.64f, 3, new Color(0.88f, 0.72f, 0.5f));
            Color fence = new Color(0.64f, 0.41f, 0.17f);
            TopDownArt.CreateColorPlane(root, "Garden Fence N", new Vector3(0f, -0.035f, sizeZ * 0.5f), new Vector2(sizeX, 0.1f), fence, 5);
            TopDownArt.CreateColorPlane(root, "Garden Fence S", new Vector3(0f, -0.035f, -sizeZ * 0.5f), new Vector2(sizeX, 0.1f), fence, 5);
            for (int index = -3; index <= 3; index++)
            {
                float x = index * sizeX / 8f;
                string plant = index % 2 == 0 ? "Tiles/tile_213" : "Tiles/tile_134";
                TopDownArt.CreateWorldSprite(root, $"Garden Plant {index}", plant, new Vector3(x, -0.022f, 0f), new Vector2(0.26f, 0.26f), 6, index * 27f);
            }
        }

        private void ClearChildren()
        {
            for (int index = transform.childCount - 1; index >= 0; index--)
            {
                Destroy(transform.GetChild(index).gameObject);
            }
        }
    }
}
