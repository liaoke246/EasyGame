using System.Collections.Generic;
using EasyGame.SideScroller.Core;
using UnityEngine;

namespace EasyGame.SideScroller.Combat
{
    /// <summary>Layered pixel VFX driven by combat phases. Never owns a damage collider.</summary>
    public sealed class CombatActionView2D : MonoBehaviour
    {
        private static Material material;
        private readonly List<Vector3> vertices = new List<Vector3>(8192);
        private readonly List<Color> colors = new List<Color>(8192);
        private readonly List<int> triangles = new List<int>(12288);
        private readonly Impact[] impacts = new Impact[12];
        private int nextImpact;
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private bool updated;
        private struct Impact { public bool Used; public Vector2 Position; public float StartedAt; public int Facing; public Color Color; }

        public static CombatActionView2D Create(Transform owner)
        {
            var effect = new GameObject("Combat telegraph and strike");
            effect.transform.SetParent(owner, false);
            return effect.AddComponent<CombatActionView2D>();
        }

        // Server-confirmed damage only. Fixed slots avoid particle object churn.
        public void ConfirmImpact(Vector2 position, int id, int facing, PlayerAvatarKind avatar)
        {
            impacts[nextImpact] = new Impact { Used = true, Position = position, StartedAt = Time.time, Facing = facing, Color = Accent(id, avatar) };
            nextImpact = (nextImpact + 1) % impacts.Length;
        }

        public void Show(int id, int facing, float elapsed, Collider2D body, PlayerAvatarKind avatar = PlayerAvatarKind.Warrior)
        {
            ClearGeometry(); updated = true;
            if (!CombatActions2D.IsValid(id) || body == null) return;
            var action = CombatActions2D.Get(id);
            // A late snapshot must not leave a frozen arc or replay a burst.
            if (elapsed < 0f || elapsed >= action.Duration) return;
            facing = facing < 0 ? -1 : 1;
            Vector2 center = action.Center(body.bounds.center, facing);
            transform.position = center;
            float ground = body.bounds.min.y - center.y + .035f;
            Color color = Accent(id, avatar);
            if (id >= CombatActions2D.PlayerActionCount) Monster(id, facing, elapsed, action, ground, color);
            else if (elapsed < action.Windup) Anticipation(id, facing, elapsed / action.Windup, action, ground, color);
            else
            {
                float life = Mathf.Clamp01((elapsed - action.Windup) / (action.Active + action.Recovery));
                if (id == (int)CombatActionId.Nova) GroundBurst(life, action, ground, color);
                else Blade(id, facing, life, action, color);
            }
        }

        private static Color Accent(int id, PlayerAvatarKind avatar)
        {
            if (id >= CombatActions2D.PlayerActionCount) return new Color(1f, .38f, .2f);
            if (avatar == PlayerAvatarKind.Slime) return new Color(.30f, .78f, 1f);
            return id == 2 ? new Color(.68f, .54f, 1f) : id == 3 ? new Color(1f, .70f, .25f) : new Color(.34f, .88f, .78f);
        }

        private void Anticipation(int id, int facing, float charge, CombatActionDefinition action, float ground, Color color)
        {
            if (id == 3)
            {
                // Segmented ground seal leaves the character silhouette clear.
                float radius = action.Size.x * .5f;
                Ellipse(new Vector2(0f, ground), radius, .15f, .028f, Alpha(color, .24f + charge * .25f), true);
                Ellipse(new Vector2(0f, ground + .02f), radius * charge, .10f * charge, .032f, Alpha(color, .65f));
                for (int i = 0; i < 9; i++)
                {
                    float x = Mathf.Lerp(-radius, radius, i / 8f);
                    Line(new Vector2(x, ground), new Vector2(x, ground + .07f), .025f, Alpha(color, .6f));
                }
            }
            if (id == 0) return;
            Vector2 grip = new Vector2(-facing * action.Offset + facing * .28f, ground + .8f);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f + .35f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 end = grip + direction * Mathf.Lerp(.6f, .1f, charge);
                Line(end + direction * .09f, end, .025f, Alpha(color, charge * .65f));
                Diamond(end, .018f + charge * .012f, Alpha(Color.Lerp(color, Color.white, .7f), charge));
            }
            Star(grip, .06f + charge * .07f, Alpha(color, charge * .8f));
        }

