using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class EllipseHeadingTests
    {
        [Test]
        public void HeadingZero_IsTheOldFormula_OnAGrid()
        {
            var shape = new EllipseShape(new Vector2(2f, -1f), new Vector2(3f, 1.4f), 0.2f);
            var fields = new CourseFields(Layout(shape));

            for (float x = -6f; x <= 10f; x += 0.37f)
            {
                for (float z = -6f; z <= 6f; z += 0.41f)
                {
                    float dx = (x - shape.Center.x) / shape.Radius.x;
                    float dz = (z - shape.Center.y) / shape.Radius.y;
                    float noise = CourseNoise.Value01((x * 0.8f) + shape.Center.x + 30f, (z * 0.8f) + shape.Center.y + 30f) - 0.5f;
                    float expected = Mathf.Sqrt((dx * dx) + (dz * dz)) - 1f + (noise * shape.Wobble * 2f);

                    Assert.That(fields.Bunker(new Vector3(x, 0f, z)), Is.EqualTo(expected).Within(1e-5f), $"at {x}, {z}");
                }
            }
        }

        [Test]
        public void Heading90_SwapsTheRadii()
        {
            var turned = new CourseFields(Layout(new EllipseShape(new Vector2(1f, 0.5f), new Vector2(3f, 1f), 0.15f, 90f)));
            var swapped = new CourseFields(Layout(new EllipseShape(new Vector2(1f, 0.5f), new Vector2(1f, 3f), 0.15f)));

            for (float x = -4f; x <= 6f; x += 0.43f)
            {
                for (float z = -4f; z <= 5f; z += 0.39f)
                {
                    var p = new Vector3(x, 0f, z);
                    Assert.That(turned.Bunker(p), Is.EqualTo(swapped.Bunker(p)).Within(1e-5f), $"at {x}, {z}");
                }
            }
        }

        [Test]
        public void TurnedEllipse_IsInsideAtTheCentreAndOutsidePastItsLongRadiusAlongTheTurnedAxis()
        {
            const float Heading = 35f;
            var centre = new Vector2(1f, 1f);
            var fields = new CourseFields(Layout(new EllipseShape(centre, new Vector2(4f, 1f), 0f, Heading)));
            var along = new Vector2(Mathf.Cos(Heading * Mathf.Deg2Rad), Mathf.Sin(Heading * Mathf.Deg2Rad));
            var across = new Vector2(-along.y, along.x);

            Vector2 inAlong = centre + (along * 3.5f);
            Vector2 pastAlong = centre + (along * 4.5f);
            Vector2 inAcross = centre + (across * 0.8f);
            Vector2 pastAcross = centre + (across * 1.5f);

            Assert.That(fields.Bunker(new Vector3(centre.x, 0f, centre.y)), Is.LessThan(0f));
            Assert.That(fields.Bunker(new Vector3(inAlong.x, 0f, inAlong.y)), Is.LessThan(0f), "inside along the turned axis");
            Assert.That(fields.Bunker(new Vector3(pastAlong.x, 0f, pastAlong.y)), Is.GreaterThan(0f), "past the long radius");
            Assert.That(fields.Bunker(new Vector3(inAcross.x, 0f, inAcross.y)), Is.LessThan(0f));
            Assert.That(fields.Bunker(new Vector3(pastAcross.x, 0f, pastAcross.y)), Is.GreaterThan(0f), "past the short radius");

            // The same distance on the unturned X axis is outside: the axes really turned.
            Assert.That(fields.Bunker(new Vector3(centre.x + 3.5f, 0f, centre.y)), Is.GreaterThan(0f));
        }

        private static CourseLayout Layout(EllipseShape bunker)
        {
            var tee = new Vector2(-30f, 0f);
            var hole = new Vector2(30f, 0f);
            return new CourseLayout(
                new[] { new IslandOutline(40f, 20f, 0f, 0.2f) },
                new CoursePath(new[] { tee, hole }),
                new FairwayLayout(-30f, 30f, 2f, 0f, 1f),
                new GreenLayout[0],
                new[] { bunker },
                new PondLayout[0],
                new GroundSwell[0],
                -0.2f,
                new GroundRelief(1f, 0f, 0f, 1f, 1f),
                tee,
                hole,
                1f,
                1f,
                new EdgeLayout(Vector2.one, 1f));
        }
    }
}
