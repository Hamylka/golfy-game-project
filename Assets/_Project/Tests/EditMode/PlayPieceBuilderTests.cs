using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class PlayPieceBuilderTests
    {
        private PlayPieceSettings _settings;
        private PaletteSettings _palette;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<PlayPieceSettings>();
            _palette = ScriptableObject.CreateInstance<PaletteSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
            Object.DestroyImmediate(_palette);
        }

        [Test]
        public void Ball_Has80FacetsOnItsRadius()
        {
            TerrainMeshData ball = PlayPieceBuilder.BuildBall(_settings, _palette);

            Assert.That(ball.TriangleCount, Is.EqualTo(80));
            foreach (Vector3 position in ball.Positions)
            {
                Assert.That(position.magnitude, Is.EqualTo(_settings.BallRadius).Within(0.0001f));
            }
        }

        [Test]
        public void Ball_FacetsFaceOutward()
        {
            TerrainMeshData ball = PlayPieceBuilder.BuildBall(_settings, _palette);

            for (int i = 0; i < ball.Positions.Count; i += 3)
            {
                Vector3 a = ball.Positions[i];
                Vector3 b = ball.Positions[i + 1];
                Vector3 c = ball.Positions[i + 2];
                Assert.That(Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c), Is.GreaterThan(0f));
            }
        }

        [Test]
        public void Flag_StandsOnTheOriginAtThePoleHeight()
        {
            TerrainMeshData flag = PlayPieceBuilder.BuildFlag(_settings, _palette);

            float lowest = float.MaxValue;
            float highest = float.MinValue;
            foreach (Vector3 position in flag.Positions)
            {
                lowest = Mathf.Min(lowest, position.y);
                highest = Mathf.Max(highest, position.y);
            }

            Assert.That(lowest, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(highest, Is.EqualTo(_settings.PoleHeight).Within(0.0001f));
        }

        [Test]
        public void Flag_PennantBlowsTowardTheRightOfTheScreen()
        {
            // The camera's right is mostly -Z, so a pennant reaching along -Z reads side on.
            TerrainMeshData flag = PlayPieceBuilder.BuildFlag(_settings, _palette);

            float furthest = 0f;
            foreach (Vector3 position in flag.Positions)
            {
                furthest = Mathf.Min(furthest, position.z);
            }

            Assert.That(furthest, Is.EqualTo(-_settings.FlagLength).Within(0.0001f));
        }

        [Test]
        public void ToMesh_KeepsEveryTriangle()
        {
            TerrainMeshData ball = PlayPieceBuilder.BuildBall(_settings, _palette);
            Mesh mesh = ball.ToMesh("Test");

            Assert.That(mesh.vertexCount, Is.EqualTo(ball.Positions.Count));
            Assert.That(mesh.triangles.Length, Is.EqualTo(ball.Positions.Count));
            Assert.That(mesh.normals.Length, Is.EqualTo(ball.Positions.Count));

            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void HoleAndShadowColorsAreNeverBlack()
        {
            // ART_DIRECTION.md: no pure black anywhere, and shadows are cooler versions of the base color.
            Assert.That(_palette.Hole.maxColorComponent, Is.GreaterThan(0f));
            Assert.That(_palette.BallShadow.maxColorComponent, Is.GreaterThan(0f));
            Assert.That(_palette.BallShadow.b, Is.GreaterThan(_palette.BallShadow.r), "The shadow should lean cool.");
            Assert.That(_palette.BallShadow.a, Is.LessThan(1f), "The shadow should be slightly transparent.");
        }
    }
}
