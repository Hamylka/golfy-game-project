using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// The automatic heading for a ball at rest: toward the hole when a full-power shot can reach it, otherwise along
    /// the course ahead of the ball, read over a look-ahead stretch of the centreline. Pure maths, for the camera to
    /// ease to and for tests.
    /// </summary>
    public sealed class CameraHeading
    {
        private readonly GroundPath _path;
        private readonly Vector2 _hole;
        private readonly float _reach;
        private readonly float _closest;
        private readonly float _maxTurn;
        private readonly float _lookAhead;

        /// <param name="centreline">The course's centreline, tee to hole (x is world X, y is world Z). A single leg counts as +X.</param>
        /// <param name="hole">The hole on the ground.</param>
        /// <param name="reach">How far a full-power shot goes on flat fairway, in metres. 0 never faces the hole.</param>
        /// <param name="closest">Nearer the hole than this, in metres, the ball's heading follows the course ahead: right by the hole the direction to it means little.</param>
        /// <param name="maxTurn">
        /// How far, in degrees, the heading toward the hole may turn from the course ahead. Past it, the heading stops at
        /// this turn, so the course keeps running up the screen and a ball past the green never turns the view around.
        /// </param>
        /// <param name="lookAhead">
        /// How far along the centreline, in metres, the course ahead is read. 0 takes the direction of the leg the ball
        /// is on, as before the look-ahead existed.
        /// </param>
        public CameraHeading(IReadOnlyList<Vector2> centreline, Vector2 hole, float reach, float closest, float maxTurn = 180f, float lookAhead = 0f)
        {
            // A straight course runs along the play axis, whatever small offset to the side its hole has, as the
            // framing reads it.
            bool bent = centreline != null && centreline.Count > 2;
            _path = bent ? new GroundPath(centreline) : null;
            _hole = hole;
            _reach = reach;
            _closest = closest;
            _maxTurn = maxTurn;
            _lookAhead = Mathf.Max(0f, lookAhead);
        }

        /// <summary>The heading of the course's first leg, for the start of a level.</summary>
        public Vector2 FirstLeg => _path != null ? _path.LegDirection(0) : Vector2.right;

        /// <summary>The ground heading for a ball resting here (x is world X, y is world Z), of unit length.</summary>
        public Vector2 For(Vector2 ball)
        {
            Vector2 toHole = _hole - ball;
            float distance = toHole.magnitude;
            if (distance <= _reach && distance >= _closest && distance > 1e-4f)
            {
                Vector2 ahead = Ahead(ball);
                float turn = Vector2.SignedAngle(ahead, toHole);
                return Mathf.Abs(turn) <= _maxTurn ? toHole / distance : Rotate(ahead, Mathf.Clamp(turn, -_maxTurn, _maxTurn));
            }

            return Ahead(ball);
        }

        /// <summary>
        /// The direction of the course ahead of the ball: the chord of the centreline from the ball's projection on it
        /// to the look-ahead distance further on, stopped at the centreline's end (the hole). +X on a straight course.
        /// </summary>
        public Vector2 Ahead(Vector2 ball)
        {
            if (_path == null)
            {
                return Vector2.right;
            }

            // A chord rather than the leg the ball is on: a ball resting just short of a bend has done with its leg,
            // and the view should show where the course goes next (par 5 seed 6, where a full shot stops 0.37 m short
            // of a 32 degree bend). Mid-leg, with the next bend further than the look-ahead, the chord lies on the leg,
            // so the heading is the leg's, as before. Near a bend it turns gradually, with no threshold to jump.
            // The chord starts on the centreline, not at the ball, so a ball off to one side gets the same heading.
            float along = _path.Project(ball, out _);
            float end = Mathf.Min(along + _lookAhead, _path.Length);

            // No look-ahead, or a ball level with or past the hole end (where the chord would point back): the leg it is on.
            if (end - along < 0.01f)
            {
                return _path.DirectionAt(along);
            }

            return (_path.PointAt(end) - _path.PointAt(along)).normalized;
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            return new Vector2((direction.x * cos) - (direction.y * sin), (direction.x * sin) + (direction.y * cos));
        }

        /// <summary>The camera's yaw for a ball resting here.</summary>
        public float YawFor(Vector2 ball)
        {
            return IsoCameraConvention.YawFor(For(ball));
        }
    }
}
