using System;
using System.Collections.Generic;
using System.Reflection;
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
    /// <summary>Exercises actual server components and Unity physics without opening a network port.</summary>
    public static class LifecycleRegressionTests
    {
        public static void Run()
        {
            Require(!NetworkServer.active && !NetworkClient.active, "Run lifecycle QA outside an active multiplayer session.");
            Scene scene = EditorSceneManager.NewPreviewScene();
            scene.name = "Lifecycle QA";
            PhysicsScene2D physicsScene = scene.GetPhysicsScene2D();
            Transport previousTransport = Transport.active;
            bool previousListen = NetworkServer.listen;
            var actors = new List<NetworkBehaviour>();
            try
            {
                Require(physicsScene.IsValid() && physicsScene != Physics2D.defaultPhysicsScene, "Physics must be isolated from the open game scene.");
                GameObject transportObject = Object(scene, "Non-listening QA transport");
                Transport.active = transportObject.AddComponent<TelepathyTransport>();
                NetworkServer.listen = false;
                NetworkServer.Listen(0);

                BoxCollider2D floor = Object(scene, "Floor").AddComponent<BoxCollider2D>();
                floor.transform.position = new Vector2(0f, SideWorldBuilder.FloorSurfaceY - 0.5f);
                floor.size = new Vector2(100f, 1f);

                var player = Actor<SideScrollerNetworkPlayer>(scene, SideWorldBuilder.PlayerSpawn, false);
                actors.Add(player);
                var zombie = Actor<SideScrollerNetworkZombie>(scene, new Vector3(5f, SideWorldBuilder.PlayerSpawn.y, 0f), false);
                actors.Add(zombie);
                Vector3 slimeSpawn = ActorGeometry2D.RootPositionForFeet(new Vector3(10f, SideWorldBuilder.FloorSurfaceY, 0f), ActorGeometry2D.SlimeFeetLocalY);
                var slime = Actor<SideScrollerNetworkSlime>(scene, slimeSpawn, true);
                actors.Add(slime);
                Physics2D.SyncTransforms();

                SetField(player, "invulnerableUntil", -1d);
                player.ApplyDamage(1000, null);
                CheckDeathAndRespawn(player, "respawnAt", SideWorldBuilder.PlayerSpawn, physicsScene);
                zombie.ApplyDamage(1000, null);
                CheckDeathAndRespawn(zombie, "reviveAt", new Vector3(5f, SideWorldBuilder.PlayerSpawn.y, 0f), physicsScene);
                slime.ApplyDamage(1000, null);
                CheckDeathAndRespawn(slime, "reviveAt", slimeSpawn, physicsScene);

                int zombieHealth = Field<int>(zombie, "health");
                int slimeHealth = Field<int>(slime, "health");
                zombie.ApplyDamage(0, player);
                slime.ApplyDamage(-1, player);
                Require(Field<int>(zombie, "health") == zombieHealth && Field<int>(slime, "health") == slimeHealth, "Non-positive damage must have no gameplay effect.");

                // An overshot patrol limit must continue facing inward on the
                // following tick, rather than flip every FixedUpdate.
                Rigidbody2D zombieBody = zombie.GetComponent<Rigidbody2D>();
                zombieBody.position = new Vector2(8.6f, SideWorldBuilder.PlayerSpawn.y);
                zombieBody.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
                Invoke(zombie, "FixedUpdate");
                Require(Field<int>(zombie, "facing") == -1, "Zombie turns inward past the right patrol limit.");
                Invoke(zombie, "FixedUpdate");
                Require(Field<int>(zombie, "facing") == -1, "Zombie must not oscillate at the patrol limit.");

                floor.size = new Vector2(0.75f, 1f);
                floor.transform.position = new Vector2(5f, SideWorldBuilder.FloorSurfaceY - 0.5f);
                zombieBody.position = new Vector2(5f, SideWorldBuilder.PlayerSpawn.y);
                zombieBody.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
                int initialFacing = Field<int>(zombie, "facing");
                Invoke(zombie, "FixedUpdate");
                Invoke(zombie, "FixedUpdate");
                Require(Field<int>(zombie, "facing") == initialFacing && Mathf.Abs(zombieBody.linearVelocity.x) < 0.001f, "Zombie on a narrow ledge must wait without turning every tick.");

                floor.transform.position = new Vector2(10f, SideWorldBuilder.FloorSurfaceY - 0.5f);
                Rigidbody2D slimeBody = slime.GetComponent<Rigidbody2D>();
                slimeBody.position = slimeSpawn;
                slimeBody.linearVelocity = Vector2.zero;
                SetField(slime, "nextHopAt", -1d);
                Physics2D.SyncTransforms();
                Invoke(slime, "FixedUpdate");
                Require(Mathf.Abs(slimeBody.linearVelocity.x) < 0.001f && slimeBody.linearVelocity.y > 0f, "A slime without a supported landing must hop vertically instead of into the gap.");
                Debug.Log("LIFECYCLE_REGRESSION_PASS: player/zombie/slime death, frozen corpses, respawn, teleport buffers, damage validation and patrol-edge scenarios passed.");
            }
            finally
            {
                foreach (NetworkBehaviour actor in actors)
                {
                    if (actor == null) continue;
                    if (NetworkServer.active && actor.netIdentity != null && actor.netIdentity.netId != 0) NetworkServer.UnSpawn(actor.gameObject);
                    UnityEngine.Object.DestroyImmediate(actor.gameObject);
                }
                if (NetworkServer.active) NetworkServer.Shutdown();
                Transport.active = previousTransport;
                NetworkServer.listen = previousListen;
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        internal static T Actor<T>(Scene scene, Vector3 position, bool slimeShape) where T : NetworkBehaviour
        {
            GameObject root = Object(scene, typeof(T).Name);
            root.transform.position = position;
            var identity = root.AddComponent<NetworkIdentity>();
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 3.15f;
            var collider = root.AddComponent<CapsuleCollider2D>();
            if (slimeShape)
            {
                collider.direction = CapsuleDirection2D.Horizontal;
                ActorGeometry2D.ConfigureSlime(collider);
            }
            else ActorGeometry2D.ConfigureHumanoid(collider);
            if (typeof(T) == typeof(SideScrollerNetworkPlayer))
            {
                root.AddComponent<PlayerInputReader>();
                root.AddComponent<Animator>();
            }
            var networkTransform = root.AddComponent<SideScrollerNetworkTransform>();
            networkTransform.target = root.transform;
            networkTransform.syncDirection = SyncDirection.ServerToClient;
            T actor = root.AddComponent<T>();
            // Edit-mode tests do not receive the play-mode Awake lifecycle.
            Invoke(identity, "InitializeNetworkBehaviours");
            Invoke(actor, "Awake");
            NetworkServer.Spawn(root);
            Require(actor.isServer, typeof(T).Name + " must be spawned on the real Mirror server.");
            return actor;
        }

        private static void CheckDeathAndRespawn(NetworkBehaviour actor, string timer, Vector3 expectedSpawn, PhysicsScene2D scene)
        {
            var body = actor.GetComponent<Rigidbody2D>();
            var collider = actor.GetComponent<Collider2D>();
            var networkTransform = actor.GetComponent<SideScrollerNetworkTransform>();
            Require(Field<bool>(actor, "defeated") && !body.simulated && !collider.enabled, actor.name + " death disables physics and collision.");
            Vector2 corpsePosition = body.position;
            SetField(actor, timer, NetworkTime.time + 100000d);
            for (int step = 0; step < 120; step++)
            {
                Invoke(actor, "FixedUpdate");
                Require(scene.Simulate(0.02f), "The isolated physics scene must simulate.");
            }
            Require(Vector2.Distance(body.position, corpsePosition) < 0.001f, actor.name + " corpse must not fall after 2.4 seconds of gravity simulation.");
            var staleSnapshot = new TransformSnapshot(1d, 1d, corpsePosition + Vector2.right * 5f, Quaternion.identity, Vector3.one);
            networkTransform.serverSnapshots.Add(1d, staleSnapshot);
            networkTransform.clientSnapshots.Add(1d, staleSnapshot);
            SetField(actor, timer, -1d);
            Invoke(actor, "FixedUpdate");
            Require(!Field<bool>(actor, "defeated") && body.simulated && collider.enabled && Field<int>(actor, "health") > 0, actor.name + " respawn restores health, collision and physics.");
            Require(Vector2.Distance(body.position, expectedSpawn) < 0.001f, actor.name + " respawn uses its authoritative feet-aligned spawn.");
            Require(networkTransform.serverSnapshots.Count == 0 && networkTransform.clientSnapshots.Count == 0, actor.name + " respawn clears interpolation history through ServerTeleport.");
        }

        private static GameObject Object(Scene scene, string name)
        {
            var created = new GameObject(name);
            SceneManager.MoveGameObjectToScene(created, scene);
            return created;
        }

        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void SetField(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Lifecycle regression: " + message);
        }
    }
}