        private void Blade(int id, int facing, float life, CombatActionDefinition action, Color color)
        {
            bool rising = id == 2;
            float fade = Mathf.Pow(1f - life, 1.45f), travel = 1f - Mathf.Pow(1f - life, 3f);
            Vector2 anchor = new Vector2(-facing * action.Size.x * .39f, 0f);
            float rx = action.Size.x * .88f, ry = action.Size.y * .47f;
            float from = rising ? -108f + travel * 45f : -74f - travel * 30f;
            float to = rising ? 90f + travel * 45f : 80f - travel * 30f;
            float thickness = id == 0 ? .11f : rising ? .27f : .30f;
            // Tapered back-edge, translucent body, colored edge and hot core.
            Ribbon(anchor, rx, ry, from, to, thickness * 1.24f, Alpha(new Color(.09f, .18f, .23f), fade * .5f), facing);
            Ribbon(anchor, rx * .98f, ry * .98f, from, to, thickness, Alpha(color, fade * .52f), facing);
            Ribbon(anchor, rx, ry, from + 8f, to - 3f, thickness * .30f, Alpha(color, fade), facing);
            Ribbon(anchor, rx * .975f, ry * .975f, from + 16f, to - 7f, thickness * .12f, Alpha(new Color(1f, .99f, .86f), fade), facing);
            if (id > 0)
            {
                Ribbon(anchor, rx * .83f, ry * .83f, from - 12f, to - 25f, .055f, Alpha(color, fade * .42f), facing);
                Ribbon(anchor, rx * .68f, ry * .71f, from - 20f, to - 42f, .025f, Alpha(color, fade * .30f), facing);
            }
            for (int i = 0; i < (id == 0 ? 4 : 10); i++)
            {
                float t = i / 9f, angle = Mathf.Lerp(from + 10f, to - 10f, t) * Mathf.Deg2Rad;
                Vector2 point = anchor + new Vector2(facing * Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry) * (.8f + .17f * t);
                Vector2 drift = rising ? new Vector2(facing * .1f, .5f) : new Vector2(facing * .45f, -.12f);
                point += drift * life;
                Line(point - drift * .20f, point, .025f, Alpha(color, fade * .55f));
                if (i % 3 == 0) Diamond(point, .03f * fade, Alpha(Color.white, fade * .8f));
            }
        }

        private void GroundBurst(float life, CombatActionDefinition action, float ground, Color color)
        {
            float fade = Mathf.Pow(1f - life, 1.4f), radius = action.Size.x * .5f;
            float expansion = 1f - Mathf.Pow(1f - life, 4f);
            Vector2 origin = new Vector2(0f, ground);
            // Full-range contact flash first. Following waves dissipate energy;
            // they never imply a delayed or separately damaging projectile.
            Line(new Vector2(-radius, ground), new Vector2(radius, ground), .13f * fade, Alpha(color, fade * .65f));
            Line(new Vector2(-radius, ground + .025f), new Vector2(radius, ground + .025f), .025f, Alpha(Color.white, fade));
            Ellipse(origin, radius, .15f, .07f * fade, Alpha(color, fade * .7f));
            Ellipse(origin + Vector2.up * .03f, radius * (.62f + expansion * .38f), .13f, .028f, Alpha(Color.white, fade * .6f));
            for (int i = 0; i < 13; i++)
            {
                float x = Mathf.Lerp(-radius * .9f, radius * .9f, i / 12f), seed = Hash(i);
                float height = (.24f + seed * .68f) * (1f - life * .72f), width = .05f + seed * .05f;
                Vector2 basePoint = new Vector2(x, ground);
                Vector2 tip = basePoint + new Vector2(Mathf.Sign(x) * .12f * expansion, height);
                Quad(basePoint - Vector2.right * width, basePoint + Vector2.right * width, tip + Vector2.right * .013f, tip, Alpha(color, fade * .42f));
                Line(basePoint, tip, .025f, Alpha(Color.Lerp(color, Color.white, .5f), fade * .9f));
                Diamond(new Vector2(x, ground + .10f + life * (.45f + seed * .7f)), .027f * fade, Alpha(color, fade));
                Vector2 dirt = new Vector2(x + Mathf.Sign(x) * life * .12f, ground + Mathf.Max(0f, .12f + life * (.7f + seed) - life * life * 1.5f));
                Diamond(dirt, .035f * fade, Alpha(new Color(.50f, .41f, .29f), fade));
            }
        }

        private void Monster(int id, int facing, float elapsed, CombatActionDefinition action, float ground, Color color)
        {
            if (elapsed < action.Windup)
            {
                float charge = elapsed / action.Windup, half = action.Size.x * .5f;
                // Orange tracks; red commits. Warning and actual strike share facing.
                Color warning = elapsed >= action.Windup - CombatActions2D.MonsterAimCommitLead ? new Color(1f, .20f, .12f) : new Color(1f, .68f, .24f);
                Line(new Vector2(-half, ground), new Vector2(half, ground), .05f, Alpha(warning, .7f));
                Line(new Vector2(-half, ground + .04f), new Vector2(Mathf.Lerp(-half, half, charge), ground + .04f), .025f, Alpha(Color.white, .7f));
                for (int side = -1; side <= 1; side += 2)
                    Line(new Vector2(side * half, ground), new Vector2(side * half, ground + .12f), .035f, warning);
                return;
            }
            float life = Mathf.Clamp01((elapsed - action.Windup) / (action.Active + action.Recovery));
            float fade = (1f - life) * (1f - life);
            if (id == 5)
            {
                Ellipse(new Vector2(0f, ground), action.Size.x * .5f, .12f, .045f, Alpha(color, fade));
                for (int i = 0; i < 7; i++) Diamond(new Vector2(Mathf.Lerp(-.7f, .7f, i / 6f), ground + life * .55f), .04f * fade, Alpha(color, fade));
            }
            else for (int i = 0; i < 3; i++)
                Ribbon(new Vector2(-facing * .4f, .14f - i * .17f), .93f, .4f, -65f - life * 25f, 60f - life * 25f, .055f, Alpha(color, fade), facing);
        }

