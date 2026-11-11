using System.Collections.Generic;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// M4 PR 4.4: the built props, traces and landmarks of the kit (shed, bench, bridge, rake, bucket, bicycle, laundry, chapel,
    /// lighthouse). Determinism, budgets, palette, flat normals, colliders that hold the mesh, and the bridge as one block for the ball.
    /// </summary>
    public class BuiltPropKitTests
    {
        private static readonly Vector3 Root = new Vector3(3f, 1.5f, -2f);
        private static readonly Vector3 BridgeEnd = new Vector3(7.2f, 1.7f, -1f);

        private static readonly Dictionary<PropKind, int> Budget = new Dictionary<PropKind, int>
        {
            { PropKind.Shed, 60 },
            { PropKind.Bench, 70 },
            { PropKind.Bridge, 320 },
            { PropKind.Rake, 30 },
            { PropKind.Bucket, 100 },
            { PropKind.Bicycle, 140 },
            { PropKind.Laundry, 80 },
            { PropKind.Chapel, 100 },
            { PropKind.Lighthouse, 120 },
        };

        private PropKitSettings _settings;
        private PaletteSettings _palette;

        public static IEnumerable<PropKind> BuiltKinds => Budget.Keys;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<PropKitSettings>();
            _palette = ScriptableObject.CreateInstance<PaletteSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
            Object.DestroyImmediate(_palette);
        }

        private (TerrainMeshData mesh, PropShape[] shapes) Build(PropKind kind, int seed, float yaw = 25f, float scale = 1f)
        {
            var builder = new PropBuilder(_settings, _palette);
            PropShape[] shapes = builder.Add(kind, Root, kind == PropKind.Bridge ? BridgeEnd : Root, scale, seed, yaw);
            return (builder.Mesh, shapes);
        }

        private HashSet<Color32> Allowed(PropKind kind)
        {
            Color[] colors;
            switch (kind)
            {
                case PropKind.Shed:
                    colors = new[] { _palette.StoneLight, _palette.TileLight, _palette.TileDark, _palette.Trunk };
                    break;
                case PropKind.Bench:
                    colors = new[] { _palette.WoodLight, _palette.Trunk };
                    break;
                case PropKind.Bridge:
                    colors = new[] { _palette.StoneLight, _palette.StoneMid, _palette.StoneDark };
                    break;
                case PropKind.Rake:
                    colors = new[] { _palette.WoodLight, _palette.Metal };
                    break;
                case PropKind.Bucket:
                    colors = new[] { _palette.TraceRed, _palette.TraceOffWhite };
                    break;
                case PropKind.Bicycle:
                    colors = new[] { _palette.MetalDark, _palette.Metal, _palette.TraceBlue };
                    break;
                case PropKind.Laundry:
                    colors = new[] { _palette.WoodLight, _palette.MetalDark, _palette.TraceOffWhite, _palette.TraceBlue };
                    break;
                case PropKind.Chapel:
                    colors = new[] { _palette.StoneLight, _palette.StoneMid, _palette.SlateLight, _palette.SlateMid, _palette.SlateDark, _palette.Trunk };
                    break;
                default:
                    colors = new[] { _palette.StoneLight, _palette.StoneDark, _palette.SlateLight, _palette.SlateMid, _palette.SlateDark };
                    break;
            }

            var set = new HashSet<Color32>();
            foreach (Color c in colors)
            {
                set.Add(TerrainBuilder.ToVertexColor(c));
            }

            return set;
        }

        [Test]
        public void SameSeedBuildsTheSameProp([ValueSource(nameof(BuiltKinds))] PropKind kind)
        {
            var first = Build(kind, 11);
            var second = Build(kind, 11);
            CollectionAssert.AreEqual(first.mesh.Positions, second.mesh.Positions);
            CollectionAssert.AreEqual(first.mesh.Colors, second.mesh.Colors);
            CollectionAssert.AreEqual(first.shapes, second.shapes);
        }

        [Test]
        public void TriangleCountsStayInBudget([ValueSource(nameof(BuiltKinds))] PropKind kind)
        {
            int worst = 0;
            for (int seed = 0; seed < 20; seed++)
            {
                worst = Mathf.Max(worst, Build(kind, seed).mesh.TriangleCount);
            }

            Debug.Log($"[BuiltKit] {kind}: {Build(kind, 0).mesh.TriangleCount} triangles (seed 0), worst of 20 seeds {worst}, budget {Budget[kind]}");
            Assert.That(worst, Is.GreaterThan(0));
            Assert.That(worst, Is.LessThanOrEqualTo(Budget[kind]));
        }

        [Test]
        public void PropUsesOnlyItsPaletteColors([ValueSource(nameof(BuiltKinds))] PropKind kind)
        {
            HashSet<Color32> allowed = Allowed(kind);
            foreach (Color32 color in Build(kind, 3).mesh.Colors)
            {
                Assert.That(allowed, Does.Contain(color), $"{kind}");
            }
        }

        [Test]
        public void NormalsAreFlat([ValueSource(nameof(BuiltKinds))] PropKind kind)
        {
            TerrainMeshData data = Build(kind, 5).mesh;
            Mesh mesh = data.ToMesh("test");
            try
            {
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Assert.That(vertices.Length, Is.EqualTo(data.TriangleCount * 3), "vertices are never shared");
                for (int i = 0; i < vertices.Length; i += 3)
                {
                    Vector3 face = Vector3.Cross(vertices[i + 1] - vertices[i], vertices[i + 2] - vertices[i]).normalized;
                    for (int k = 0; k < 3; k++)
                    {
                        Assert.That(Vector3.Dot(normals[i + k], face), Is.GreaterThan(0.999f), $"{kind}: flat normal");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void CollidersHoldTheMesh([ValueSource(nameof(BuiltKinds))] PropKind kind)
        {
            // A rake collides with nothing, and laundry only by its posts: the cloths hang between them.
            if (kind == PropKind.Rake || kind == PropKind.Laundry)
            {
                Assert.Pass("Not meant to enclose its mesh");
            }

            foreach (float scale in new[] { 0.92f, 1f, 1.12f })
            {
                for (int seed = 0; seed < 10; seed++)
                {
                    var (mesh, shapes) = Build(kind, seed, seed * 37f, scale);
                    Assert.That(shapes, Is.Not.Empty, $"{kind}");
                    foreach (Vector3 point in mesh.Positions)
                    {
                        // The foot below the ground line is never reached by the ball.
                        if (point.y < Root.y - 0.001f && kind != PropKind.Bridge)
                        {
                            continue;
                        }

                        float best = float.MaxValue;
                        foreach (PropShape shape in shapes)
                        {
                            best = Mathf.Min(best, shape.Level(point));
                        }

                        Assert.That(best, Is.LessThanOrEqualTo(1.001f), $"{kind} seed {seed} scale {scale}: a vertex sticks out of every collider");
                    }
                }
            }
        }

        [Test]
        public void ColliderShapesAndMaterials()
        {
            Assert.That(Build(PropKind.Shed, 1).shapes[0].Kind, Is.EqualTo(PropShapeKind.Box));
            Assert.That(Build(PropKind.Bench, 1).shapes[0].Kind, Is.EqualTo(PropShapeKind.Box));
            Assert.That(Build(PropKind.Bench, 1).shapes[0].Material, Is.EqualTo(PropMaterial.Wood));
            Assert.That(Build(PropKind.Bridge, 1).shapes, Has.Length.EqualTo(1), "one block, arch to deck");
            Assert.That(Build(PropKind.Bridge, 1).shapes[0].Kind, Is.EqualTo(PropShapeKind.Box));
            Assert.That(Build(PropKind.Bridge, 1).shapes[0].Material, Is.EqualTo(PropMaterial.Stone));
            Assert.That(Build(PropKind.Chapel, 1).shapes[0].Kind, Is.EqualTo(PropShapeKind.Box));
            PropShape[] lighthouse = Build(PropKind.Lighthouse, 1).shapes;
            Assert.That(lighthouse, Has.Length.EqualTo(2));
            Assert.That(lighthouse[0].Kind, Is.EqualTo(PropShapeKind.Cylinder));
            Assert.That(Build(PropKind.Bucket, 1).shapes[0].Kind, Is.EqualTo(PropShapeKind.Cylinder));
            Assert.That(Build(PropKind.Bicycle, 1).shapes[0].Kind, Is.EqualTo(PropShapeKind.Box));
        }

        [Test]
        public void FlatRake_HasNoCollider_AndUprightTracesHaveOne()
        {
            Assert.That(Build(PropKind.Rake, 1).shapes, Is.Empty, "flat traces don't collide (Q11 A)");
            foreach (PropKind kind in new[] { PropKind.Bucket, PropKind.Bicycle, PropKind.Laundry })
            {
                Assert.That(Build(kind, 1).shapes, Is.Not.Empty, $"{kind} stands up and collides");
            }

            // Laundry: its two posts only, the cloths and the line above them don't.
            PropShape[] laundry = Build(PropKind.Laundry, 1).shapes;
            Assert.That(laundry, Has.Length.EqualTo(2));
            Assert.That(laundry[0].Kind, Is.EqualTo(PropShapeKind.Cylinder));
        }

        [Test]
        public void BridgeBlock_StandsOnItsBanksAndReachesBelowThem()
        {
            PropShape box = Build(PropKind.Bridge, 4).shapes[0];
            float bottom = box.Center.y - box.Extents.y;
            Assert.That(bottom, Is.LessThan(Mathf.Min(Root.y, BridgeEnd.y) - 0.3f), "stone stands in the water");
            Assert.That(box.Center.y + box.Extents.y, Is.GreaterThan(Mathf.Max(Root.y, BridgeEnd.y)), "the deck is above the banks");

            Vector2 flat = new Vector2(BridgeEnd.x - Root.x, BridgeEnd.z - Root.z);
            Assert.That(box.Extents.x * 2f, Is.GreaterThan(flat.magnitude - 0.1f), "one block holds the whole span");
            Assert.That(box.Yaw, Is.EqualTo(Mathf.Atan2(flat.y, flat.x)).Within(1e-5f));
        }

        [Test]
        public void LighthouseNarrowsUp_AndItsCylindersFollowIt()
        {
            PropShape[] cylinders = Build(PropKind.Lighthouse, 2).shapes;
            Assert.That(cylinders[1].Extents.x, Is.LessThanOrEqualTo(cylinders[0].Extents.x));
            Assert.That(cylinders[1].Center.y - cylinders[1].Extents.y, Is.EqualTo(cylinders[0].Center.y + cylinders[0].Extents.y).Within(1e-4f), "stacked");
        }

        // --- The bridge, as the ball meets it ---

        private static bool Final(BallSimulation ball)
        {
            return ball.State == BallState.Resting || ball.State == BallState.Holed || ball.State == BallState.InWater || ball.State == BallState.OffIsland;
        }

        /// <summary>
        /// A flat fairway with a pond under a bridge: the bridge runs along +X over water, 1.5 m either side of the shot's line, and the
        /// ball's flat shot is aimed so its landing falls on the middle of the span.
        /// </summary>
        private static BallSimulation Drop(ShotSettings settings, float power, float aimDegrees, out PropShape block)
        {
            var probeGround = PlaneBallGround.All(CourseSurface.Fairway);
            var probe = new BallSimulation(probeGround, settings, Vector2.zero, null);
            Vector2 aim = Geo2.Dir(aimDegrees);
            BallisticArc arc = probe.ArcFor(aim, power);
            Vector3 landing = arc.PositionAt(probe.PredictLanding(arc).Time);

            var kit = ScriptableObject.CreateInstance<PropKitSettings>();
            try
            {
                var along = new Vector3(aim.x, 0f, aim.y);
                Vector3 start = landing - (along * 2.4f);
                Vector3 end = landing + (along * 2.4f);
                start.y = 0f;
                end.y = 0f;
                block = new PropBuilder(kit, null).AddBridge(start, end, 1f, new System.Random(7))[0];

                var ground = new PlaneBallGround(Vector2.zero, (x, z) =>
                {
                    float u = ((x - start.x) * along.x) + ((z - start.z) * along.z);
                    float v = ((x - start.x) * -along.z) + ((z - start.z) * along.x);
                    return u > -0.7f && u < 5.5f && Mathf.Abs(v) < 2.5f ? CourseSurface.Water : CourseSurface.Fairway;
                });
                var obstacles = new BallObstacles(new[] { block }, settings.BallRadius, null, ground);
                var ball = new BallSimulation(ground, settings, Vector2.zero, obstacles);
                ball.Shoot(aim, power);
                return ball;
            }
            finally
            {
                Object.DestroyImmediate(kit);
            }
        }

        private static class Geo2
        {
            public static Vector2 Dir(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
        }

        [Test]
        public void BallDroppingOnTheBridge_BouncesOrFallsInTheWater_NeverRestsOnTheDeck()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ShotSettings>("Assets/_Project/Settings/ShotSettings.asset");
            int struck = 0;
            for (int p = 0; p < 12; p++)
            {
                float power = 0.3f + (p * 0.06f);
                foreach (float aim in new[] { 0f, 20f, -35f })
                {
                    BallSimulation ball = Drop(settings, power, aim, out PropShape block);
                    float deck = block.Center.y + block.Extents.y;
                    for (int i = 0; i < 60 * 120 && !Final(ball); i++)
                    {
                        ball.Step();
                    }

                    Assert.That(Final(ball), $"power {power:F2} aim {aim}: still {ball.State}");
                    if (ball.Hits > 0)
                    {
                        struck++;
                    }

                    Assert.That(ball.Hits, Is.LessThanOrEqualTo(settings.MaxHits));

                    // Never at rest up on the block, and never carried across: it ends in the water or off the block's top.
                    bool onTheBlock = block.Level(ball.Position) <= 1.001f || ball.Position.y > deck - 0.2f;
                    Assert.That(ball.State == BallState.Resting && onTheBlock, Is.False, $"power {power:F2} aim {aim}: at rest on the bridge at {ball.Position}");
                }
            }

            Assert.That(struck, Is.GreaterThan(20), "the shots do meet the bridge");
        }

        [Test]
        public void BridgeHitCap_EndsTheBallCleanly()
        {
            // Perfectly bouncy stone: the ball keeps hitting the block, and the hit cap drops it straight down, into the water.
            var settings = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ShotSettings>("Assets/_Project/Settings/ShotSettings.asset"));
            try
            {
                using (var serialized = new SerializedObject(settings))
                {
                    serialized.FindProperty("stone.restitution").floatValue = 1f;
                    serialized.FindProperty("stone.alongKeep").floatValue = 1f;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                BallSimulation ball = Drop(settings, 0.7f, 0f, out _);
                for (int i = 0; i < 60 * 120 && !Final(ball); i++)
                {
                    ball.Step();
                }

                Assert.That(Final(ball), $"still {ball.State}");
                Assert.That(ball.Hits, Is.LessThanOrEqualTo(settings.MaxHits));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }
    }
}
