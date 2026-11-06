using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class CourseHeightFieldTests
    {
        private CourseShapeSettings _shape;
        private CourseFields _fields;
        private CourseHeightField _field;

        [SetUp]
        public void SetUp()
        {
            // The real asset, not code defaults: this is the ground that ships, and the two can drift apart.
            _shape = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>("Assets/_Project/Settings/Par3CourseShape.asset");
            _fields = new CourseFields(_shape.ToLayout());
            _field = new CourseHeightField(_shape.ToLayout());
        }

        [Test]
        public void Height_MeetsTheTerrainAtEveryVertex()
        {
            for (int x = -17; x <= 17; x++)
            {
                for (int z = -7; z <= 7; z++)
                {
                    Assert.That(_field.Height(x, z), Is.EqualTo(_fields.Height(x, z)).Within(0.0001f), $"at {x}, {z}");
                }
            }
        }

        [Test]
        public void FacetHeight_MatchesTheVisibleTriangles()
        {
            var palette = ScriptableObject.CreateInstance<PaletteSettings>();
            TerrainMeshData land = TerrainBuilder.Build(_shape.ToLayout(), palette).Land;
            Object.DestroyImmediate(palette);

            int checkedFacets = 0;
            for (int i = 0; i < land.Positions.Count; i += 3)
            {
                Vector3 a = land.Positions[i];
                Vector3 b = land.Positions[i + 1];
                Vector3 c = land.Positions[i + 2];

                // Only ground facets: the land mesh also holds near-vertical walls under its edges.
                if (Vector3.Cross(b - a, c - a).normalized.y < 0.7f)
                {
                    continue;
                }

                Vector3 centroid = (a + b + c) / 3f;
                Assert.That(_field.FacetHeight(centroid.x, centroid.z), Is.EqualTo(centroid.y).Within(0.001f), $"at {centroid}");
                checkedFacets++;
            }

            Assert.That(checkedFacets, Is.GreaterThan(500));
        }

        [Test]
        public void Slope_MatchesTheFeltHeightsChange()
        {
            const float Step = 0.01f;
            for (float x = -15.3f; x < 15f; x += 1.37f)
            {
                for (float z = -5.3f; z < 5f; z += 1.13f)
                {
                    Vector2 gradient = _field.Slope(x, z);
                    float alongX = (_field.FeltHeight(x + Step, z) - _field.FeltHeight(x - Step, z)) / (Step * 2f);
                    float alongZ = (_field.FeltHeight(x, z + Step) - _field.FeltHeight(x, z - Step)) / (Step * 2f);
                    Assert.That(gradient.x, Is.EqualTo(alongX).Within(0.01f), $"along X at {x}, {z}");
                    Assert.That(gradient.y, Is.EqualTo(alongZ).Within(0.01f), $"along Z at {x}, {z}");
                }
            }
        }

        [Test]
        public void Slope_NeverJumpsAcrossCells()
        {
            const float Step = 0.001f;

            // Crossing cell borders on both axes. On the visible facets, the slope jumps at each of these.
            for (int border = -5; border <= 5; border++)
            {
                for (float along = -5.25f; along < 5f; along += 0.5f)
                {
                    Vector2 before = _field.Slope(border - Step, along);
                    Vector2 after = _field.Slope(border + Step, along);
                    Assert.That((after - before).magnitude, Is.LessThan(0.01f), $"crossing X at {border}, {along}");

                    before = _field.Slope(along, border - Step);
                    after = _field.Slope(along, border + Step);
                    Assert.That((after - before).magnitude, Is.LessThan(0.01f), $"crossing Z at {along}, {border}");
                }
            }
        }

        [Test]
        public void Height_StaysCloseToTheFacetsOnTheFairwayAndGreen()
        {
            // Against a ball about 30 cm across. The measured worst case is 7.7 cm, near the fairway's edge.
            const float MaxGap = 0.08f;

            for (float x = -15f; x < 16f; x += 0.17f)
            {
                for (float z = -5f; z < 5f; z += 0.17f)
                {
                    var point = new Vector3(x, 0f, z);
                    if (_fields.Fairway(point) < 0f || _fields.Green(point) < 0f)
                    {
                        Assert.That(Mathf.Abs(_field.Height(x, z) - _field.FacetHeight(x, z)), Is.LessThan(MaxGap), $"at {x}, {z}");
                    }
                }
            }
        }

        [Test]
        public void Ground_IsFlatAtTheTeeAndHole()
        {
            foreach (Vector2 center in new[] { _shape.Tee, _shape.Hole })
            {
                Assert.That(_field.Slope(center.x, center.y).magnitude, Is.LessThan(0.01f));
                Assert.That(_field.Height(center.x, center.y), Is.EqualTo(0f).Within(0.01f));
            }
        }

        [Test]
        public void Tee_IsOnTheFairway_AndHoleIsOnTheGreen_TowardTheNorthEast()
        {
            Assert.That(_fields.Fairway(new Vector3(_shape.Tee.x, 0f, _shape.Tee.y)), Is.LessThan(0f));
            Assert.That(_fields.Green(new Vector3(_shape.Hole.x, 0f, _shape.Hole.y)), Is.LessThan(0f));
            Assert.That(_shape.Tee.x, Is.LessThan(_shape.Hole.x));
        }

        [Test]
        public void Green_LeansTheWayItsTiltSays()
        {
            // Well away from the hole's flat area, on the side of the green facing the fairway.
            Vector2 gradient = _field.Slope(11f, -1f);

            Assert.That(Vector2.Dot(gradient, _shape.GreenTilt.normalized), Is.GreaterThan(0.02f));
        }

        [Test]
        public void PlaySurfaces_AreGentle()
        {
            for (float x = -15f; x < 16f; x += 0.11f)
            {
                for (float z = -5f; z < 5f; z += 0.11f)
                {
                    var point = new Vector3(x, 0f, z);
                    float slope = _field.Slope(x, z).magnitude;
                    if (_fields.Green(point) < -0.2f)
                    {
                        Assert.That(slope, Is.LessThan(0.09f), $"green at {x}, {z}");
                    }
                    else if (_fields.Fairway(point) < -0.5f)
                    {
                        Assert.That(slope, Is.LessThan(0.18f), $"fairway at {x}, {z}");
                    }
                }
            }
        }

        [Test]
        public void Field_FarOutsideTheCourse_StaysUsable()
        {
            Assert.That(float.IsNaN(_field.Height(1000f, -1000f)), Is.False);
            Assert.That(float.IsNaN(_field.FacetHeight(-1000f, 1000f)), Is.False);
            Assert.That(float.IsNaN(_field.Slope(1000f, 1000f).x), Is.False);
        }

        [Test]
        public void Field_IsTheSameEveryTime()
        {
            var again = new CourseHeightField(_shape.ToLayout());

            Assert.That(again.Height(3.3f, 1.7f), Is.EqualTo(_field.Height(3.3f, 1.7f)));
            Assert.That(again.Slope(-7.1f, 2.2f), Is.EqualTo(_field.Slope(-7.1f, 2.2f)));
        }
    }
}
