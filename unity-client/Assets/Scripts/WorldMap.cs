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

            VisualFactory.Box(transform, "Ground", new Vector3(width * 0.5f, -0.09f, height * 0.5f), new Vector3(width, 0.18f, height), new Color(0.16f, 0.24f, 0.13f));
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
            Color[] colors =
            {
                new Color(0.19f, 0.29f, 0.15f),
                new Color(0.13f, 0.21f, 0.12f),
                new Color(0.23f, 0.27f, 0.14f),
                new Color(0.17f, 0.25f, 0.18f)
            };
            int detailCount = Mathf.Clamp(Mathf.RoundToInt(width * height * 0.42f), 120, 360);
            for (int index = 0; index < detailCount; index++)
            {
                float x = 0.3f + (float)random.NextDouble() * (width - 0.6f);
                float z = 0.3f + (float)random.NextDouble() * (height - 0.6f);
                float size = 0.08f + (float)random.NextDouble() * 0.22f;
                Transform patch = VisualFactory.Box(transform, $"Ground Detail {index}", new Vector3(x, 0.006f, z), new Vector3(size * 1.8f, 0.012f, size), colors[index % colors.Length]);
                patch.localRotation = Quaternion.Euler(0f, random.Next(0, 180), 0f);
            }
        }

        private void DrawBoundary()
        {
            Color wall = new Color(0.22f, 0.2f, 0.14f);
            VisualFactory.Box(transform, "North Wall", new Vector3(width * 0.5f, 0.34f, height + 0.08f), new Vector3(width + 0.3f, 0.68f, 0.16f), wall);
            VisualFactory.Box(transform, "South Wall", new Vector3(width * 0.5f, 0.34f, -0.08f), new Vector3(width + 0.3f, 0.68f, 0.16f), wall);
            VisualFactory.Box(transform, "West Wall", new Vector3(-0.08f, 0.34f, height * 0.5f), new Vector3(0.16f, 0.68f, height), wall);
            VisualFactory.Box(transform, "East Wall", new Vector3(width + 0.08f, 0.34f, height * 0.5f), new Vector3(0.16f, 0.68f, height), wall);
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
            Color timber = new Color(0.34f, 0.21f, 0.1f);
            VisualFactory.Box(root, "Foundation", new Vector3(0f, 0.4f, 0f), new Vector3(sizeX, 0.8f, sizeZ), timber);
            VisualFactory.Box(root, "FrontTrim", new Vector3(0f, 0.45f, -sizeZ * 0.51f), new Vector3(sizeX * 0.9f, 0.11f, 0.08f), new Color(0.55f, 0.37f, 0.16f));
            Transform roof = VisualFactory.Box(root, "Roof", new Vector3(0f, 0.93f, 0f), new Vector3(sizeX * 1.12f, 0.18f, sizeZ * 1.15f), new Color(0.2f, 0.16f, 0.12f));
            roof.localRotation = Quaternion.Euler(0f, 0f, 2f);
            for (int index = -3; index <= 3; index++)
            {
                float z = index * sizeZ * 0.14f;
                VisualFactory.Box(root, $"Roof Seam {index}", new Vector3(0f, 1.035f, z), new Vector3(sizeX * 1.08f, 0.018f, 0.035f), new Color(0.42f, 0.27f, 0.13f));
            }
            VisualFactory.Box(root, "Chimney", new Vector3(sizeX * 0.29f, 1.22f, sizeZ * 0.12f), new Vector3(0.28f, 0.55f, 0.3f), new Color(0.24f, 0.19f, 0.15f));
            VisualFactory.Box(root, "Door", new Vector3(0f, 0.36f, -sizeZ * 0.52f), new Vector3(0.46f, 0.62f, 0.07f), new Color(0.18f, 0.11f, 0.06f));
            VisualFactory.Box(root, "Window", new Vector3(-sizeX * 0.27f, 0.53f, -sizeZ * 0.53f), new Vector3(0.38f, 0.32f, 0.06f), new Color(0.32f, 0.55f, 0.58f));
        }

        private void DrawPond(string id, Vector3 center, float sizeX, float sizeZ)
        {
            Transform root = VisualFactory.Empty(transform, id, center);
            VisualFactory.Cylinder(root, "Water", new Vector3(0f, 0.015f, 0f), new Vector3(sizeX * 0.5f, 0.025f, sizeZ * 0.5f), new Color(0.13f, 0.37f, 0.4f));
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 0.25f;
                Vector3 edge = new Vector3(Mathf.Cos(angle) * sizeX * 0.48f, 0.06f, Mathf.Sin(angle) * sizeZ * 0.48f);
                VisualFactory.Sphere(root, $"Bank Stone {i}", edge, new Vector3(0.34f, 0.13f, 0.24f), new Color(0.29f, 0.3f, 0.24f));
            }
        }

        private void DrawTree(string id, Vector3 center, float sizeX, float sizeZ)
        {
            Transform root = VisualFactory.Empty(transform, id, center);
            VisualFactory.Cylinder(root, "Trunk", new Vector3(0f, 0.45f, 0f), new Vector3(0.15f, 0.45f, 0.15f), new Color(0.28f, 0.16f, 0.07f));
            Color leaves = new Color(0.12f, 0.29f, 0.11f);
            VisualFactory.Sphere(root, "Crown", new Vector3(0f, 1.05f, 0f), new Vector3(Mathf.Max(0.65f, sizeX * 1.08f), 0.72f, Mathf.Max(0.65f, sizeZ * 0.86f)), leaves);
            VisualFactory.Sphere(root, "Crown Light", new Vector3(-0.18f, 1.2f, -0.08f), new Vector3(0.48f, 0.44f, 0.48f), new Color(0.19f, 0.38f, 0.14f));
        }

        private void DrawRock(string id, Vector3 center, float sizeX, float sizeZ)
        {
            Transform root = VisualFactory.Empty(transform, id, center);
            Transform rock = VisualFactory.Sphere(root, "Rock", new Vector3(0f, 0.2f, 0f), new Vector3(sizeX * 1.08f, 0.38f, sizeZ * 1.08f), new Color(0.31f, 0.32f, 0.27f));
            rock.localRotation = Quaternion.Euler(0f, 27f, 7f);
            VisualFactory.Box(root, "Rock Face", new Vector3(-0.08f, 0.27f, -0.2f), new Vector3(0.25f, 0.18f, 0.05f), new Color(0.4f, 0.4f, 0.33f));
        }

        private void DrawGarden(string id, Vector3 center, float sizeX, float sizeZ)
        {
            Transform root = VisualFactory.Empty(transform, id, center);
            VisualFactory.Box(root, "Soil", new Vector3(0f, 0.02f, 0f), new Vector3(sizeX, 0.04f, sizeZ), new Color(0.25f, 0.15f, 0.07f));
            Color fence = new Color(0.5f, 0.34f, 0.15f);
            VisualFactory.Box(root, "Fence N", new Vector3(0f, 0.22f, sizeZ * 0.5f), new Vector3(sizeX, 0.24f, 0.08f), fence);
            VisualFactory.Box(root, "Fence S", new Vector3(0f, 0.22f, -sizeZ * 0.5f), new Vector3(sizeX, 0.24f, 0.08f), fence);
            for (int i = -3; i <= 3; i++)
            {
                float x = i * sizeX / 8f;
                VisualFactory.Sphere(root, $"Plant {i}", new Vector3(x, 0.15f, 0f), new Vector3(0.14f, 0.2f, 0.14f), new Color(0.25f, 0.48f, 0.12f));
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