        private void LateUpdate()
        {
            if (!updated) ClearGeometry();
            updated = false;
            for (int i = 0; i < impacts.Length; i++)
            {
                if (!impacts[i].Used) continue;
                float age = Time.time - impacts[i].StartedAt;
                if (age >= .34f) { impacts[i].Used = false; continue; }
                float life = Mathf.Clamp01(age / .34f), fade = 1f - life;
                // Anchored in world space, not dragged along with the attacker.
                Vector2 center = impacts[i].Position - (Vector2)transform.position;
                if (age < .09f) Star(center, .22f * (1f - age / .09f), Alpha(new Color(1f, .99f, .85f), .95f));
                for (int ray = 0; ray < 9; ray++)
                {
                    float angle = ray * 2.39996f;
                    Vector2 dir = new Vector2(Mathf.Cos(angle) * impacts[i].Facing, Mathf.Sin(angle));
                    Vector2 point = center + dir * (.08f + life * (.32f + Hash(ray) * .32f));
                    Line(point - dir * .12f * fade, point, .03f * fade, Alpha(impacts[i].Color, fade));
                    if (ray % 2 == 0) Diamond(point, .025f * fade, Alpha(Color.white, fade));
                }
            }
            if (vertices.Count == 0) { if (meshRenderer != null) meshRenderer.enabled = false; return; }
            EnsureMesh(); meshRenderer.enabled = true;
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        }

        private void ClearGeometry() { vertices.Clear(); colors.Clear(); triangles.Clear(); }
        private void EnsureMesh()
        {
            if (mesh != null) return;
            if (material == null) material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            mesh = new Mesh { name = "Layered pixel combat" }; mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = gameObject.AddComponent<MeshRenderer>(); meshRenderer.sharedMaterial = material; meshRenderer.sortingOrder = 15;
        }
        private void Ribbon(Vector2 center, float rx, float ry, float start, float end, float width, Color color, int facing)
        {
            const int steps = 40;
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)steps, next = (i + 1f) / steps;
                float a = Mathf.Lerp(start, end, t) * Mathf.Deg2Rad, b = Mathf.Lerp(start, end, next) * Mathf.Deg2Rad;
                Vector2 da = new Vector2(facing * Mathf.Cos(a), Mathf.Sin(a)), db = new Vector2(facing * Mathf.Cos(b), Mathf.Sin(b));
                Vector2 p = center + Vector2.Scale(da, new Vector2(rx, ry)), q = center + Vector2.Scale(db, new Vector2(rx, ry));
                float wa = width * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), .75f);
                float wb = width * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(next * Mathf.PI)), .75f);
                Color shade = color; shade.a *= Mathf.Lerp(.15f, 1f, Mathf.Sin(t * Mathf.PI));
                Quad(p - da * wa, p, q, q - db * wb, shade);
            }
        }
        private void Ellipse(Vector2 center, float rx, float ry, float width, Color color, bool segmented = false)
        {
            const int steps = 48;
            for (int i = 0; i < steps; i++)
            {
                if (segmented && i % 4 == 3) continue;
                float a = i * Mathf.PI * 2f / steps, b = (i + 1f) * Mathf.PI * 2f / steps;
                Line(center + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry), center + new Vector2(Mathf.Cos(b) * rx, Mathf.Sin(b) * ry), width, color);
            }
        }
        private void Star(Vector2 center, float size, Color color)
        {
            Diamond(center, size, color);
            Quad(center + Vector2.left * size * 2f, center + Vector2.up * size * .35f, center + Vector2.right * size * 2f, center + Vector2.down * size * .35f, color);
        }
        private void Diamond(Vector2 center, float size, Color color) =>
            Quad(center + Vector2.up * size * 1.5f, center + Vector2.right * size, center + Vector2.down * size * 1.5f, center + Vector2.left * size, color);
        private void Line(Vector2 from, Vector2 to, float width, Color color)
        {
            if (width <= .001f || (to - from).sqrMagnitude < .00001f) return;
            Vector2 normal = Vector2.Perpendicular((to - from).normalized) * (width * .5f);
            Quad(from - normal, from + normal, to + normal, to - normal, color);
        }
        private void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            if (color.a <= .005f) return;
            int index = vertices.Count;
            vertices.Add(Snap(a)); vertices.Add(Snap(b)); vertices.Add(Snap(c)); vertices.Add(Snap(d));
            for (int i = 0; i < 4; i++) colors.Add(color);
            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2); triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
        }
        private static Color Alpha(Color color, float alpha) { color.a = Mathf.Clamp01(alpha); return color; }
        private static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i * 127.1f + 31.7f) * 43758.5453f, 1f);
        private static Vector3 Snap(Vector2 value) => new Vector3(Mathf.Round(value.x * 64f) / 64f, Mathf.Round(value.y * 64f) / 64f, 0f);
        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
