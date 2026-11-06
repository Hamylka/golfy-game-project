using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// The automatic heading for a ball at rest: toward the hole when a full-power shot can reach it, otherwise along
    /// the leg of the course's centreline the ball is on. Pure maths, for the camera to ease to and for tests.
    /// </summary>
    public sealed class CameraHeading
    {
        private readonly GroundPath _path;
        private readonly Vector2 _hole;
        private readonly float _reach;
        private readonly float _closest;
        private readonly float _maxTurn;

        /// <param name="centreline">The course's centreline, tee to hole (x is world X, y is world Z). A single leg counts as +X.</param>
        /// <param name="hole">The hole on the ground.</param>
        /// <param name="reach">How far a full-power shot goes on flat fairway, in metres. 0 never faces the hole.</param>
        /// <param name="closest">Nearer the hole than this, in metres, the ball's heading follows its leg: right by the hole the direction to it means little.</param>
        /// <param name="maxTurn">
        /// How far, in degrees, the heading toward the hole may turn from the ball's leg. Past it, the heading stops at
        /// this turn, so the course keeps running up the screen and a ball past the green never turns the view around.
        /// </param>
        public CameraHeading(IReadOnlyList<Vector2> centreline, Vector2 hole, float reach, float closest, float maxTurn = 180f)
        {
            // A straight course runs along the play axis, whatever small offset to the side its hole has, as the
            // framing reads it.
            bool bent = centreline != null && centreline.Count > 2;
            _path = bent ? new GroundPath(centreline) : null;
            _hole = hole;
            _reach = reach;
            _closest = closest;
            _maxTurn = maxTurn;
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
                Vector2 leg = Leg(ball);
                float turn = Vector2.SignedAngle(leg, toHole);
                return Mathf.Abs(turn) <= _maxTurn ? toHole / distance : Rotate(leg, Mathf.Clamp(turn, -_maxTurn, _maxTurn));
            }

            return Leg(ball);
        }

        /// <summary>The direction of the centreline's leg nearest to the ball. +X on a straight course.</summary>
        public Vector2 Leg(Vector2 ball)
        {
            if (_path == null)
            {
                return Vector2.right;
            }

            return _path.DirectionAt(_path.Project(ball, out _));
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
