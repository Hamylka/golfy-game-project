using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// Turns the sun once, when a level starts, and never again: a moving sun would make the crisp shadows shimmer.
    /// The sun's rotation in the scene is the one for a course heading along +X; its pitch never changes.
    /// </summary>
    public sealed class LevelSun : MonoBehaviour
    {
        [SerializeField] private Light sun;

        private Vector3 _authored;
        private bool _captured;

        /// <param name="heading">The ground heading from tee to hole (x is world X, y is world Z).</param>
        public void Apply(Vector2 heading)
        {
            if (!_captured)
            {
                _authored = sun.transform.eulerAngles;
                _captured = true;
            }

            sun.transform.rotation = Quaternion.Euler(_authored.x, SunHeading.YawFor(_authored.y, heading), _authored.z);
        }
    }
}
