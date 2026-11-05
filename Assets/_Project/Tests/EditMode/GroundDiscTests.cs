using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class GroundDiscTests
    {
        private static readonly Color32 Tint = new Color32(10, 20, 30, 128);

        private static GroundDisc FlatDisc(int rings = 3, int segments = 16)
        {
            return new GroundDisc(1f, rings, segments, (x, z) => 0f, (x, z) => true);
        }

        [Test]
        public void Ring_HasNothingInsideItsInnerRadius_AndFacesUp()
        {
            var ring = new GroundDisc(1f, 3, 12, (x, z) => 0f, (x, z) => true, 0.6f);
            ring.Place(0f, 0f, 0f, Tint);

            for (int i = 0; i < ring.Triangles.Length; i += 3)
            {
                Vector3 a = ring.Positions[ring.Triangles[i]];
                Vector3 b = ring.Positions[ring.Triangles[i + 1]];
                Vector3 c = ring.Positions[ring.Triangles[i + 2]];

                Assert.That(a.magnitude, Is.GreaterThanOrEqualTo(0.6f - 0.0001f));
                Assert.That(b.magnitude, Is.GreaterThanOrEqualTo(0.6f - 0.0001f));
                Assert.That(c.magnitude, Is.GreaterThanOrEqualTo(0.6f - 0.0001f));
                Assert.That(Vector3.Cross(b - a, c - a).y, Is.GreaterThan(0f), $"triangle {i / 3}");
            }
        }

        [Test]
        public void Ring_ReachesItsOuterRadius()
        {
            var ring = new GroundDisc(1f, 2, 12, (x, z) => 0f, (x, z) => true, 0.6f);
            ring.Place(0f, 0f, 0f, Tint);

            float farthest = 0f;
            foreach (Vector3 position in ring.Positions)
            {
                farthest = Mathf.Max(farthest, position.magnitude);
            }

            Assert.That(farthest, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void EveryTriangleFacesUp()
        {
            GroundDisc disc = FlatDisc();
            disc.Place(0f, 0f, 0f, Tint);

            for (int i = 0; i < disc.Triangles.Length; i += 3)
            {
                Vector3 a = disc.Positions[disc.Triangles[i]];
                Vector3 b = disc.Positions[disc.Triangles[i + 1]];
                Vector3 c = disc.Positions[disc.Triangles[i + 2]];

                // On a front face in Unity, this cross product points toward the viewer.
                Assert.That(Vector3.Cross(b - a, c - a).y, Is.GreaterThan(0f), $"triangle {i / 3}");
            }
        }

        [Test]
        public void TrianglesUseEveryVertexAndStayInRange()
        {
            GroundDisc disc = FlatDisc(4, 12);
            var used = new bool[disc.Positions.Length];
            foreach (int index in disc.Triangles)
            {
                used[index] = true;
            }

            Assert.That(used, Has.All.True);
        }

        [Test]
        public void Place_CentersTheDiscWithinItsRadius()
        {
            GroundDisc disc = FlatDisc();
            disc.Place(5f, -3f, 0f, Tint);

            Assert.That(disc.Positions[0], Is.EqualTo(new Vector3(5f, 0f, -3f)));
            foreach (Vector3 position in disc.Positions)
            {
                float distance = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(5f, -3f));
                Assert.That(distance, Is.LessThanOrEqualTo(1.0001f));
            }
        }

        [Test]
        public void Place_FollowsTheGroundAndAddsTheLift()
        {
            var disc = new GroundDisc(1f, 2, 8, (x, z) => (x * 0.5f) + (z * 0.25f), (x, z) => true);
            disc.Place(2f, 4f, 0.1f, Tint);

            foreach (Vector3 position in disc.Positions)
            {
                Assert.That(position.y, Is.EqualTo((position.x * 0.5f) + (position.z * 0.25f) + 0.1f).Within(0.0001f));
            }
        }

        [Test]
        public void Place_HidesVerticesWithNoGroundBelow()
        {
            var disc = new GroundDisc(1f, 2, 8, (x, z) => 0f, (x, z) => x < 0f);
            disc.Place(0f, 0f, 0f, Tint);

            for (int i = 0; i < disc.Positions.Length; i++)
            {
                byte expected = disc.Positions[i].x < 0f ? Tint.a : (byte)0;
                Assert.That(disc.Colors[i].a, Is.EqualTo(expected), $"vertex at {disc.Positions[i]}");
            }
        }

        [Test]
        public void TheShadowStaysAboveTheFacetsAcrossTheRealCourse()
        {
            var shape = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>("Assets/_Project/Settings/Par3CourseShape.asset");
            var pieces = AssetDatabase.LoadAssetAtPath<PlayPieceSettings>("Assets/_Project/Settings/PlayPieces.asset");
            var ground = new BallGround(shape);
            var disc = new GroundDisc(pieces.ShadowRadius, pieces.DiscRings, pieces.DiscSegments, ground.DrawnHeight, ground.IsOnIsland);

            float worst = float.MaxValue;
            for (float x = -16f; x <= 16f; x += 0.17f)
            {
                for (float z = -7f; z <= 7f; z += 0.17f)
                {
                    if (ground.Surface(x, z) == CourseSurface.OffIsland || NearWater(ground, x, z, pieces.ShadowRadius))
                    {
                        continue;
                    }

                    disc.Place(x, z, pieces.ShadowLift, new Color32(0, 0, 0, 255));
                    worst = Mathf.Min(worst, LowestEdgeClearance(disc, ground));
                }
            }

            // Between its vertices, a disc crossing a crease cuts the corner. Its lift has to cover that.
            Assert.That(worst, Is.GreaterThan(0f), "A shadow's triangle dips under the facets.");
        }

        private static bool NearWater(BallGround ground, float x, float z, float radius)
        {
            // Over a bank, the disc spans the drop to the water, so it's expected to leave the facets there.
            float reach = radius + 0.3f;
            return ground.Surface(x - reach, z) == CourseSurface.Water
                || ground.Surface(x + reach, z) == CourseSurface.Water
                || ground.Surface(x, z - reach) == CourseSurface.Water
                || ground.Surface(x, z + reach) == CourseSurface.Water
                || ground.Surface(x, z) == CourseSurface.Water;
        }

        private static float LowestEdgeClearance(GroundDisc disc, BallGround ground)
        {
            float lowest = float.MaxValue;
            for (int i = 0; i < disc.Triangles.Length; i += 3)
            {
                Vector3 a = disc.Positions[disc.Triangles[i]];
                Vector3 b = disc.Positions[disc.Triangles[i + 1]];
                Vector3 c = disc.Positions[disc.Triangles[i + 2]];
                lowest = Mathf.Min(lowest, Clearance((a + b) * 0.5f, ground));
                lowest = Mathf.Min(lowest, Clearance((b + c) * 0.5f, ground));
                lowest = Mathf.Min(lowest, Clearance((c + a) * 0.5f, ground));
            }

            return lowest;
        }

        private static float Clearance(Vector3 point, BallGround ground)
        {
            return point.y - ground.DrawnHeight(point.x, point.z);
        }
    }
}
