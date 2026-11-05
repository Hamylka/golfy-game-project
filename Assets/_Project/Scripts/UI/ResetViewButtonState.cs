using UnityEngine;

namespace IsoGolf.UI
{
    /// <summary>
    /// When the reset view button shows, and when a touch counts as a press on it. Plain C#, so the rules
    /// can be tested without a screen.
    /// </summary>
    public sealed class ResetViewButtonState
    {
        private bool _wasPressed;

        public bool IsPressed { get; private set; }

        /// <summary>
        /// Shown only while the ball rests and nothing is being aimed. During a shot nothing else is on screen
        /// (CLAUDE.md), so it hides for the flight as well.
        /// </summary>
        public static bool IsVisible(bool ballAtRest, bool aiming)
        {
            return ballAtRest && !aiming;
        }

        /// <summary>True if a touch landing here belongs to the button, so the camera and the shot must leave it alone.</summary>
        public static bool ClaimsTouch(bool visible, Rect hit, Vector2 pixel)
        {
            return visible && hit.Contains(pixel);
        }

        /// <summary>
        /// Call once a frame. Returns true on the frame the button is pressed: a touch that began on it and lifted
        /// on it. A drag from elsewhere that ends over it does nothing, and neither does one that slides off.
        /// </summary>
        public bool Update(bool visible, Rect hit, bool pressed, Vector2 pixel, int fingers)
        {
            bool landed = pressed && !_wasPressed;
            _wasPressed = pressed;

            if (!visible || fingers > 1)
            {
                IsPressed = false;
                return false;
            }

            if (landed)
            {
                IsPressed = hit.Contains(pixel);
                return false;
            }

            if (!IsPressed)
            {
                return false;
            }

            if (pressed)
            {
                // Sliding off the button is how a player changes their mind.
                IsPressed = hit.Contains(pixel);
                return false;
            }

            IsPressed = false;
            return true;
        }
    }
}
