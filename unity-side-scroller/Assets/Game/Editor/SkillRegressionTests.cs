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

        private static GameObject Object(Scene scene, string name) { var item = new GameObject(name); SceneManager.MoveGameObjectToScene(item, scene); return item; }
        private static T Field<T>(object item, string name) => (T)item.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(item);
        private static void Set(object item, string name, object value) => item.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(item, value);
        private static void Invoke(object item, string name, params object[] args) => item.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(item, args);
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("Skill regression: " + message); }
    }
}
