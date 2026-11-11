using System;
using System.Collections.Generic;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// F3.1 (split fairway plan §2): the fairway field as the union of pieces in the centreline's frame minus rough ovals. Every
    /// case runs on a straight centreline along +X from the origin, where the distance along is x and the distance across is z, with
    /// no edge wobble and values exact in binary, so expected fields are exact numbers.
    /// </summary>
    public class FairwayPieceFieldTests
    {
        private static CourseLayout Course(IEnumerable<FairwayPiece> pieces, IEnumerable<EllipseShape> ovals = null, float wobble = 0f, CoursePath path = null)
        {
            return new CourseLayout(
                new[] { new IslandOutline(40f, 20f, 0f, 0.1f, 10f) },
                path ?? new CoursePath(new[] { Vector2.zero, new Vector2(20f, 0f) }),
                new FairwayLayout(0f, 20f, 1f, wobble, 1f, 0f, 0f, 0f, pieces, ovals),
                new GreenLayout[0],
                new EllipseShape[0],
                new PondLayout[0],
                new GroundSwell[0],
                -1f,
                new GroundRelief(1f, 0f, 0f, 1f, 0.1f),
                Vector2.zero,
                new Vector2(20f, 0f),
                1f,
                1f,
                new EdgeLayout(new Vector2(0.5f, 1f), 0.1f),
                alongCentreline: true);
        }

        private static float Field(CourseLayout layout, float x, float z) => new CourseFields(layout).Fairway(new Vector3(x, 0f, z));

        [Test]
        public void ConstantOffset_MovesTheStripAcross()
        {
            CourseLayout layout = Course(new[] { new FairwayPiece(0f, 20f, 1f, offset: 2f) });
            Assert.That(Field(layout, 10f, 2f), Is.EqualTo(-1f));
            Assert.That(Field(layout, 10f, 1.5f), Is.EqualTo(-0.5f));
            Assert.That(Field(layout, 10f, 0f), Is.EqualTo(1f), "the centreline is outside a strip offset by twice its half width");
            Assert.That(Field(layout, 10f, -2f), Is.EqualTo(3f));
        }

        [Test]
        public void EasedOffset_OpensAndClosesWithASmoothstep()
        {
            var piece = new FairwayPiece(0f, 20f, 1f, offset: 2f, offsetEase: 4f);
            Assert.That(piece.OffsetAt(0f), Is.EqualTo(0f));
            Assert.That(piece.OffsetAt(2f), Is.EqualTo(1f), "halfway through the ease, smoothstep(0.5) = 0.5");
            Assert.That(piece.OffsetAt(4f), Is.EqualTo(2f));
            Assert.That(piece.OffsetAt(10f), Is.EqualTo(2f));
            Assert.That(piece.OffsetAt(18f), Is.EqualTo(1f));
            Assert.That(piece.OffsetAt(20f), Is.EqualTo(0f));

            CourseLayout layout = Course(new[] { piece });
            Assert.That(Field(layout, 2f, 1f), Is.EqualTo(-1f), "the strip's middle has moved half way across");
            Assert.That(Field(layout, 10f, 2f), Is.EqualTo(-1f));
        }

        [Test]
        public void EasedWidth_SwellsFromItsEdgeValue()
        {
            var piece = new FairwayPiece(0f, 20f, 2f, edgeHalfWidth: 1f, widthEase: 4f);
            Assert.That(piece.HalfWidthAt(0f), Is.EqualTo(1f));
            Assert.That(piece.HalfWidthAt(2f), Is.EqualTo(1.5f));
            Assert.That(piece.HalfWidthAt(4f), Is.EqualTo(2f));
            Assert.That(piece.HalfWidthAt(10f), Is.EqualTo(2f));

            CourseLayout layout = Course(new[] { piece });
            Assert.That(Field(layout, 10f, 2f), Is.EqualTo(0f));
            Assert.That(Field(layout, 2f, 1.5f), Is.EqualTo(0f));
            Assert.That(Field(layout, 2f, 1f), Is.EqualTo(-0.5f));
        }

        [Test]
        public void RoundedCap_NarrowsTheTip_WhereAFlatJoinHoldsItsWidth()
        {
            CourseLayout capped = Course(new[] { new FairwayPiece(0f, 20f, 1f, fromCap: 2f) });
            CourseLayout flat = Course(new[] { new FairwayPiece(0f, 20f, 1f) });

            Assert.That(Field(capped, 0f, 0f), Is.EqualTo(0f), "the tip's point sits on the start");
            Assert.That(Field(capped, 1f, 0f), Is.EqualTo(-0.5f));
            Assert.That(Field(capped, 1f, 0.9f), Is.GreaterThan(0f), "inside the cap the strip is narrower than its half width");
            Assert.That(Field(flat, 1f, 0.9f), Is.LessThan(0f));
            Assert.That(Field(flat, 0f, 0.5f), Is.EqualTo(0f), "a flat join cuts square at the start");
            Assert.That(Field(capped, 5f, 0.5f), Is.EqualTo(Field(flat, 5f, 0.5f)), "past the cap both are the plain strip");
        }

        [Test]
        public void Union_TakesTheNearestPiece()
        {
            CourseLayout layout = Course(new[] { new FairwayPiece(0f, 10f, 1f), new FairwayPiece(8f, 20f, 1f, offset: 3f) });
            Assert.That(Field(layout, 9f, 0f), Is.EqualTo(-1f), "inside the first piece");
            Assert.That(Field(layout, 9f, 3f), Is.EqualTo(-1f), "inside the second piece");
            Assert.That(Field(layout, 9f, 1.5f), Is.EqualTo(0.5f), "rough between them");
            Assert.That(Field(layout, 15f, 0f), Is.EqualTo(2f), "past the first piece's end, the second is nearer");
        }

        [Test]
        public void RoughOval_IsCutOutOfThePieces()
        {
            var oval = new EllipseShape(new Vector2(10f, 0f), new Vector2(2f, 1f), 0f);
            CourseLayout layout = Course(new[] { new FairwayPiece(0f, 20f, 3f) }, new[] { oval });
            var fields = new CourseFields(layout);

            Assert.That(Field(layout, 10f, 0f), Is.EqualTo(1f), "the oval's middle is rough");
            Assert.That(Field(layout, 10f, 2f), Is.EqualTo(-1f), "fairway beside the oval");
            Assert.That(Field(layout, 4f, 0f), Is.EqualTo(-2f), "away from the oval the turned oval field (-2) is the higher one, still fairway");
            Assert.That(Field(layout, 1f, 2.5f), Is.EqualTo(-0.5f), "far from the oval, the piece alone");
            Assert.That(fields.Surface(new Vector3(10f, 0f, 0f)), Is.EqualTo(CourseSurface.Rough));
            Assert.That(fields.Surface(new Vector3(10f, 0f, 2f)), Is.EqualTo(CourseSurface.Fairway));
        }

        [Test]
        public void APointExactlyOnABorder_IsZero_AndRough()
        {
            CourseLayout strip = Course(new[] { new FairwayPiece(0f, 20f, 1f, offset: 2f) });
            Assert.That(Field(strip, 10f, 3f), Is.EqualTo(0f));
            Assert.That(new CourseFields(strip).Surface(new Vector3(10f, 0f, 3f)), Is.EqualTo(CourseSurface.Rough));

            var oval = new EllipseShape(new Vector2(10f, 0f), new Vector2(2f, 1f), 0f);
            CourseLayout cut = Course(new[] { new FairwayPiece(0f, 20f, 3f) }, new[] { oval });
            Assert.That(Field(cut, 10f, 1f), Is.EqualTo(0f));
            Assert.That(new CourseFields(cut).Surface(new Vector3(10f, 0f, 1f)), Is.EqualTo(CourseSurface.Rough));
        }

        [Test]
        public void NoPiecesGiven_IsOnePlainPiece_SameBitsAsAnExplicitOne()
        {
            // A bent centreline, the edge wobble and both caps: everything the plain generated fairway uses.
            var path = new CoursePath(new[] { new Vector2(-10f, -1f), new Vector2(4f, 0.5f), new Vector2(18f, 4f) });
            var implicitLayout = new CourseLayout(
                new[] { new IslandOutline(40f, 20f, 0f, 0.1f) }, path,
                new FairwayLayout(-2f, 26f, 2.5f, 1.6f, 1f, 417f, 2.5f, 2.5f),
                new GreenLayout[0], new EllipseShape[0], new PondLayout[0], new GroundSwell[0], -1f,
                new GroundRelief(1f, 0f, 0f, 1f, 0.1f), path.Start, path.End, 1f, 1f, new EdgeLayout(new Vector2(0.5f, 1f), 0.1f), alongCentreline: true);
            var explicitLayout = new CourseLayout(
                new[] { new IslandOutline(40f, 20f, 0f, 0.1f) }, path,
                new FairwayLayout(-2f, 26f, 2.5f, 1.6f, 1f, 417f, 2.5f, 2.5f, new[] { FairwayPiece.Plain(-2f, 26f, 2.5f, 2.5f, 2.5f) }),
                new GreenLayout[0], new EllipseShape[0], new PondLayout[0], new GroundSwell[0], -1f,
                new GroundRelief(1f, 0f, 0f, 1f, 0.1f), path.Start, path.End, 1f, 1f, new EdgeLayout(new Vector2(0.5f, 1f), 0.1f), alongCentreline: true);

            Assert.That(implicitLayout.Fairway.Pieces.Count, Is.EqualTo(1));
            Assert.That(implicitLayout.Fairway.IsPlain, Is.True);
            Assert.That(implicitLayout.Fairway.RoughOvals.Count, Is.Zero);

            var a = new CourseFields(implicitLayout);
            var b = new CourseFields(explicitLayout);
            for (float x = -14f; x <= 22f; x += 0.37f)
            {
                for (float z = -6f; z <= 9f; z += 0.37f)
                {
                    var p = new Vector3(x, 0f, z);
                    Assert.That(BitConverter.SingleToInt32Bits(b.Fairway(p)), Is.EqualTo(BitConverter.SingleToInt32Bits(a.Fairway(p))), $"at {p}");
                }
            }
        }

        [Test]
        public void ShapedFairway_IsNotPlain()
        {
            Assert.That(new FairwayLayout(0f, 20f, 1f, 0f, 1f, pieces: new[] { new FairwayPiece(0f, 20f, 1f, offset: 1f) }).IsPlain, Is.False);
            Assert.That(new FairwayLayout(0f, 20f, 1f, 0f, 1f, pieces: new[] { new FairwayPiece(0f, 10f, 1f), new FairwayPiece(9f, 20f, 1f) }).IsPlain, Is.False);
            Assert.That(new FairwayLayout(0f, 20f, 1f, 0f, 1f, roughOvals: new[] { new EllipseShape(Vector2.zero, Vector2.one, 0f) }).IsPlain, Is.False);
        }
    }
}
