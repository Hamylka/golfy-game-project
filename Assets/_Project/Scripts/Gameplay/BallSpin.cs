using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>Turns the ball's movement into spin. The ball's facets would otherwise show it sliding.</summary>
    public static class BallSpin
    {
        /// <summary>
        /// The rotation after rolling along this movement without slipping: one radian per radius travelled,
        /// around the axis across the direction of travel. Height changes don't count.
        /// </summary>
        public static Quaternion Roll(Quaternion rotation, Vector3 movement, float radius)
        {
            var along = new Vector3(movement.x, 0f, movement.z);
            float distance = along.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                return rotation;
            }

            Vector3 axis = Vector3.Cross(Vector3.up, along / distance);
            return Quaternion.AngleAxis(distance / radius * Mathf.Rad2Deg, axis) * rotation;
        }
    }
}
