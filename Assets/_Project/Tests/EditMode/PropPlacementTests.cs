using System.Collections.Generic;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IsoGolf.Tests
{
    /// <summary>
    /// M4 PR 4.3: props placed by rules on generated courses. The keep-out checks here are written independently of
    /// <see cref="PropZones"/> (they sample the true outlines and the safe route's own capsule samples), so a bug in the zones
    /// can't hide itself.
    /// </summary>
    public class PropPlacementTests
    {
        private const float Tolerance = 0.011f;

        private static LevelGenSettings Settings => CourseGeneratorTests.Settings;
        private static ShotSettings Shot => CourseCheckMeasurements.Shot;
        private static PaletteSettings Palette => AssetDatabase.LoadAssetAtPath<PaletteSettings>("Assets/_Project/Settings/Palette.asset");

        private static LevelGenSettings Copy(System.Action<SerializedObject> change)
        {
            LevelGenSettings copy = Object.Instantiate(Settings);
            var serialized = new SerializedObject(copy);
            change(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return copy;
        }

        [Test]
        public void MainSettings_HaveAPropKit()
        {
            Assert.That(Settings.PropKit, Is.Not.Null);
        }

        [Test]
        public void SameSeed_GivesTheSameProps()
        {
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < 5; seed++)
                {
                    IReadOnlyList<CourseProp> a = CourseGenerator.Generate(seed, par, Settings).Props;
                    IReadOnlyList<CourseProp> b = CourseGenerator.Generate(seed, par, Settings).Props;
                    Assert.That(b.Count, Is.EqualTo(a.Count), $"seed {seed} par {par}");
                    for (int i = 0; i < a.Count; i++)
                    {
                        Assert.That(b[i].Kind, Is.EqualTo(a[i].Kind));
                        Assert.That(b[i].Root, Is.EqualTo(a[i].Root));
                        Assert.That(b[i].End, Is.EqualTo(a[i].End));
                        Assert.That(b[i].Scale, Is.EqualTo(a[i].Scale));
                        Assert.That(b[i].Seed, Is.EqualTo(a[i].Seed));
                        Assert.That(b[i].Shapes, Is.EqualTo(a[i].Shapes));
                    }
                }
            }
        }

        [Test]
        public void PropsOnOrOff_LeaveTheCourseUntouched()
        {
            // Props come last, from their own stream: islands, slopes, green, pin, hazards and the plan are the same with or
            // without them, and with no prop kit at all.
            LevelGenSettings noKit = Copy(s => s.FindProperty("propKit").objectReferenceValue = null);
            try
            {
                foreach (int par in new[] { 3, 4, 5 })
                {
                    for (int seed = 0; seed < 10; seed++)
                    {
                        CourseLayout on = CourseGenerator.Generate(seed, par, Settings);
                        CourseLayout off = CourseGenerator.Generate(seed, par, Settings, 0, props: false);
                        CourseLayout none = CourseGenerator.Generate(seed, par, noKit);
                        Assert.That(off.Props, Is.Empty);
                        Assert.That(none.Props, Is.Empty);
                        foreach (CourseLayout other in new[] { off, none })
                        {
                            Assert.That(other.Islands, Is.EqualTo(on.Islands), $"seed {seed} par {par}");
                            Assert.That(other.Centreline.Points, Is.EqualTo(on.Centreline.Points));
                            Assert.That(other.Tee, Is.EqualTo(on.Tee));
                            Assert.That(other.Hole, Is.EqualTo(on.Hole));
                            Assert.That(other.Greens, Is.EqualTo(on.Greens));
                            Assert.That(other.Bunkers, Is.EqualTo(on.Bunkers));
                            Assert.That(other.Ponds, Is.EqualTo(on.Ponds));
                            Assert.That(other.Plan.SafeRoute, Is.EqualTo(on.Plan.SafeRoute));
                            Assert.That(other.Elevation.KnotCount, Is.EqualTo(on.Elevation.KnotCount));
                            var a = new CourseFields(on);
                            var b = new CourseFields(other);
                            for (int i = 0; i < 20; i++)
                            {
                                Vector2 p = on.Centreline.PointAt(on.Centreline.Length * i / 19f) + new Vector2(0.3f, 1.1f);
                                Assert.That(b.Height(p.x, p.y), Is.EqualTo(a.Height(p.x, p.y)));
                            }
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(noKit);
            }
        }

        [Test, Timeout(600000)]
        public void KeepOuts_AreNeverBroken_OverSeeds0To199_EveryPar()
        {
            var report = new System.Text.StringBuilder();
            int checkedProps = 0;
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < 200; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                    foreach (CourseProp prop in layout.Props)
                    {
                        checkedProps++;
                        string broken = BrokenKeepOut(layout, prop);
                        if (broken != null && report.Length < 3000)
                        {
                            report.Append($"seed {seed} par {par} {prop.Kind} at {prop.Root}: {broken}\n");
                        }
                    }
                }
            }

            Assert.That(checkedProps, Is.GreaterThan(1000));
            Assert.That(report.ToString(), Is.Empty);
        }

        [Test]
        public void Clusters_StayWithinTheCountsOfTheirPar()
        {
            foreach (int par in new[] { 3, 4, 5 })
            {
                PropDensity density = Settings.Props.Density(par);
                for (int seed = 0; seed < 60; seed++)
                {
                    var members = new Dictionary<int, int>();
                    foreach (CourseProp prop in CourseGenerator.Generate(seed, par, Settings).Props)
                    {
                        // The built props (PR 4.4) have their own cluster numbers and counts, tested apart.
                        if (prop.Cluster >= PropPlacement.BuiltBase)
                        {
                            continue;
                        }

                        members[prop.Cluster] = members.TryGetValue(prop.Cluster, out int n) ? n + 1 : 1;
                    }

                    Assert.That(members.Count, Is.LessThanOrEqualTo(density.Clusters.y), $"seed {seed} par {par}");
                    foreach (KeyValuePair<int, int> cluster in members)
                    {
                        Assert.That(cluster.Key, Is.LessThan(density.Clusters.y));
                        Assert.That(cluster.Value, Is.InRange(2, density.Members.y), $"seed {seed} par {par} cluster {cluster.Key}");
                    }
                }
            }
        }

        [Test]
        public void FewerClusters_LeaveTheFirstOnesWhereTheyWere()
        {
            // One sub-stream per cluster: asking for two clusters instead of three keeps the first two exactly.
            LevelGenSettings three = Copy(s => s.FindProperty("props.par4.clusters").vector2IntValue = new Vector2Int(3, 3));
            LevelGenSettings two = Copy(s => s.FindProperty("props.par4.clusters").vector2IntValue = new Vector2Int(2, 2));
            try
            {
                int compared = 0;
                for (int seed = 0; seed < 10; seed++)
                {
                    IReadOnlyList<CourseProp> a = CourseGenerator.Generate(seed, 4, three).Props;
                    var b = new List<CourseProp>();
                    foreach (CourseProp prop in CourseGenerator.Generate(seed, 4, two).Props)
                    {
                        // The built props (PR 4.4) come after the clusters and are compared in their own test.
                        if (prop.Cluster < PropPlacement.BuiltBase)
                        {
                            b.Add(prop);
                        }
                    }

                    var kept = new List<CourseProp>();
                    foreach (CourseProp prop in a)
                    {
                        if (prop.Cluster < 2)
                        {
                            kept.Add(prop);
                        }
                    }

                    Assert.That(b.Count, Is.EqualTo(kept.Count), $"seed {seed}");
                    for (int i = 0; i < b.Count; i++)
                    {
                        Assert.That(b[i].Root, Is.EqualTo(kept[i].Root));
                        Assert.That(b[i].Seed, Is.EqualTo(kept[i].Seed));
                        compared++;
                    }
                }

                Assert.That(compared, Is.GreaterThan(10));
            }
            finally
            {
                Object.DestroyImmediate(three);
                Object.DestroyImmediate(two);
            }
        }

        [Test]
        public void DisplayBuild_GivesTheShapesTheBallStrikes_BitForBit()
        {
            // TerrainView builds the drawn props from the same seeds with the palette; the shapes must be the placement's own.
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < 6; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                    var shapes = new List<PropShape>();
                    TerrainMeshData mesh = PropBuilder.BuildCourseProps(layout.Props, Settings.PropKit, Palette, shapes);
                    Assert.That(shapes.Count, Is.EqualTo(layout.PropShapes.Count), $"seed {seed} par {par}");
                    for (int i = 0; i < shapes.Count; i++)
                    {
                        PropShape a = shapes[i], b = layout.PropShapes[i];
                        Assert.That(a.Kind, Is.EqualTo(b.Kind));
                        Assert.That(a.Material, Is.EqualTo(b.Material));
                        Assert.That(a.Center.x, Is.EqualTo(b.Center.x));
                        Assert.That(a.Center.y, Is.EqualTo(b.Center.y));
                        Assert.That(a.Center.z, Is.EqualTo(b.Center.z));
                        Assert.That(a.Extents.x, Is.EqualTo(b.Extents.x));
                        Assert.That(a.Extents.y, Is.EqualTo(b.Extents.y));
                        Assert.That(a.Extents.z, Is.EqualTo(b.Extents.z));
                        Assert.That(a.Yaw, Is.EqualTo(b.Yaw));
                    }

                    Assert.That(mesh.TriangleCount > 0, Is.EqualTo(layout.Props.Count > 0));
                }
            }
        }

        [Test]
        public void Footprints_HoldEveryShapeOnTheGround()
        {
            // The keep-outs test a prop by the reach of its collision shapes (fitted to the mesh): every shape's outline on the
            // ground stays within it.
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < 30; seed++)
                {
                    foreach (CourseProp prop in CourseGenerator.Generate(seed, par, Settings).Props)
                    {
                        PropPlacement.Reach(prop.Root, prop.End, prop.Shapes, out float radius, out _);
                        var from = new Vector2(prop.Root.x, prop.Root.z);
                        var to = new Vector2(prop.End.x, prop.End.z);
                        foreach (PropShape shape in prop.Shapes)
                        {
                            foreach (Vector2 corner in GroundCorners(shape))
                            {
                                Assert.That(Geo.DistanceToSegment(corner, from, to), Is.LessThanOrEqualTo(radius + Tolerance), $"seed {seed} par {par} {prop.Kind}");
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void HandMadeCourses_HaveNoProps()
        {
            var shape = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>("Assets/_Project/Settings/Par3CourseShape.asset");
            Assert.That(shape.ToLayout().Props, Is.Empty);
        }

        [Test]
        public void CheckedCourses_PassWithTheirPropsAndThePole()
        {
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < 8; seed++)
                {
                    CheckedCourseResult result = CheckedCourse.Generate(seed, par, Settings, Shot);
                    Assert.That(result.Passed, Is.True, $"seed {seed} par {par}");
                    Assert.That(CourseChecks.Run(result.Layout, Shot, Settings, true), Is.EqualTo(CourseCheck.None), $"seed {seed} par {par}");
                    CourseLayout bare = CourseGenerator.Generate(seed, par, Settings, result.Variant, props: false);
                    Assert.That(result.Layout.Islands, Is.EqualTo(bare.Islands));
                    Assert.That(result.Layout.Bunkers, Is.EqualTo(bare.Bunkers));
                }
            }
        }

        [Test]
        public void APropThatBreaksACheck_IsDropped_NotTheCourse()
        {
            // A tall stone block right in front of the spot the approach check plays from (the safe route's last rest spot) sends
            // every approach back: the check fails with it. The struck props go (the block, and any placed prop a bounce reached); one out of every ball's reach (30 m up), struck by nothing, stays.
            CheckedCourseResult result = CheckedCourse.Generate(0, 3, Settings, Shot);
            CourseLayout layout = result.Layout;
            Vector2 tee = layout.Plan.SafeRoute[layout.Plan.SafeRoute.Count - 2];
            Vector2 toHole = (layout.Hole - tee).normalized;
            var fields = new CourseFields(layout);

            Vector2 wallAt = tee + (toHole * 1.5f);
            var wall = new CourseProp(PropKind.Wall, new Vector3(wallAt.x, 0f, wallAt.y), new Vector3(wallAt.x, 0f, wallAt.y), 1f, 0, 99,
                new[] { PropShape.Box(new Vector3(wallAt.x, fields.Height(wallAt.x, wallAt.y) + 1.5f, wallAt.y), new Vector3(0.2f, 1.5f, 3f), Mathf.Atan2(toHole.y, toHole.x), PropMaterial.Stone) });
            Vector2 behind = tee - (toHole * 2.5f);
            var rock = new CourseProp(PropKind.Rock, new Vector3(behind.x, 0f, behind.y), new Vector3(behind.x, 0f, behind.y), 0.4f, 0, 98,
                new[] { PropShape.Ellipsoid(new Vector3(behind.x, fields.Height(behind.x, behind.y) + 30f, behind.y), new Vector3(0.2f, 0.15f, 0.2f), PropMaterial.Stone) });

            var props = new List<CourseProp>(layout.Props) { wall, rock };
            CourseLayout blocked = layout.WithProps(props);
            Assert.That(CourseChecks.Run(blocked, Shot, Settings, true), Is.Not.EqualTo(CourseCheck.None));

            CourseLayout kept = CheckedCourse.KeepProps(blocked, CourseCheck.None, Shot, Settings, 0, 3, out int dropped);
            Assert.That(dropped, Is.GreaterThanOrEqualTo(1));
            Assert.That(kept.Props.Count, Is.EqualTo(props.Count - dropped));
            Assert.That(kept.Props, Has.None.Matches<CourseProp>(p => p.Cluster == 99));
            Assert.That(kept.Props, Has.Some.Matches<CourseProp>(p => p.Cluster == 98));
            Assert.That(CourseChecks.Run(kept, Shot, Settings, true), Is.EqualTo(CourseCheck.None));
        }

        [Test]
        public void ShadowRule_MatchesMainsSun()
        {
            // Generation can't see the sun, so the shadow direction and length are settings; they must match the scene's sun.
            Scene main = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity", OpenSceneMode.Additive);
            try
            {
                Light sun = null;
                foreach (GameObject root in main.GetRootGameObjects())
                {
                    if (root.name == "Sun")
                    {
                        sun = root.GetComponent<Light>();
                    }
                }

                Vector3 light = sun.transform.forward;
                var ground = new Vector2(light.x, light.z);
                Assert.That(Mathf.DeltaAngle(Geo.HeadingOf(ground), Settings.Props.ShadowHeading), Is.InRange(-1f, 1f));
                Assert.That(ground.magnitude / -light.y, Is.EqualTo(Settings.Props.ShadowLength).Within(0.01f));
            }
            finally
            {
                EditorSceneManager.CloseScene(main, true);
            }
        }

        /// <summary>
        /// Which keep-out this prop breaks, or null. Sampled from the true outlines and the safe route's capsule samples, not from
        /// <see cref="PropZones"/>.
        /// </summary>
        internal static string BrokenKeepOut(CourseLayout layout, CourseProp prop)
        {
            PropRules rules = Settings.Props;

            // A rake lies flat in a bunker and never collides (M4 PR 4.4): it keeps out of nothing.
            if (prop.Kind == PropKind.Rake)
            {
                return null;
            }

            // A bridge spans a pond: its footprint is over water by design, so the ground, pond and edge rules don't apply to it.
            bool bridge = prop.Kind == PropKind.Bridge;
            PropPlacement.Reach(prop.Root, prop.End, prop.Shapes, out float radius, out float height);
            if (bridge)
            {
                // Only what stands above the higher bank casts a shadow: the block's foot is in the water.
                height = prop.Shapes[0].Center.y + prop.Shapes[0].Extents.y - Mathf.Max(prop.Root.y, prop.End.y);
            }

            var fields = new CourseFields(layout);
            CoursePlan plan = layout.Plan;
            float corridor = plan.ShotCorridorHalfWidth;
            bool rim = prop.Kind == PropKind.RoundRock;

            var circles = new List<Vector2>();
            var from = new Vector2(prop.Root.x, prop.Root.z);
            var to = new Vector2(prop.End.x, prop.End.z);
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / 0.25f));
            for (int i = 0; i <= samples; i++)
            {
                circles.Add(Vector2.Lerp(from, to, (float)i / samples));
            }

            Vector2 shadow = Geo.Dir(Geo.HeadingOf(layout.Centreline.Points.Count > 2 ? layout.Hole - layout.Tee : Vector2.right) + rules.ShadowHeading);
            foreach (Vector2 c in circles)
            {
                if (!bridge && fields.Surface(Geo.At(c)) != CourseSurface.Rough)
                {
                    return "not on rough";
                }

                if (!bridge && rules.FairwayMargin >= 0f)
                {
                    // The whole footprint plus the margin stays off the fairway (PR 4.4, director).
                    for (int a = 0; a < 24; a++)
                    {
                        float reach = radius + rules.FairwayMargin - Tolerance;
                        if (fields.Surface(Geo.At(c + (Geo.Dir(a * 15f) * reach))) == CourseSurface.Fairway
                            || fields.Surface(Geo.At(c + (Geo.Dir(a * 15f) * reach * 0.5f))) == CourseSurface.Fairway)
                        {
                            return "fairway";
                        }
                    }
                }

                if (-fields.Island(Geo.At(c)) < rules.EdgeMargin - Tolerance)
                {
                    return "too close to the edge";
                }

                if (Vector2.Distance(c, layout.Tee) < rules.TeeKeepOut + radius - Tolerance)
                {
                    return "tee";
                }

                foreach (ShotLine line in plan.Lines)
                {
                    for (int k = 0; k <= 10; k++)
                    {
                        Vector2 s = c + (shadow * (height * rules.ShadowLength * k / 10f));
                        if (Geo.DistanceToSegment(s, line.From, line.To) < corridor + radius - Tolerance)
                        {
                            return $"{line.Kind} corridor{(k > 0 ? " (shadow)" : "")}";
                        }
                    }
                }

                for (int r = 1; r + 1 < plan.SafeRoute.Count; r++)
                {
                    float along = layout.Centreline.Project(plan.SafeRoute[r], out _);
                    for (float s = along - Settings.SafeLandingBack; s <= along + Settings.SafeLandingAhead + 0.001f; s += 0.5f)
                    {
                        Vector2 centre = layout.Centreline.PointAt(s);
                        Vector2 side = Geo.Left(Geo.PathDirection(layout.Centreline, s));
                        for (float across = -Settings.SafeLandingHalfWidth; across <= Settings.SafeLandingHalfWidth + 0.001f; across += 0.5f)
                        {
                            if (Vector2.Distance(c, centre + (side * across)) < radius - Tolerance)
                            {
                                return "landing capsule";
                            }
                        }
                    }
                }

                // The end kept for M5: nothing past the hole, nothing in the fan around +X.
                float past = layout.Centreline.Project(c, out _) - layout.Centreline.Length;
                if (past > -radius + Tolerance)
                {
                    return "past the hole";
                }

                Vector2 fromHole = c - layout.Hole;
                if (Vector2.Angle(Vector2.right, fromHole) <= rules.EndFan * 0.5f)
                {
                    return "+X fan";
                }

                for (int a = 0; a < 24; a++)
                {
                    Vector2 dir = Geo.Dir(a * 15f);
                    if (fields.Green(Geo.At(c + (dir * (rules.GreenKeepOut + radius - Tolerance)))) < 0f)
                    {
                        return "green";
                    }

                    if (fields.Bunker(Geo.At(c + (dir * (rules.HazardKeepOut + radius - Tolerance)))) < 0f)
                    {
                        return "bunker";
                    }

                    float pond = rim ? rules.RimGap.x : rules.HazardKeepOut;
                    if (!bridge && fields.Pond(Geo.At(c + (dir * (pond + radius - Tolerance)))) < 0f)
                    {
                        return "pond";
                    }
                }
            }

            return null;
        }

        /// <summary>Points around a shape's widest outline on the ground plane.</summary>
        private static IEnumerable<Vector2> GroundCorners(PropShape shape)
        {
            var centre = new Vector2(shape.Center.x, shape.Center.z);
            if (shape.Kind == PropShapeKind.Box)
            {
                Vector2 along = new Vector2(Mathf.Cos(shape.Yaw), Mathf.Sin(shape.Yaw));
                Vector2 across = Geo.Left(along);
                for (int i = 0; i < 4; i++)
                {
                    yield return centre + (along * (shape.Extents.x * (i < 2 ? 1f : -1f))) + (across * (shape.Extents.z * (i % 2 == 0 ? 1f : -1f)));
                }

                yield break;
            }

            for (int a = 0; a < 36; a++)
            {
                Vector2 dir = Geo.Dir(a * 10f);
                yield return centre + new Vector2(dir.x * shape.Extents.x, dir.y * shape.Extents.z);
            }
        }
    }
}
