using System;
using System.Collections.Generic;
using System.Reflection;
using EasyGame.SideScroller.Combat;
using EasyGame.SideScroller.Core;
using EasyGame.SideScroller.Enemies;
using EasyGame.SideScroller.Network;
using EasyGame.SideScroller.Player;
using EasyGame.SideScroller.World;
using Mirror;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EasyGame.SideScroller.Editor
{
    public static class SkillRegressionTests
    {
        public static void Run()
        {
            CheckClocksAndInput();
            CheckServerCombat();
            CheckPresentation();
            Debug.Log("SKILL_REGRESSION_PASS: 6 action timelines; 3 skill inputs; telegraph/dodge/single-hit/interrupt/death; skill damage, PvP, launch, cooldown and all avatar action frames.");
        }

        private static void CheckClocksAndInput()
        {
            for (int id = 0; id < CombatActions2D.Count; id++)
            {
                var clock = new CombatActionClock();
                var definition = CombatActions2D.Get(id);
                Check(clock.TryBegin(id, -1, true, 10d), "valid action starts");
                Check(clock.Facing == -1, "direction locks at anticipation");
                Check(!clock.ConsumeHit(10d + definition.Windup - .001d), "windup cannot damage");
                Check(clock.ConsumeHit(10d + definition.Windup + .001d), "active phase hits");
                Check(!clock.ConsumeHit(10d + definition.Windup + .002d), "same action cannot hit twice");
                Check(!clock.TryBegin(0, 1, true, 10d + definition.Windup), "cannot replace action before recovery");
                clock.Cancel();
                Check(!clock.ConsumeHit(10d + definition.Windup + .003d), "cancel removes pending hit");
                Check(!clock.TryBegin(id, 1, true, 10.01d), "interrupt does not refund cooldown");
                Check(clock.TryBegin(id, 1, true, 10d + definition.Cooldown + .01d), "action available after cooldown");
                Check(!clock.ConsumeHit(100d), "expired strike cannot hit after suspended simulation");
                Check(definition.Frame(definition.Windup + .001f, 6) == 4, "contact uses the authored blade slash, not the raised-sword frame");
                for (int frame = 0; frame < 20; frame++)
                {
                    int index = definition.Frame(frame / 19f * definition.Duration, 6);
                    Check(index >= 0 && index < 6, "phase frame stays in range");
                }
            }
            var grounded = new CombatActionClock();
            Check(!grounded.TryBegin(3, 1, false, 1d) && grounded.ReadyAt(3) == 0d, "airborne nova denied without spending cooldown");
            Check(!grounded.TryBegin(99, 1, true, 1d) && !grounded.TryBegin(1, 1, true, double.NaN), "invalid action and time denied");
            var bridgeObject = new GameObject("Skill input QA");
            try
            {
                var bridge = bridgeObject.AddComponent<MobileInputBridge>();
                for (int id = 1; id <= 3; id++)
                {
                    MobileInputBridge.ResetState();
                    bridge.SetMobileControl("skill" + id + ":1");
                    Check(MobileInputBridge.ConsumeSkillPressed() == id, "touch skill edge received");
                    bridge.SetMobileControl("skill" + id + ":1");
                    Check(MobileInputBridge.ConsumeSkillPressed() == -1, "held touch cannot retrigger");
                    bridge.SetMobileControl("skill" + id + ":0");
                    bridge.SetMobileControl("skill" + id + ":1");
                    bridge.SetMobileControl("reset:1");
                    Check(MobileInputBridge.ConsumeSkillPressed() == -1, "focus reset discards skill edges");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(bridgeObject); }
        }

        private static void CheckServerCombat()
        {
            Check(!NetworkServer.active && !NetworkClient.active, "QA server must be isolated");
            Scene scene = EditorSceneManager.NewPreviewScene();
            Transport previous = Transport.active;
            bool previousListen = NetworkServer.listen;
            var actors = new List<NetworkBehaviour>();
            try
            {
                GameObject transport = Object(scene, "QA transport");
                Transport.active = transport.AddComponent<TelepathyTransport>();
                NetworkServer.listen = false; NetworkServer.Listen(0);
                var floor = Object(scene, "Floor").AddComponent<BoxCollider2D>();
                floor.transform.position = new Vector2(0f, SideWorldBuilder.FloorSurfaceY - .5f);
                floor.size = new Vector2(100f, 1f);
                float y = SideWorldBuilder.PlayerSpawn.y;
                var caster = LifecycleRegressionTests.Actor<SideScrollerNetworkPlayer>(scene, new Vector3(-10f, y), false);
                var opponent = LifecycleRegressionTests.Actor<SideScrollerNetworkPlayer>(scene, new Vector3(-8.8f, y), false);
                var zombie = LifecycleRegressionTests.Actor<SideScrollerNetworkZombie>(scene, new Vector3(5f, y), false);
                var slime = LifecycleRegressionTests.Actor<SideScrollerNetworkSlime>(scene,
                    ActorGeometry2D.RootPositionForFeet(new Vector3(10f, SideWorldBuilder.FloorSurfaceY), ActorGeometry2D.SlimeFeetLocalY), true);
                actors.Add(caster); actors.Add(opponent); actors.Add(zombie); actors.Add(slime);
                Set(caster, "invulnerableUntil", -1d); Set(opponent, "invulnerableUntil", -1d);
                Physics2D.SyncTransforms();
                Field<PlayerMotor2D>(caster, "motor").Step(0f, false, .02f);
                Check(Field<PlayerMotor2D>(caster, "motor").IsGrounded, "skill caster grounded on actual floor");

                foreach (int id in new[] { 1, 2, 3 })
                {
                    Field<CombatActionClock>(caster, "actionClock").Reset();
                    Set(caster, "actionLockedUntil", 0f); Set(opponent, "health", 100);
                    opponent.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    Invoke(caster, "BeginAttack", id);
                    Check(opponent.Health == 100, "skill start cannot damage before windup");
                    var clock = Field<CombatActionClock>(caster, "actionClock");
                    Check(clock.Action == id, "server accepted the requested skill");
                    typeof(CombatActionClock).GetProperty("StartedAt").SetValue(clock, NetworkTime.time - CombatActions2D.Get(id).Windup - .005d);
                    Invoke(caster, "FixedUpdate");
                    Check(opponent.Health == 100 - CombatActions2D.Get(id).PvpDamage, "server applies configured PvP damage");
                    Invoke(caster, "FixedUpdate");
                    Check(opponent.Health == 100 - CombatActions2D.Get(id).PvpDamage, "multiple physics ticks cannot repeat damage");
                    if (id == 2) Check(opponent.GetComponent<Rigidbody2D>().linearVelocity.y >= 5f, "rising strike really launches the rigidbody");
                    Check(caster.SkillRemaining(id) > 0f, "authoritative cooldown published to HUD");
                }

                // Interrupt a pending player cast before contact; no late phantom damage.
                var playerClock = Field<CombatActionClock>(caster, "actionClock");
                playerClock.Reset(); Set(caster, "actionLockedUntil", 0f); Set(opponent, "health", 100);
                Invoke(caster, "BeginAttack", 1);
                caster.ApplyDamage(1, null);
                Check(playerClock.Action == -1 && !playerClock.ConsumeHit(NetworkTime.time + 1d), "damage cancels the player cast");
                Check(opponent.Health == 100, "interrupted cast deals no damage");

                // Actual monster component enters the attack motion, and its
                // shared server query resolves only at the strike timestamp.
                CheckMonsterTurning(zombie, caster, 4);
                CheckMonsterTurning(slime, caster, 5);
                CheckMonster(zombie, caster, 4, 4);
                CheckMonster(slime, caster, 5, 7);

                foreach (var avatar in new[] { PlayerAvatarKind.Warrior, PlayerAvatarKind.Ranger, PlayerAvatarKind.Slime })
                {
                    Transform visual = RuntimePlayerVisual.CreatePlayer(opponent.transform, avatar, Color.white);
                    var adapter = visual.GetComponent<PlayerAvatarAnimator>();
                    Vector3 anchor = visual.localPosition;
                    for (int id = 0; id < 4; id++)
                    for (int phase = 0; phase <= 8; phase++)
                    {
                        adapter.SetCombatAction(id, CombatActions2D.Get(id).Duration * phase / 8f, phase % 2 == 0 ? 1 : -1);
                        foreach (var component in visual.GetComponents<MonoBehaviour>())
                            component.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
                        Check(visual.GetComponentInChildren<SpriteRenderer>().sprite != null, "every skill phase uses a real frame");
                        if (avatar != PlayerAvatarKind.Slime)
                        {
                            var renderer = visual.GetComponentInChildren<SpriteRenderer>();
                            Check(renderer.sharedMaterial.shader.name == "EasyGame/Combat Sprite", "character uses authored-slash separation shader");
                            var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
                            bool bakedFx = renderer.sprite.name.EndsWith("attack_4", StringComparison.Ordinal) || renderer.sprite.name.EndsWith("attack_5", StringComparison.Ordinal);
                            Check((properties.GetFloat("_SeparateSlash") > .5f) == bakedFx, "separate baked VFX on both contact and recovery tail frames");
                        }
                        Check(visual.localPosition == anchor && Mathf.Abs(visual.localScale.x) == 1f && visual.localScale.y == 1f, "skill preserves feet and body scale");
                    }
                    UnityEngine.Object.DestroyImmediate(visual.gameObject);
                }
            }
            finally
            {
                foreach (var actor in actors) { if (actor == null) continue; NetworkServer.UnSpawn(actor.gameObject); UnityEngine.Object.DestroyImmediate(actor.gameObject); }
                if (NetworkServer.active) NetworkServer.Shutdown();
                Transport.active = previous; NetworkServer.listen = previousListen;
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void CheckMonster(NetworkBehaviour monster, SideScrollerNetworkPlayer victim, int id, int motion)
        {
            var body = monster.GetComponent<Collider2D>();
            var melee = Field<MonsterMelee2D>(monster, "melee");
            var victimBody = victim.GetComponent<Rigidbody2D>();
            victimBody.position = new Vector2(body.bounds.center.x + .72f, SideWorldBuilder.PlayerSpawn.y);
            victimBody.linearVelocity = Vector2.zero;
            Set(victim, "health", 100); Set(victim, "invulnerableUntil", -1d);
            Set(monster, "nextContactDamageAt", -1d); Set(monster, "staggerUntil", -1d);
            melee.Clock.Reset(); Physics2D.SyncTransforms();
            Invoke(monster, "FixedUpdate");
            Check(Field<int>(monster, "motion") == motion && victim.Health == 100, "monster visibly anticipates before damage");
            double start = melee.Clock.StartedAt;
            int facing = melee.Clock.Facing;
            victimBody.position += Vector2.right * 4f; Physics2D.SyncTransforms();
            melee.Step(id, body, true, start + CombatActions2D.Get(id).Windup + .005d, ref facing);
            Check(victim.Health == 100, "leaving monster telegraph avoids damage");
            victimBody.position -= Vector2.right * 4f; Physics2D.SyncTransforms();
            melee.Clock.Reset();
            double now = NetworkTime.time;
            melee.Step(id, body, true, now, ref facing);
            melee.Step(id, body, true, now + CombatActions2D.Get(id).Windup + .005d, ref facing);
            Check(victim.Health == 100 - CombatActions2D.Get(id).Damage, "monster damage occurs on contact frame");
            melee.Step(id, body, true, now + CombatActions2D.Get(id).Windup + .01d, ref facing);
            Check(victim.Health == 100 - CombatActions2D.Get(id).Damage, "monster cannot damage twice per strike");
            melee.Clock.Reset(); melee.Step(id, body, true, now, ref facing);
            if (monster is SideScrollerNetworkZombie zombie) zombie.ApplyDamage(1, victim);
            if (monster is SideScrollerNetworkSlime slime) slime.ApplyDamage(1, victim);
            Check(melee.Clock.Action == -1 && !melee.Clock.ConsumeHit(now + .5d), "hitstun interrupts monster anticipation");
            if (monster is SideScrollerNetworkZombie z) z.ApplyDamage(1000, victim);
            if (monster is SideScrollerNetworkSlime s) s.ApplyDamage(1000, victim);
            Check(!body.enabled && !melee.Clock.ConsumeHit(now + 1d), "dead monsters cannot finish attacks");
        }

        private static void CheckMonsterTurning(NetworkBehaviour monster, SideScrollerNetworkPlayer victim, int id)
        {
            var body = monster.GetComponent<Collider2D>();
            var melee = Field<MonsterMelee2D>(monster, "melee");
            var action = CombatActions2D.Get(id);
            var victimBody = victim.GetComponent<Rigidbody2D>();
            Vector2 right = new Vector2(body.bounds.center.x + .72f, SideWorldBuilder.PlayerSpawn.y);
            Vector2 left = new Vector2(body.bounds.center.x - .72f, SideWorldBuilder.PlayerSpawn.y);
            Set(victim, "health", 100); Set(victim, "invulnerableUntil", -1d);
            melee.Clock.Reset(); int facing = 1;
            victimBody.position = right; Physics2D.SyncTransforms();
            Check(melee.Step(id, body, true, 100d, ref facing) && facing == 1, "monster starts toward nearby player");
            victimBody.position = left; Physics2D.SyncTransforms();
            Check(melee.Step(id, body, true, 100.08d, ref facing) && facing == -1, "early windup tracks player crossing behind");
            Check(melee.Clock.StartedAt == 100d && Math.Abs(melee.Clock.ReadyAt(id) - 100d - action.Cooldown) < .001d, "turning never restarts windup or cooldown");
            victimBody.position = right; Physics2D.SyncTransforms();
            melee.Step(id, body, true, 100d + action.Windup - .04d, ref facing);
            Check(facing == -1, "red committed telegraph cannot whip around");
            melee.Step(id, body, true, 100d + action.Windup + .005d, ref facing);
            Check(facing == -1, "contact direction matches committed warning");
            if (id == 4) Check(victim.Health == 100, "crossing behind committed directional claw dodges it");
            Check(!melee.Step(id, body, true, 100d + action.Duration + .01d, ref facing) && facing == 1 && melee.HasTarget,
                "recovery end retargets behind even while attack is cooling down");
            victimBody.position = left; Physics2D.SyncTransforms();
            Check(melee.Step(id, body, true, 100d + action.Cooldown + .01d, ref facing) && facing == -1, "next attack re-acquires opposite side");

            // Exercise real patrol code, not just the isolated melee helper.
            melee.Clock.Reset(); melee.Clock.TryBegin(id, 1, true, NetworkTime.time);
            typeof(CombatActionClock).GetProperty("StartedAt").SetValue(melee.Clock, NetworkTime.time - action.Duration - .01d);
            Vector3 oldSpawn = Field<Vector3>(monster, "spawnPosition");
            Set(monster, "spawnPosition", monster.transform.position - Vector3.right * 20f);
            Set(monster, "nextContactDamageAt", -1d); Set(monster, "staggerUntil", -1d);
            Invoke(monster, "FixedUpdate");
            Check(Field<int>(monster, "facing") == -1 && Field<int>(monster, "motion") == 0 && Mathf.Abs(body.attachedRigidbody.linearVelocity.x) < .001f,
                "engaged monster faces player and waits; patrol must not override cooldown facing");
            Set(monster, "spawnPosition", oldSpawn);
            victimBody.position = right + Vector2.right * 5f; Physics2D.SyncTransforms();
            melee.Clock.Reset(); melee.Step(id, body, true, NetworkTime.time, ref facing);
            Check(!melee.HasTarget, "lost target releases engagement");
        }

        private static void CheckPresentation()
        {
            var shader = Resources.Load<Shader>("Effects/CombatSprite");
            Check(shader != null && !UnityEditor.ShaderUtil.ShaderHasError(shader), "combat sprite shader imports without errors");
            var actor = new GameObject("Combat presentation QA");
            try
            {
                var body = actor.AddComponent<BoxCollider2D>(); body.size = new Vector2(.7f, 1.6f);
                var view = CombatActionView2D.Create(actor.transform);
                Physics2D.SyncTransforms();
                foreach (var avatar in new[] { PlayerAvatarKind.Warrior, PlayerAvatarKind.Ranger, PlayerAvatarKind.Slime })
                for (int id = 0; id < CombatActions2D.Count; id++)
                foreach (int direction in new[] { -1, 1 })
                {
                    var action = CombatActions2D.Get(id);
                    foreach (float age in new[] { action.Windup * .5f, action.Windup + .002f, action.Windup + action.Active, action.Duration - .01f })
                    {
                        view.Show(id, direction, age, body, avatar); Invoke(view, "LateUpdate");
                        var filter = view.GetComponent<MeshFilter>();
                        if (id == 0 && age < action.Windup) continue;
                        Check(filter != null && filter.sharedMesh.vertexCount > 0 && filter.sharedMesh.vertexCount < 8192, "every effect phase renders within fixed mesh budget");
                        foreach (Vector3 point in filter.sharedMesh.vertices)
                            Check(!float.IsNaN(point.x) && !float.IsNaN(point.y) && !float.IsInfinity(point.x) && !float.IsInfinity(point.y), "finite effect geometry");
                    }
                    view.Show(id, direction, action.Duration + .1f, body, avatar); Invoke(view, "LateUpdate");
                    Check(!view.GetComponent<MeshRenderer>().enabled, "expired network action hides its visual");
                }
                Check(view.GetComponentsInChildren<Collider2D>().Length == 0, "visual effects never introduce hitboxes");
                view.ConfirmImpact(new Vector2(3f, 2f), 1, 1, PlayerAvatarKind.Ranger); Invoke(view, "LateUpdate");
                Check(view.GetComponent<MeshRenderer>().enabled, "confirmed impact renders outside cast lifetime");
                Vector3 before = view.GetComponent<MeshFilter>().sharedMesh.bounds.center + view.transform.position;
                actor.transform.position += Vector3.right * 2f; Invoke(view, "LateUpdate");
                Vector3 after = view.GetComponent<MeshFilter>().sharedMesh.bounds.center + view.transform.position;
                Check(Vector3.Distance(before, after) < .025f, "hit sparks stay at world contact point when attacker moves");
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }
            Debug.Log("COMBAT_POLISH_PASS: monster rear tracking, commit lock, cooldown facing; finite layered VFX, expiry and world-space confirmed impacts.");
        }

        private static GameObject Object(Scene scene, string name) { var item = new GameObject(name); SceneManager.MoveGameObjectToScene(item, scene); return item; }
        private static T Field<T>(object item, string name) => (T)item.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(item);
        private static void Set(object item, string name, object value) => item.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(item, value);
        private static void Invoke(object item, string name, params object[] args) => item.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(item, args);
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("Skill regression: " + message); }
    }
}
