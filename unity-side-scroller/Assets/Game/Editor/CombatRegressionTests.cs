using System;
using System.Reflection;
using EasyGame.SideScroller.Combat;
using EasyGame.SideScroller.Core;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EasyGame.SideScroller.Editor
{
    public static class CombatRegressionTests
    {
        public static void Run()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            scene.name = "Combat QA";
            try
            {
                var query = new CombatQuery2D();
                var attacker = Actor(scene, "Attacker", Vector2.zero);
                var target = Actor(scene, "Target", Vector2.right);
                var extraHitbox = new GameObject("Compound hitbox");
                extraHitbox.transform.SetParent(target.transform, false);
                extraHitbox.AddComponent<CircleCollider2D>().radius = 0.2f;
                Physics2D.SyncTransforms();
                var hits = query.CollectTargets(attacker, Vector2.right * 0.7f, new Vector2(2f, 2f));
                Check(hits.Count == 1 && hits[0].attachedRigidbody == target.attachedRigidbody, "each compound actor hit once; self excluded");

                var wall = new GameObject("Wall");
                SceneManager.MoveGameObjectToScene(wall, scene);
                wall.transform.position = Vector2.right * 0.5f;
                var wallCollider = wall.AddComponent<BoxCollider2D>();
                wallCollider.size = new Vector2(0.1f, 3f);
                Physics2D.SyncTransforms();
                Check(query.CollectTargets(attacker, Vector2.right, new Vector2(2f, 2f)).Count == 0, "solid terrain blocks melee");
                wallCollider.isTrigger = true;
                Physics2D.SyncTransforms();
                Check(query.CollectTargets(attacker, Vector2.right, new Vector2(2f, 2f)).Count == 1, "trigger volumes do not block melee");

                target.attachedRigidbody.simulated = false;
                Physics2D.SyncTransforms();
                Check(query.CollectTargets(attacker, Vector2.right, new Vector2(2f, 2f)).Count == 0, "disabled/dead actor cannot be hit");
                attacker.enabled = false;
                Check(query.CollectTargets(attacker, Vector2.right, new Vector2(2f, 2f)).Count == 0, "disabled attacker cannot attack");

                foreach (var avatar in new[] { PlayerAvatarKind.Warrior, PlayerAvatarKind.Ranger, PlayerAvatarKind.Slime })
                {
                    Transform visual = RuntimePlayerVisual.CreatePlayer(target.transform, avatar, Color.white);
                    var adapter = visual.GetComponent<PlayerAvatarAnimator>();
                    var renderer = visual.GetComponentInChildren<SpriteRenderer>();
                    Vector3 anchor = visual.localPosition;
                    for (int motion = 0; motion <= 6; motion++)
                    {
                        adapter.SetState(motion, -1, 5f);
                        TickAnimator(visual);
                        Check(renderer.sprite != null, avatar + " has a real sprite for motion " + motion);
                        Check(visual.localPosition == anchor, "animation preserves collision feet anchor");
                        Check(Mathf.Abs(visual.localScale.x) == 1f && visual.localScale.y == 1f, "animation does not change actor size");
                    }
                    adapter.PlayAttack(1f);
                    TickAnimator(visual);
                    Check(renderer.sprite != null, "explicit attack event restarts a valid strip");
                    UnityEngine.Object.DestroyImmediate(visual.gameObject);
                }
                Debug.Log("COMBAT_REGRESSION_PASS: 5 collision-query scenarios and 21 avatar motion states passed.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static BoxCollider2D Actor(Scene scene, string name, Vector2 position)
        {
            var actor = new GameObject(name);
            SceneManager.MoveGameObjectToScene(actor, scene);
            actor.transform.position = position;
            actor.AddComponent<Rigidbody2D>().gravityScale = 0f;
            var collider = actor.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.4f, 1f);
            return collider;
        }

        private static void TickAnimator(Transform visual)
        {
            foreach (MonoBehaviour component in visual.GetComponents<MonoBehaviour>())
                component.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Combat regression: " + message);
        }
    }
}
