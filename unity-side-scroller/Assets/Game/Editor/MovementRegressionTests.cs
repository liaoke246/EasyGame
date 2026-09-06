using System;
using System.Collections.Generic;
using EasyGame.SideScroller.Core;
using EasyGame.SideScroller.Data;
using EasyGame.SideScroller.Player;
using EasyGame.SideScroller.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EasyGame.SideScroller.Editor
{
    /// <summary>
    /// Executes real Unity 2D physics in isolated scenes, without editing the
    /// open game scene or requiring the optional Unity Test Framework package.
    /// Batch: -executeMethod EasyGame.SideScroller.Editor.MovementRegressionTests.Run
    /// </summary>
    public static class MovementRegressionTests
    {
        private const float StepTime = 0.02f;

        [MenuItem("EasyGame 2D/Validate Movement Physics")]
        public static void Run()
        {
            TestGroundAndWallDetection();
            TestDynamicActorIsNotGround();
            TestOneWayInteriorIsNotGround();
            TestShortJumpOntoWorldPlatform(false);
            TestShortJumpOntoWorldPlatform(true);
            TestCoyoteTime();
            TestJumpBuffer();
            TestNoSecondJumpDuringAscent();
            TestFallSpeedCap();
            TestRenderFrameIndependence();
            TestWallSlide();
            TestEveryPlatformLink(false);
            TestEveryPlatformLink(true);
            Debug.Log("MOVEMENT_REGRESSION_PASS: 13 real-physics scenarios passed, including every platform link in both directions for short and held jumps.");
        }

        private static void TestGroundAndWallDetection()
        {
            using (Fixture fixture = new Fixture("Ground detection"))
            {
                fixture.Rectangle("Floor", new Vector2(0f, -0.5f), new Vector2(4f, 1f));
                fixture.Actor(new Vector2(0f, 0f));
                fixture.Tick(10);
                Require(fixture.Motor.IsGrounded, "Standing on the floor must ground the actor.");
                Require(Mathf.Abs(fixture.FeetY) < 0.04f, "The collision feet must meet the floor surface.");

                fixture.Teleport(new Vector2(4f, 1f));
                fixture.Rectangle("Wall", new Vector2(4.4f, 1.5f), new Vector2(0.4f, 4f));
                Physics2D.SyncTransforms();
                fixture.Motor.Step(0f, false, StepTime);
                Require(!fixture.Motor.IsGrounded, "Touching the side of a wall must not grant a jump.");
            }
        }

        private static void TestDynamicActorIsNotGround()
        {
            using (Fixture fixture = new Fixture("Actor support rejection"))
            {
                fixture.Actor(new Vector2(0f, 1f));
                BoxCollider2D other = fixture.Rectangle("Other actor", new Vector2(0f, 0.5f), Vector2.one);
                Rigidbody2D otherBody = other.gameObject.AddComponent<Rigidbody2D>();
                otherBody.gravityScale = 0f;
                Physics2D.SyncTransforms();
                Require(!GroundProbe2D.Check(fixture.Collider, 0.78f, 0.16f, ~0), "Other dynamic actors must not replenish grounded jump time.");
            }
        }

        private static void TestOneWayInteriorIsNotGround()
        {
            using (Fixture fixture = new Fixture("One-way overlap rejection"))
            {
                fixture.Rectangle("One-way platform", new Vector2(0f, -0.5f), new Vector2(4f, 1f), true);
                fixture.Actor(new Vector2(0f, -0.15f));
                fixture.Motor.QueueJump();
                fixture.Motor.Step(0f, false, StepTime);
                Require(!fixture.Motor.IsGrounded, "A foot inside a one-way platform is not on its top surface.");
                Require(fixture.Body.linearVelocity.y <= 0f, "Platform overlap must not produce a midair jump.");
            }
        }

        private static void TestShortJumpOntoWorldPlatform(bool slimeShape)
        {
            using (Fixture fixture = new Fixture(slimeShape ? "Slime short jump" : "Humanoid short jump"))
            {
                GameObject world = fixture.Object("World collision");
                world.AddComponent<SideWorldBuilder>().BuildServerCollision();
                fixture.Actor(new Vector2(-7f, SideWorldBuilder.FloorSurfaceY), slimeShape);
                fixture.Tick(10);
                Require(fixture.Motor.IsGrounded, "The spawn point must be supported before jumping.");
                fixture.Motor.QueueJump();
                float highestFeet = fixture.FeetY;
                for (int step = 0; step < 90; step++)
                {
                    fixture.Tick(1, 0f, false);
                    highestFeet = Mathf.Max(highestFeet, fixture.FeetY);
                }
                Require(highestFeet > 0.15f, "Even a released/short jump must clear the first platform.");
                Require(Mathf.Abs(fixture.FeetY) < 0.045f && fixture.Motor.IsGrounded,
                    "The short jump must settle on top of the first platform, without sinking through it.");
                fixture.Tick(100);
                Require(Mathf.Abs(fixture.FeetY) < 0.045f, "Standing on a platform must remain stable for two more seconds.");
                Debug.Log($"Movement QA: {(slimeShape ? "slime" : "humanoid")} short-jump peak feet={highestFeet:F3}, landed feet={fixture.FeetY:F3}.");
            }
        }

        private static void TestCoyoteTime()
        {
            using (Fixture fixture = new Fixture("Coyote time"))
            {
                fixture.Rectangle("Ledge", new Vector2(0f, -0.5f), new Vector2(2f, 1f));
                fixture.Actor(Vector2.zero);
                fixture.Tick(10);
                fixture.Teleport(new Vector2(3f, 0f), false);
                fixture.Tick(4);
                fixture.Motor.QueueJump();
                fixture.Tick(1, 0f, true);
                Require(fixture.Body.linearVelocity.y > 10f, "Jumping 80 ms after leaving a ledge should use coyote time.");

                fixture.Teleport(Vector2.zero);
                fixture.Tick(10);
                fixture.Teleport(new Vector2(3f, 0f), false);
                fixture.Tick(12);
                fixture.Motor.QueueJump();
                fixture.Tick(1, 0f, true);
                Require(fixture.Body.linearVelocity.y < 0f, "Coyote time must expire and not allow a late air jump.");
            }
        }

        private static void TestJumpBuffer()
        {
            using (Fixture fixture = new Fixture("Landing jump buffer"))
            {
                fixture.Rectangle("Floor", new Vector2(0f, -0.5f), new Vector2(8f, 1f));
                fixture.Actor(new Vector2(0f, 0.55f));
                fixture.Body.linearVelocity = new Vector2(0f, -4f);
                fixture.Motor.QueueJump();
                bool jumped = false;
                for (int step = 0; step < 10; step++)
                {
                    fixture.Tick(1, 0f, true);
                    jumped |= fixture.Body.linearVelocity.y > 10f;
                }
                Require(jumped, "A jump pressed shortly before landing must be consumed on landing.");

                fixture.Teleport(new Vector2(0f, 3f));
                fixture.Motor.QueueJump();
                jumped = false;
                for (int step = 0; step < 80; step++)
                {
                    fixture.Tick(1);
                    jumped |= fixture.Body.linearVelocity.y > 10f;
                }
                Require(!jumped, "An expired airborne jump press must not auto-jump on a later landing.");
            }
        }

        private static void TestNoSecondJumpDuringAscent()
        {
            using (Fixture fixture = new Fixture("No coyote refill during ascent"))
            {
                fixture.Rectangle("Floor", new Vector2(0f, -0.5f), new Vector2(4f, 1f));
                fixture.Actor(Vector2.zero);
                fixture.Tick(10);
                fixture.Motor.QueueJump();
                fixture.Tick(1, 0f, true);
                float previousVelocity = fixture.Body.linearVelocity.y;
                fixture.Motor.QueueJump();
                fixture.Tick(1, 0f, true);
                Require(!fixture.Motor.IsGrounded, "An ascending actor must be airborne even within probe distance.");
                Require(fixture.Body.linearVelocity.y < previousVelocity - 0.1f,
                    "A second press during ascent must not replenish jump velocity.");
            }
        }

        private static void TestFallSpeedCap()
        {
            using (Fixture fixture = new Fixture("Fall speed cap"))
            {
                fixture.Actor(new Vector2(0f, 100f));
                fixture.Tick(120);
                Require(fixture.Body.linearVelocity.y >= -fixture.Config.maxFallSpeed - 0.01f,
                    "The velocity after Unity's gravity step must respect the configured terminal speed.");
            }
        }

        private static void TestRenderFrameIndependence()
        {
            Vector2 everyPhysicsFrame = SimulateInputWithRenderBatch(1);
            Vector2 fivePhysicsStepsPerFrame = SimulateInputWithRenderBatch(5);
            Require(Vector2.Distance(everyPhysicsFrame, fivePhysicsStepsPerFrame) < 0.001f,
                "Physics catch-up under low render FPS must not change movement or jump timing.");
        }

        private static void TestWallSlide()
        {
            using (Fixture fixture = new Fixture("No sticky wall friction"))
            {
                fixture.Rectangle("Wall", new Vector2(1f, 0f), new Vector2(0.4f, 100f));
                fixture.Actor(new Vector2(0.43f, 5f));
                fixture.Tick(25, 1f, false);
                Require(fixture.FeetY < 3f, "Pressing into a vertical wall must not stop gravity or glue the player in midair.");
                Require(fixture.Body.position.x < 0.48f, "The actor must slide beside the wall instead of penetrating it.");
            }
        }

        private static void TestEveryPlatformLink(bool jumpHeld)
        {
            using (Fixture fixture = new Fixture(jumpHeld ? "Held-jump world traversal" : "Short-jump world traversal"))
            {
                GameObject world = fixture.Object("World collision");
                world.AddComponent<SideWorldBuilder>().BuildServerCollision();
                Physics2D.SyncTransforms();
                List<BoxCollider2D> platforms = new List<BoxCollider2D>();
                foreach (BoxCollider2D collider in world.GetComponentsInChildren<BoxCollider2D>())
                {
                    if (collider.usedByEffector)
                    {
                        platforms.Add(collider);
                    }
                }
                platforms.Sort((left, right) => left.bounds.min.x.CompareTo(right.bounds.min.x));
                Require(platforms.Count > 1, "The world traversal test requires a connected platform path.");
                fixture.Actor(new Vector2(-7f, SideWorldBuilder.FloorSurfaceY));

                // Query the actual world rather than maintaining a second list of
                // coordinates: changing a jump or moving any ledge must revalidate
                // every route in both directions.
                for (int index = 0; index < platforms.Count - 1; index++)
                {
                    JumpBetween(fixture, platforms[index], platforms[index + 1], jumpHeld);
                    JumpBetween(fixture, platforms[index + 1], platforms[index], jumpHeld);
                }

                Bounds exitPlatform = platforms[platforms.Count - 1].bounds;
                fixture.Teleport(new Vector2(exitPlatform.center.x, SideWorldBuilder.FloorSurfaceY));
                fixture.Tick(10);
                fixture.Motor.QueueJump();
                fixture.Tick(90, 0f, jumpHeld);
                Require(Mathf.Abs(fixture.FeetY - exitPlatform.max.y) < 0.045f && fixture.Motor.IsGrounded,
                    "Players reaching the far end on the floor must be able to rejoin the platform path.");
                Debug.Log($"Movement QA: {(jumpHeld ? "held" : "short")} jump traversed {(platforms.Count - 1) * 2} directed platform links and the exit entrance.");
            }
        }

        private static void JumpBetween(Fixture fixture, BoxCollider2D source, BoxCollider2D target, bool jumpHeld)
        {
            Bounds from = source.bounds;
            Bounds to = target.bounds;
            float direction = Mathf.Sign(to.center.x - from.center.x);
            float takeoffX = direction > 0f ? from.max.x - 0.4f : from.min.x + 0.4f;
            float destinationX = direction > 0f ? to.min.x + 0.55f : to.max.x - 0.55f;
            fixture.Teleport(new Vector2(takeoffX, from.max.y));
            fixture.Tick(10);
            Require(fixture.Motor.IsGrounded, $"Takeoff platform {source.name} must support the player.");
            fixture.Motor.QueueJump();
            bool releasedMovement = false;
            for (int step = 0; step < 100; step++)
            {
                releasedMovement |= (fixture.Body.position.x - destinationX) * direction >= 0f;
                fixture.Tick(1, releasedMovement ? 0f : direction, jumpHeld);
            }
            Require(fixture.Body.position.x > to.min.x && fixture.Body.position.x < to.max.x
                && Mathf.Abs(fixture.FeetY - to.max.y) < 0.045f && fixture.Motor.IsGrounded,
                $"{(jumpHeld ? "Held" : "Short")} jump could not land from {source.name} to {target.name}. "
                + $"Final feet=({fixture.Body.position.x:F3}, {fixture.FeetY:F3}), target={to}.");
        }

        private static Vector2 SimulateInputWithRenderBatch(int stepsPerRenderFrame)
        {
            using (Fixture fixture = new Fixture($"Render batch {stepsPerRenderFrame}"))
            {
                fixture.Rectangle("Floor", new Vector2(0f, -0.5f), new Vector2(100f, 1f));
                fixture.Actor(Vector2.zero);
                fixture.Tick(10);
                fixture.Motor.QueueJump();
                for (int physicsStep = 0; physicsStep < 100; physicsStep += stepsPerRenderFrame)
                {
                    fixture.Tick(stepsPerRenderFrame, 1f, false);
                }
                return fixture.Body.position;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Movement regression: " + message);
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly Scene scene;
            private readonly PhysicsScene2D physicsScene;
            private float feetOffset;

            public PlayerMovementConfig Config { get; }
            public Rigidbody2D Body { get; private set; }
            public CapsuleCollider2D Collider { get; private set; }
            public PlayerMotor2D Motor { get; private set; }
            public float FeetY => Body.position.y + feetOffset;

            public Fixture(string name)
            {
                scene = EditorSceneManager.NewPreviewScene();
                scene.name = "Movement QA " + name;
                physicsScene = scene.GetPhysicsScene2D();
                Require(physicsScene.IsValid() && physicsScene != Physics2D.defaultPhysicsScene, "Physics tests must be isolated from the open game scene.");
                PlayerMovementConfig savedConfig = Resources.Load<PlayerMovementConfig>("Config/PlayerMovement");
                Config = savedConfig != null ? UnityEngine.Object.Instantiate(savedConfig) : ScriptableObject.CreateInstance<PlayerMovementConfig>();
            }

            public GameObject Object(string name)
            {
                GameObject created = new GameObject(name);
                SceneManager.MoveGameObjectToScene(created, scene);
                return created;
            }

            public BoxCollider2D Rectangle(string name, Vector2 center, Vector2 size, bool oneWay = false)
            {
                GameObject created = Object(name);
                created.transform.position = center;
                BoxCollider2D collider = created.AddComponent<BoxCollider2D>();
                collider.size = size;
                if (oneWay)
                {
                    collider.usedByEffector = true;
                    PlatformEffector2D effector = created.AddComponent<PlatformEffector2D>();
                    effector.useOneWay = true;
                    effector.surfaceArc = 170f;
                }
                return collider;
            }

            public void Actor(Vector2 feetPosition, bool slimeShape = false)
            {
                GameObject actor = Object("Test actor");
                Body = actor.AddComponent<Rigidbody2D>();
                Collider = actor.AddComponent<CapsuleCollider2D>();
                if (slimeShape)
                {
                    Collider.direction = CapsuleDirection2D.Horizontal;
                    ActorGeometry2D.ConfigureSlime(Collider);
                    feetOffset = ActorGeometry2D.SlimeFeetLocalY;
                }
                else
                {
                    ActorGeometry2D.ConfigureHumanoid(Collider);
                    feetOffset = ActorGeometry2D.HumanoidFeetLocalY;
                }
                Motor = new PlayerMotor2D(Body, Collider, Config);
                Teleport(feetPosition);
            }

            public void Teleport(Vector2 feetPosition, bool resetMotor = true)
            {
                Body.position = feetPosition - new Vector2(0f, feetOffset);
                Body.linearVelocity = Vector2.zero;
                if (resetMotor)
                {
                    Motor.Reset();
                }
                Physics2D.SyncTransforms();
            }

            public void Tick(int count, float horizontal = 0f, bool held = false)
            {
                Physics2D.SyncTransforms();
                for (int step = 0; step < count; step++)
                {
                    Motor.Step(horizontal, held, StepTime);
                    Require(physicsScene.Simulate(StepTime), "The isolated physics scene failed to simulate.");
                }
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Config);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
