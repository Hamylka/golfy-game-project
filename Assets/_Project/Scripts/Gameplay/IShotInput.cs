using System;
using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>
    /// Whatever lets the player aim and play a shot. The game only sees this, so the gesture can be swapped.
    /// Directions are unit vectors on the ground in the course's local space, as (x, z).
    /// </summary>
    public interface IShotInput
    {
        /// <summary>Whether letting go now would play a shot.</summary>
        bool IsAiming { get; }

        /// <summary>Where the shot would go. Only meaningful while <see cref="IsAiming"/>.</summary>
        Vector2 Direction { get; }

        /// <summary>The shot's power, from 0 to 1. Only meaningful while <see cref="IsAiming"/>.</summary>
        float Power { get; }

        /// <summary>The player played a shot, with this direction and power.</summary>
        event Action<Vector2, float> ShotReleased;
    }
}
