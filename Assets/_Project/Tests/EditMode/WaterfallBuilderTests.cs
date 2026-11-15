using System.Collections.Generic;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>M4 PR 4.8: where the waterfalls go (segment finding and merging) and what the sheet looks like (lip, bounds, tone, light, determinism).</summary>
    public class WaterfallBuilderTests
    {
        private const float Tolerance = 0.05f;

        private PaletteSettings _palette;
        private WaterfallSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _palette = ScriptableObject.CreateInstance<PaletteSettings>();
            _settings = ScriptableObject.CreateInstance<WaterfallSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_palette);
            Object.DestroyImmediate(_settings);
        }

        // A straight edge along +X with the island to the north (+Z), so outward is -Z.
        private static EdgeSegment Piece(float x0, float x1, EdgeSurface surface = EdgeSurface.Water, float y = -0.3f)
        {
            return new EdgeSegment(new Vector3(x0, y, 0f), new Vector3(x1, y, 0f), Vector3.back, surface);
        }

        [Test]
        public void FindRuns_JoinsTouchingWaterPiecesIntoOneRun()
        {
            var edge = new List<EdgeSegment> { Piece(2f, 3f), Piece(0f, 1f), Piece(1f, 2f) };

            List<WaterfallRun> runs = WaterfallBuilder.FindRuns(edge, Tolerance, 0.5f);

            Assert.AreEqual(1, runs.Count);
            Assert.AreEqual(3f, runs[0].Length, 0.01f);
        }

        [Test]
        public void FindRuns_IgnoresLandAndKeepsLandBetweenTwoRunsApart()
        {
            // Water, a piece of land, water: the sheets never bridge over the land.
            var edge = new List<EdgeSegment> { Piece(0f, 2f), Piece(2f, 3f, EdgeSurface.Land), Piece(3f, 5f) };

            List<WaterfallRun> runs = WaterfallBuilder.FindRuns(edge, Tolerance, 0.5f);

            Assert.AreEqual(2, runs.Count);
            Assert.AreEqual(2f, runs[0].Length, 0.01f);
            Assert.AreEqual(2f, runs[1].Length, 0.01f);
        }

        [Test]
        public void FindRuns_GapTolerance_IsInWholeCentimetres()
        {
            // A 4 cm gap joins at a 5 cm tolerance, a 6 cm gap does not.
            Assert.AreEqual(1, WaterfallBuilder.FindRuns(new List<EdgeSegment> { Piece(0f, 1f), Piece(1.04f, 2f) }, Tolerance, 0.5f).Count);
            Assert.AreEqual(2, WaterfallBuilder.FindRuns(new List<EdgeSegment> { Piece(0f, 1f), Piece(1.06f, 2f) }, Tolerance, 0.5f).Count);
        }

        [Test]
        public void FindRuns_DropsRunsShorterThanTheMinimum()
        {
            var edge = new List<EdgeSegment> { Piece(0f, 0.4f), Piece(5f, 7f) };

            List<WaterfallRun> runs = WaterfallBuilder.FindRuns(edge, Tolerance, 0.6f);

            Assert.AreEqual(1, runs.Count);
            Assert.AreEqual(2f, runs[0].Length, 0.01f);
        }

        [Test]
        public void FindRuns_DoesNotDependOnTheOrderOrTheDirectionOfThePieces()
        {
            var forward = new List<EdgeSegment> { Piece(0f, 1f), Piece(1f, 2f), Piece(2f, 3f), Piece(6f, 8f) };
            var shuffled = new List<EdgeSegment>
            {
                Piece(8f, 6f), Piece(2f, 3f), Piece(0f, 1f), Piece(1f, 2f),
            };

            List<WaterfallRun> a = WaterfallBuilder.FindRuns(forward, Tolerance, 0.5f);
            List<WaterfallRun> b = WaterfallBuilder.FindRuns(shuffled, Tolerance, 0.5f);

            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Points.Count, b[i].Points.Count);
                for (int p = 0; p < a[i].Points.Count; p++)
                {
                    Assert.AreEqual(0f, Vector3.Distance(a[i].Points[p], b[i].Points[p]), 0.011f);
                }
            }
        }

        [Test]
        public void FindRuns_WalksWithOutwardOnTheRight()
        {
            // The run's direction d always satisfies Cross(d, up) = outward. Outward is -Z here, so d is -X, whichever way the piece was given.
            foreach (EdgeSegment piece in new[] { Piece(3f, 0f), Piece(0f, 3f) })
            {
                List<WaterfallRun> runs = WaterfallBuilder.FindRuns(new List<EdgeSegment> { piece }, Tolerance, 0.5f);

                Assert.Less(runs[0].Points[1].x, runs[0].Points[0].x);
            }
        }

        [Test]
        public void Build_MakesNothingWithoutWater()
        {
            TerrainMeshData mesh = WaterfallBuilder.Build(_settings, _palette, new List<EdgeSegment> { Piece(0f, 4f, EdgeSurface.Land) });

            Assert.AreEqual(0, mesh.TriangleCount);
        }

        [Test]
        public void Build_SheetStartsOnTheEdgeAtTheWaterLevelAndFallsOutward()
        {
            const float level = -0.3f;
            TerrainMeshData mesh = WaterfallBuilder.Build(_settings, _palette, new List<EdgeSegment> { Piece(0f, 5f, EdgeSurface.Water, level) });

            Assert.Greater(mesh.TriangleCount, 0);
            float top = float.MinValue;
            float bottom = float.MaxValue;
            int onLip = 0;
            foreach (Vector3 point in mesh.Positions)
            {
                top = Mathf.Max(top, point.y);
                bottom = Mathf.Min(bottom, point.y);

                // Outward is -Z: the sheet never goes back inside the island (Z > 0), and never past the run's ends by more than a jitter.
                Assert.LessOrEqual(point.z, 0.001f);
                Assert.GreaterOrEqual(point.x, -0.1f);
                Assert.LessOrEqual(point.x, 5.1f);

                // The lip row lies exactly on the edge, where the pond's surface ends: no gap, no earth line at the joint.
                if (point.y > level - 0.0001f)
                {
                    Assert.AreEqual(0f, point.z, 0.0001f, $"a vertex at the water level off the edge: {point}");
                    onLip++;
                }
            }

            Assert.Greater(onLip, 0);
            Assert.AreEqual(level, top, 0.001f, "the lip is the highest point, flush with the water");
            Assert.GreaterOrEqual(bottom, level - _settings.FallDepth - _settings.RowHeight, "a ragged tip reaches a little below the fall");
            Assert.Less(bottom, level - (_settings.FallDepth * 0.5f));
        }

        [Test]
        public void Build_GivesOneSheetPerRun_ThatNarrowsAndNeverBridgesLand()
        {
            const float level = -0.3f;
            var edge = new List<EdgeSegment> { Piece(0f, 3f), Piece(3f, 5f, EdgeSurface.Land), Piece(5f, 8f) };
            TerrainMeshData mesh = WaterfallBuilder.Build(_settings, _palette, edge);

            float footLow = float.MaxValue;
            float footHigh = float.MinValue;
            foreach (Vector3 point in mesh.Positions)
            {
                Assert.IsFalse(point.x > 3.15f && point.x < 4.85f, $"the sheet bridges the land between two runs at {point}");
                if (point.x < 4f && point.y < level - (_settings.FallDepth * 0.85f))
                {
                    footLow = Mathf.Min(footLow, point.x);
                    footHigh = Mathf.Max(footHigh, point.x);
                }
            }

            Assert.Less(footHigh - footLow, 3f * 0.9f, "the sheet narrows toward its foot");
            Assert.Greater(footHigh - footLow, 3f * 0.25f, "the sheet stays a curtain, not a thread");
        }

        [Test]
        public void RowDepths_RunFromTheLipToTheFallDepth_DenserAtTheTop_AndOutwardGrows()
        {
            List<float> depths = WaterfallBuilder.RowDepths(_settings);

            Assert.AreEqual(0f, depths[0]);
            Assert.AreEqual(_settings.FallDepth, depths[depths.Count - 1], 0.0001f);
            Assert.LessOrEqual(depths.Count - 1, 15, "the flow phase packs at most 16 rows");
            Assert.Less(depths[1], _settings.BrinkRadius, "the brink gets rows of its own");
            for (int r = 1; r < depths.Count; r++)
            {
                Assert.Greater(depths[r], depths[r - 1]);
            }

            Assert.AreEqual(0f, WaterfallBuilder.Outward(_settings, 0f), "the water leaves the lip on the edge");
            float previous = 0f;
            for (float depth = 0.01f; depth <= _settings.FallDepth; depth += 0.01f)
            {
                float outward = WaterfallBuilder.Outward(_settings, depth);
                Assert.GreaterOrEqual(outward, previous - 0.0001f, $"depth {depth}");
                previous = outward;
            }
        }

        [Test]
        public void Build_TopTakesThePondsShoreToneAndLight_FootFadesIntoTheSky()
        {
            TerrainMeshData mesh = WaterfallBuilder.Build(_settings, _palette, new List<EdgeSegment> { Piece(0f, 4f) });

            Assert.IsNotNull(mesh.Normals);
            Assert.AreEqual(mesh.Positions.Count, mesh.Normals.Count);
            for (int i = 0; i < mesh.Positions.Count; i++)
            {
                // Row 0 of the flow phase is the brink's first row: lit as if facing up, like the pond.
                if (mesh.Phase[i] < TerrainMeshData.FallPhase(1, 0f))
                {
                    Assert.Less(Vector3.Angle(Vector3.up, mesh.Normals[i]), 2f, $"vertex {i}");
                }
            }

            // Row 0: the pond's colour where it meets the edge (here WaterShallow) with the pond's tone spread. Just below: the foam,
            // near white. At the foot: the pale blue, within the light spread that keeps the curtain faceted.
            float halfSpread = _palette.WaterToneVariation * 0.5f;
            foreach (float hash in new[] { 0.1f, 0.5f, 0.95f })
            {
                Color top = WaterfallBuilder.ToneAt(_settings, _palette, _palette.WaterShallow, 0, hash, 0f);
                int level = Mathf.Min(4, (int)(hash * 5));
                float gain = 1f + (((level / 4f) - 0.5f) * 2f * _palette.WaterToneVariation);
                Assert.AreEqual(Mathf.Clamp01(_palette.WaterShallow.r * gain), top.r, 0.0001f);
                Assert.AreEqual(Mathf.Clamp01(_palette.WaterShallow.g * gain), top.g, 0.0001f);
                Assert.AreEqual(Mathf.Clamp01(_palette.WaterShallow.b * gain), top.b, 0.0001f);

                Color foam = WaterfallBuilder.ToneAt(_settings, _palette, _palette.WaterShallow, 1, hash, 0.01f);
                Assert.AreEqual(_settings.FoamTone.g, foam.g, (halfSpread * _settings.FoamTone.g) + 0.01f);

                Color foot = WaterfallBuilder.ToneAt(_settings, _palette, _palette.WaterShallow, 6, hash, 1f);
                Assert.AreEqual(_settings.FadeTone.r, foot.r, (halfSpread * _settings.FadeTone.r) + 0.0001f);
                Assert.AreEqual(_settings.FadeTone.g, foot.g, (halfSpread * _settings.FadeTone.g) + 0.0001f);
                Assert.AreEqual(_settings.FadeTone.b, foot.b, (halfSpread * _settings.FadeTone.b) + 0.0001f);
            }
        }

        [Test]
        public void Build_AlphaIsOpaqueAtTheLipAndNeverRisesDownToClearAtTheFoot()
        {
            const float level = -0.3f;
            TerrainMeshData mesh = WaterfallBuilder.Build(_settings, _palette, new List<EdgeSegment> { Piece(0f, 4f, EdgeSurface.Water, level) });

            List<float> depths = WaterfallBuilder.RowDepths(_settings);
            Assert.AreEqual(255, WaterfallBuilder.AlphaAt(_settings, 0f));
            Assert.AreEqual(0, WaterfallBuilder.AlphaAt(_settings, _settings.FallDepth));
            for (int r = 1; r < depths.Count; r++)
            {
                Assert.LessOrEqual(WaterfallBuilder.AlphaAt(_settings, depths[r]), WaterfallBuilder.AlphaAt(_settings, depths[r - 1]), $"row {r}");
            }

            // Every vertex on the brink is opaque, so the joint with the pond (opaque) shows no see-through line.
            for (int i = 0; i < mesh.Positions.Count; i++)
            {
                if (mesh.Positions[i].y > level - _settings.BrinkRadius - 0.01f)
                {
                    Assert.AreEqual(255, mesh.Colors[i].a, $"vertex {i} at {mesh.Positions[i]}");
                }
            }
        }

        [Test]
        public void Build_CurtainBulgesInItsMiddle_ButTheLipStaysOnTheEdge()
        {
            const float level = -0.3f;
            TerrainMeshData mesh = WaterfallBuilder.Build(_settings, _palette, new List<EdgeSegment> { Piece(0f, 4f, EdgeSurface.Water, level) });

            // Outward is -Z. Halfway down, the middle of the run is further out than near its ends.
            float midDepth = level - (_settings.FallDepth * 0.5f);
            float middleOut = 0f;
            float endOut = 0f;
            foreach (Vector3 point in mesh.Positions)
            {
                if (Mathf.Abs(point.y - midDepth) > 1.2f)
                {
                    continue;
                }

                if (Mathf.Abs(point.x - 2f) < 0.4f)
                {
                    middleOut = Mathf.Max(middleOut, -point.z);
                }
                else if (point.x < 0.4f || point.x > 3.6f)
                {
                    endOut = Mathf.Max(endOut, -point.z);
                }
            }

            float bulge = Mathf.Min(_settings.MaxBulge, _settings.BulgePerMetre * 4f);
            Assert.Greater(middleOut, endOut + (bulge * 0.5f), "the curtain bulges out in its middle");
        }

        [Test]
        public void Build_CheeksCloseTheSidesBackToTheCliff()
        {
            const float level = -0.3f;
            TerrainMeshData mesh = WaterfallBuilder.Build(_settings, _palette, new List<EdgeSegment> { Piece(0f, 4f, EdgeSurface.Water, level) });

            // Below the brink, vertices on the edge plane itself (z = 0) can only be the cheeks' back corners, at both ends of the run.
            bool startCheek = false;
            bool endCheek = false;
            foreach (Vector3 point in mesh.Positions)
            {
                if (point.y < level - _settings.BrinkRadius - 0.5f && Mathf.Abs(point.z) < 0.001f)
                {
                    startCheek |= point.x < 2f;
                    endCheek |= point.x > 2f;
                }
            }

            Assert.IsTrue(startCheek, "no cheek at the start of the run");
            Assert.IsTrue(endCheek, "no cheek at the end of the run");
        }

        [Test]
        public void Build_FootIsCalm_WithinTheFootJitterOfTheFallDepth()
        {
            const float level = -0.3f;
            TerrainMeshData mesh = WaterfallBuilder.Build(_settings, _palette, new List<EdgeSegment> { Piece(0f, 4f, EdgeSurface.Water, level) });

            float bottom = float.MaxValue;
            foreach (Vector3 point in mesh.Positions)
            {
                bottom = Mathf.Min(bottom, point.y);
            }

            Assert.GreaterOrEqual(bottom, level - _settings.FallDepth - 0.15f, "a spike below the foot");
            Assert.Less(bottom, level - _settings.FallDepth + 0.15f);
        }

        [Test]
        public void Build_ColumnsTakeTheirOwnLight()
        {
            TerrainMeshData mesh = WaterfallBuilder.Build(_settings, _palette, new List<EdgeSegment> { Piece(0f, 4f) });

            // On a straight run, the bulge and each column's yaw give the columns different lighting normals below the brink.
            var normals = new HashSet<Vector3>();
            for (int i = 0; i < mesh.Positions.Count; i++)
            {
                if (mesh.Phase[i] >= TerrainMeshData.FallPhase(4, 0f) && Mathf.Abs(mesh.Normals[i].y) < 0.99f)
                {
                    normals.Add(mesh.Normals[i]);
                }
            }

            Assert.Greater(normals.Count, 3, "the columns share one light: the curtain reads flat");
        }

        [Test]
        public void FindRuns_CarryThePondsToneAtTheEdge_WeightedByLength()
        {
            var dark = new Color(0.2f, 0.4f, 0.5f, 1f);
            var light = new Color(0.4f, 0.8f, 0.7f, 1f);
            var edge = new List<EdgeSegment>
            {
                new EdgeSegment(new Vector3(0f, -0.3f, 0f), new Vector3(1f, -0.3f, 0f), Vector3.back, EdgeSurface.Water, dark),
                new EdgeSegment(new Vector3(1f, -0.3f, 0f), new Vector3(4f, -0.3f, 0f), Vector3.back, EdgeSurface.Water, light),
            };

            List<WaterfallRun> runs = WaterfallBuilder.FindRuns(edge, _settings.GapTolerance, _settings.MinRunLength);

            Assert.AreEqual(1, runs.Count);
            Color expected = (dark * 0.25f) + (light * 0.75f);
            Assert.AreEqual(expected.r, runs[0].Tone.r, 0.0001f);
            Assert.AreEqual(expected.g, runs[0].Tone.g, 0.0001f);
            Assert.AreEqual(expected.b, runs[0].Tone.b, 0.0001f);
            Assert.AreEqual(1f, runs[0].Tone.a, 0.0001f);
        }

        [Test]
        public void Build_IsDeterministic()
        {
            var edge = new List<EdgeSegment> { Piece(0f, 3f), Piece(3f, 6f) };

            TerrainMeshData a = WaterfallBuilder.Build(_settings, _palette, edge);
            TerrainMeshData b = WaterfallBuilder.Build(_settings, _palette, edge);

            Assert.AreEqual(a.Positions.Count, b.Positions.Count);
            for (int i = 0; i < a.Positions.Count; i++)
            {
                Assert.AreEqual(a.Positions[i], b.Positions[i]);
                Assert.AreEqual(a.Colors[i], b.Colors[i]);
                Assert.AreEqual(a.Normals[i], b.Normals[i]);
            }
        }

        [Test]
        public void Build_WaterfallFacetsCarryTheFlowPhaseAndNeverAPondGlintHash()
        {
            TerrainMeshData mesh = WaterfallBuilder.Build(_settings, _palette, new List<EdgeSegment> { Piece(0f, 4f) });

            Assert.IsNotNull(mesh.Sway);
            for (int i = 0; i < mesh.Phase.Count; i++)
            {
                // x is always 0, so the sway stage (weight = UV1.x) never moves a vertex. y is in the waterfall band above 1, which no
                // pond glint hash (0..1) reaches: the shader's pond branch never takes it.
                Assert.AreEqual(0f, mesh.Sway[i]);
                float y = mesh.Phase[i];
                Assert.IsTrue(y > 1.005f && y < 2f, $"UV1.y {y}");
            }
        }

        [Test]
        public void TerrainMeshes_KeepFlatNormalsFromTheirFaces()
        {
            // Only a waterfall sets lighting normals: every other mesh is built exactly as before (RecalculateNormals in ToMesh).
            var mesh = new TerrainMeshData();
            mesh.AddGlint(Vector3.zero, Vector3.right, Vector3.forward, Vector3.up, new Color32(1, 1, 1, 255), 0.5f);
            mesh.Add(Vector3.zero, Vector3.right, Vector3.up, Vector3.back, new Color32(1, 1, 1, 255));
            Assert.IsNull(mesh.Normals);

            TerrainResult terrain = TerrainBuilder.Build(CourseGenerator.Generate(2, 5, CourseGeneratorTests.Settings, 0, false), _palette);
            Assert.IsNull(terrain.Land.Normals);
            Assert.IsNull(terrain.Water.Normals);
        }

        [Test]
        public void FallPhase_PacksTheRowAndTheHashInTheBandAbovePondHashes()
        {
            for (int row = 0; row <= 15; row++)
            {
                foreach (float hash in new[] { 0.0001f, 0.5f, 1f })
                {
                    float phase = TerrainMeshData.FallPhase(row, hash);
                    Assert.Greater(phase, 1.005f);
                    Assert.Less(phase, 2f);

                    // What AmbientGlint_float decodes.
                    float packed = ((phase - 1.01f) / 0.98f) * 16f;
                    Assert.AreEqual(row, Mathf.FloorToInt(packed + 0.0001f), $"row {row} hash {hash}");
                    Assert.AreEqual(hash, (packed - row) / 0.99f, 0.001f);
                }
            }
        }

        [Test]
        public void PondFacets_KeepAGlintHashAtOrBelowOne()
        {
            var mesh = new TerrainMeshData();
            mesh.AddGlint(Vector3.zero, Vector3.right, Vector3.forward, Vector3.up, new Color32(1, 1, 1, 255), 1f);

            Assert.LessOrEqual(mesh.Phase[0], 1f, "a pond's hash never reaches the waterfall band above 1.005");
        }

        [Test]
        public void Build_OnGeneratedCourses_StaysOutsideTheIslandAndIsDeterministic()
        {
            LevelGenSettings generation = CourseGeneratorTests.Settings;
            int checkedCourses = 0;
            for (int seed = 0; seed < 50 && checkedCourses < 4; seed++)
            {
                CourseLayout layout = CourseGenerator.Generate(seed, 5, generation, 0, false);
                TerrainResult terrain = TerrainBuilder.Build(layout, _palette);
                TerrainMeshData mesh = WaterfallBuilder.Build(_settings, _palette, terrain.Edge);
                if (mesh.TriangleCount == 0)
                {
                    continue;
                }

                checkedCourses++;
                var fields = new CourseFields(layout);
                foreach (Vector3 point in mesh.Positions)
                {
                    Assert.GreaterOrEqual(fields.Island(point), -0.05f, $"seed {seed}: a vertex of the sheet is inside the island at {point}");
                }

                TerrainMeshData again = WaterfallBuilder.Build(_settings, _palette, TerrainBuilder.Build(layout, _palette).Edge);
                CollectionAssert.AreEqual(mesh.Positions, again.Positions, $"seed {seed}");
            }

            Assert.Greater(checkedCourses, 0, "no par 5 in seeds 0-49 has a pond at the edge");
        }
    }
}
