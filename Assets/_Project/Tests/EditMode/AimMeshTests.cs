using IsoGolf.Generation;
using IsoGolf.UI;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class AimMeshTests
    {
        private static readonly Color32 Mark = new Color32(1, 2, 3, 255);
        private static readonly Color32 Fill = new Color32(4, 5, 6, 255);

        private AimMesh _mesh;

        [SetUp]
        public void SetUp()
        {
            _mesh = new AimMesh();
        }

        [Test]
        public void Dot_IsARoundFanOfTheRightRadius()
        {
            _mesh.AddDot(new Vector3(1f, 2f, 3f), 0.5f, 8, Vector3.right, Vector3.up, Mark);

            Assert.That(_mesh.Positions.Count, Is.EqualTo(9));
            Assert.That(_mesh.Triangles.Count, Is.EqualTo(24));
            for (int i = 1; i < _mesh.Positions.Count; i++)
            {
                Assert.That(Vector3.Distance(_mesh.Positions[i], new Vector3(1f, 2f, 3f)), Is.EqualTo(0.5f).Within(0.0001f));
            }
        }

        [Test]
        public void Dot_FacesTheCamera()
        {
            // With these axes the camera looks along +Z, so a front face's normal points back along -Z.
            _mesh.AddDot(Vector3.zero, 1f, 6, Vector3.right, Vector3.up, Mark);

            for (int i = 0; i < _mesh.Triangles.Count; i += 3)
            {
                Vector3 a = _mesh.Positions[_mesh.Triangles[i]];
                Vector3 b = _mesh.Positions[_mesh.Triangles[i + 1]];
                Vector3 c = _mesh.Positions[_mesh.Triangles[i + 2]];
                Assert.That(Vector3.Cross(b - a, c - a).z, Is.LessThan(0f), $"triangle {i / 3}");
            }
        }

        [TestCase(0f, 0f)]
        [TestCase(0.5f, 0.4f)]
        [TestCase(1f, 0.8f)]
        public void Gauge_FillGrowsFromTheLeftInsideItsBorder(float power, float expectedWidth)
        {
            _mesh.AddGauge(Vector3.zero, 1f, 0.2f, 0.1f, power, Vector3.right, Vector3.up, Mark, Fill);

            // Four vertices of border, then four of fill.
            Assert.That(_mesh.Positions.Count, Is.EqualTo(8));
            Assert.That(_mesh.Colors[4], Is.EqualTo(Fill));
            Assert.That(_mesh.Positions[4].x, Is.EqualTo(-0.4f).Within(0.0001f));
            Assert.That(_mesh.Positions[7].x - _mesh.Positions[4].x, Is.EqualTo(expectedWidth).Within(0.0001f));
        }

        [Test]
        public void Gauge_DrawsItsFillAfterItsBorder()
        {
            _mesh.AddGauge(Vector3.zero, 1f, 0.2f, 0.05f, 1f, Vector3.right, Vector3.up, Mark, Fill);

            // See-through geometry with no depth writes draws in triangle order.
            Assert.That(_mesh.Colors[_mesh.Triangles[0]], Is.EqualTo(Mark));
            Assert.That(_mesh.Colors[_mesh.Triangles[_mesh.Triangles.Count - 1]], Is.EqualTo(Fill));
        }

        [Test]
        public void Gauge_PowerOutOfRangeStaysInside()
        {
            _mesh.AddGauge(Vector3.zero, 1f, 0.2f, 0.1f, 3f, Vector3.right, Vector3.up, Mark, Fill);

            Assert.That(_mesh.Positions[7].x, Is.EqualTo(0.4f).Within(0.0001f));
        }

        [Test]
        public void Disc_KeepsItsTrianglesPointingAtItsOwnVertices()
        {
            var ring = new GroundDisc(1f, 2, 8, (x, z) => 0f, (x, z) => true, 0.5f);
            ring.Place(0f, 0f, 0f, Mark);
            _mesh.AddDot(Vector3.zero, 1f, 6, Vector3.right, Vector3.up, Mark);
            int first = _mesh.Positions.Count;

            _mesh.AddDisc(ring);

            Assert.That(_mesh.Positions.Count, Is.EqualTo(first + ring.Positions.Length));
            for (int i = 0; i < ring.Triangles.Length; i++)
            {
                Assert.That(_mesh.Triangles[_mesh.Triangles.Count - ring.Triangles.Length + i], Is.EqualTo(first + ring.Triangles[i]));
            }
        }

        [Test]
        public void Clear_EmptiesEverything()
        {
            _mesh.AddDot(Vector3.zero, 1f, 6, Vector3.right, Vector3.up, Mark);

            _mesh.Clear();

            Assert.That(_mesh.Positions, Is.Empty);
            Assert.That(_mesh.Colors, Is.Empty);
            Assert.That(_mesh.Triangles, Is.Empty);
        }

        [Test]
        public void ScreenWidth_MatchesTheFrustumAtThatDepth()
        {
            // At 90° the frustum's height is twice the depth, and a 0.5 aspect makes the width half of that.
            Assert.That(AimMesh.MetresPerScreenWidth(90f, 0.5f, 10f), Is.EqualTo(10f).Within(0.0001f));
        }
    }
}
