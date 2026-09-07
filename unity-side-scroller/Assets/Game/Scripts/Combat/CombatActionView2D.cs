using System.Collections.Generic;
using EasyGame.SideScroller.Core;
using UnityEngine;

namespace EasyGame.SideScroller.Combat
{
    /// <summary>Reusable code-native pixel ribbons; visual only, never a damage collider.</summary>
    public sealed class CombatActionView2D : MonoBehaviour
    {
        private static Material material;
        private readonly List<Vector3> vertices = new List<Vector3>(512);
        private readonly List<Color> colors = new List<Color>(512);
        private readonly List<int> triangles = new List<int>(768);
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private bool updated;

        public static CombatActionView2D Create(Transform owner)
        {
            var effect = new GameObject("Combat telegraph and strike");
            effect.transform.SetParent(owner, false);
            return effect.AddComponent<CombatActionView2D>();
        }

        public void Show(int id, int facing, float elapsed, Collider2D body, PlayerAvatarKind avatar = PlayerAvatarKind.Warrior)
        {
            EnsureMesh();
            updated = true;
            meshRenderer.enabled = true;
            vertices.Clear(); colors.Clear(); triangles.Clear();
            var definition = CombatActions2D.Get(id);
            Vector2 bodyCenter = body.bounds.center;
            Vector2 center = definition.Center(bodyCenter, facing);
            transform.position = center;
            bool monster = id >= CombatActions2D.PlayerActionCount;
            Color accent = monster ? new Color(1f, .35f, .15f) : avatar == PlayerAvatarKind.Slime
                ? new Color(.28f, .85f, 1f) : id == 2 ? new Color(.6f, .57f, 1f)
                : id == 3 ? new Color(1f, .63f, .19f) : new Color(.4f, .92f, .89f);
            float groundY = body.bounds.min.y - center.y + .035f;
            if (elapsed < definition.Windup)
            {
                float charge = Mathf.Clamp01(elapsed / definition.Windup);
                Color warning = accent; warning.a = monster ? .8f : .5f;
                Line(new Vector2(-definition.Size.x * .5f, groundY), new Vector2(definition.Size.x * .5f, groundY), .045f, warning);
                float half = definition.Size.x * .5f * charge;
                Line(new Vector2(-half, groundY + .05f), new Vector2(half, groundY + .05f), .045f, Color.white);
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * Mathf.PI * .4f + charge * 1.5f;
                    Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (.42f * (1f - charge) + .1f);
                    Diamond(new Vector2(bodyCenter.x - center.x, 0f) + offset, .035f, warning);
                }
            }
            else
            {
                float age = elapsed - definition.Windup;
                float life = Mathf.Clamp01(age / (definition.Active + definition.Recovery));
                accent.a = 1f - life;
                if (id == 3 || id == 5)
                {
                    float radius = definition.Size.x * .5f * Mathf.Lerp(.78f, 1f, life);
                    Arc(Vector2.zero, radius, definition.Size.y * .43f, 0f, 360f, .09f * (1f - life) + .025f, accent);
                    for (int i = 0; i < 12; i++)
                    {
                        float angle = i * Mathf.PI / 6f;
                        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * .5f);
                        Line(direction * radius * .7f, direction * radius, .055f * (1f - life) + .015f, accent);
                    }
                }
                else
                {
                    float start = id == 2 ? -100f : -75f;
                    float sweep = id == 2 ? 220f : 150f;
                    // Facing is incorporated in geometry, never in the actor's scale.
                    Arc(new Vector2(-facing * definition.Size.x * .34f, 0f), definition.Size.x * .66f,
                        definition.Size.y * .44f, start, start + sweep, .13f * (1f - life) + .025f, accent, facing);
                    Color core = Color.Lerp(accent, Color.white, .82f); core.a = accent.a;
                    Arc(new Vector2(-facing * definition.Size.x * .34f, 0f), definition.Size.x * .64f,
                        definition.Size.y * .41f, start + 9f, start + sweep - 12f, .035f, core, facing);
                }
                for (int i = 0; i < 9; i++)
                {
                    float angle = i * 2.39996f;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 point = direction * (life * .65f + .2f);
                    point.x += facing * .15f;
                    Diamond(point, .045f * (1f - life) + .012f, accent);
                }
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        private void LateUpdate()
        {
            if (meshRenderer != null && !updated) meshRenderer.enabled = false;
            updated = false;
        }

        private void EnsureMesh()
        {
            if (mesh != null) return;
            if (material == null) material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            mesh = new Mesh { name = "Pixel combat ribbon" }; mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = gameObject.AddComponent<MeshRenderer>(); meshRenderer.sharedMaterial = material;
            meshRenderer.sortingOrder = 15;
        }

        private void Arc(Vector2 center, float rx, float ry, float start, float end, float width, Color color, int facing = 1)
        {
            const int steps = 20;
            for (int i = 0; i < steps; i++)
            {
                float a = Mathf.Lerp(start, end, i / (float)steps) * Mathf.Deg2Rad;
                float b = Mathf.Lerp(start, end, (i + 1f) / steps) * Mathf.Deg2Rad;
                Line(center + new Vector2(facing * Mathf.Cos(a) * rx, Mathf.Sin(a) * ry),
                    center + new Vector2(facing * Mathf.Cos(b) * rx, Mathf.Sin(b) * ry), width, color);
            }
        }

        private void Diamond(Vector2 center, float size, Color color) =>
            Quad(center + Vector2.up * size * 2f, center + Vector2.right * size, center + Vector2.down * size * 2f, center + Vector2.left * size, color);

        private void Line(Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 normal = Vector2.Perpendicular((to - from).normalized) * (width * .5f);
            Quad(from - normal, from + normal, to + normal, to - normal, color);
        }

        private void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int index = vertices.Count;
            vertices.Add(Snap(a)); vertices.Add(Snap(b)); vertices.Add(Snap(c)); vertices.Add(Snap(d));
            for (int i = 0; i < 4; i++) colors.Add(color);
            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
            triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
        }
        private static Vector3 Snap(Vector2 value) => new Vector3(Mathf.Round(value.x * 64f) / 64f, Mathf.Round(value.y * 64f) / 64f, 0f);
        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
