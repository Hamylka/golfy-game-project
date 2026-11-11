using System.Text;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>What one sweep found. Counts are of shots played from the tee.</summary>
    public sealed class ShotSweepResult
    {
        public int Shots;
        public int Holed;
        public int Water;
        public int OffIsland;

        /// <summary>Widest run of consecutive angles that hole out at one power, in degrees, over all powers.</summary>
        public float WidestWindowDegrees;

        /// <summary>The power with the most holed shots, and how many (0 and 0 if nothing holed).</summary>
        public float BestPower;
        public int BestPowerHoled;

        /// <summary>Lowest and highest power that holes at least one shot (0 and 0 if nothing holed).</summary>
        public float LowestHoledPower;
        public float HighestHoledPower;

        /// <summary>Powers that hole at least one shot, as "0.95 (3), 1.00 (11)".</summary>
        public string HoledPowers = "";

        public float HoledShare => Shots == 0 ? 0f : (float)Holed / Shots;
    }

    /// <summary>One straight shot at the hole, traced for the "how does it feel" numbers.</summary>
    public sealed class StraightShotTrace
    {
        public float FirstLandingX;
        public int Bounces;
        public float RestX;
        public float PastHole;
        public float RollAfterLanding;
        public BallState Final;
    }

    /// <summary>
    /// The hole-in-one check: plays shots from the tee at the hole over a range of aims and powers, on a plain
    /// course with no scene, and counts what drops. Settings are passed in, so a candidate can be tried on a copy
    /// before the asset changes. It plays the same <see cref="BallSimulation"/> a real shot does.
    /// </summary>
    public static class ShotSweep
    {
        private const int StepLimit = 60 * 120;

        public static ShotSweepResult Run(ShotSettings settings, IBallGround ground, Vector2 tee, Vector2 hole,
            float maxAngle = 6f, float angleStep = 0.1f, float minPower = 0.5f, float maxPower = 1f, float powerStep = 0.05f, BallObstacles obstacles = null)
        {
            var result = new ShotSweepResult();
            var powers = new StringBuilder();
            Vector2 straight = hole - tee;
            int angleCount = Mathf.RoundToInt(2f * maxAngle / angleStep) + 1;
            int powerCount = Mathf.RoundToInt((maxPower - minPower) / powerStep) + 1;

            for (int p = 0; p < powerCount; p++)
            {
                float power = Mathf.Min(maxPower, minPower + p * powerStep);
                int holedAtPower = 0;
                int run = 0;
                int longestRun = 0;

                for (int a = 0; a < angleCount; a++)
                {
                    float angle = -maxAngle + a * angleStep;
                    Vector2 direction = Quaternion.Euler(0f, 0f, -angle) * (Vector3)straight;
                    BallState end = PlayToEnd(settings, ground, tee, direction, power, out _, out _, out _, obstacles);

                    result.Shots++;
                    if (end == BallState.Holed)
                    {
                        result.Holed++;
                        holedAtPower++;
                        run++;
                        longestRun = Mathf.Max(longestRun, run);
                    }
                    else
                    {
                        run = 0;
                        result.Water += end == BallState.InWater ? 1 : 0;
                        result.OffIsland += end == BallState.OffIsland ? 1 : 0;
                    }
                }

                if (holedAtPower > 0)
                {
                    result.LowestHoledPower = result.LowestHoledPower == 0f ? power : Mathf.Min(result.LowestHoledPower, power);
                    result.HighestHoledPower = Mathf.Max(result.HighestHoledPower, power);
                    powers.Append(powers.Length > 0 ? ", " : "").Append(power.ToString("0.00")).Append(" (").Append(holedAtPower).Append(')');
                    if (holedAtPower > result.BestPowerHoled)
                    {
                        result.BestPowerHoled = holedAtPower;
                        result.BestPower = power;
                    }
                }

                result.WidestWindowDegrees = Mathf.Max(result.WidestWindowDegrees, longestRun * angleStep);
            }

            result.HoledPowers = powers.ToString();
            return result;
        }

        /// <summary>One shot straight at the hole, to read where it lands, how far it rolls and where it stops.</summary>
        public static StraightShotTrace Trace(ShotSettings settings, IBallGround ground, Vector2 tee, Vector2 hole, float power)
        {
            BallState end = PlayToEnd(settings, ground, tee, hole - tee, power, out Vector3 landing, out Vector3 rest, out int bounces);
            return new StraightShotTrace
            {
                FirstLandingX = landing.x,
                Bounces = bounces,
                RestX = rest.x,
                PastHole = rest.x - hole.x,
                RollAfterLanding = rest.x - landing.x,
                Final = end,
            };
        }

        private static BallState PlayToEnd(ShotSettings settings, IBallGround ground, Vector2 tee, Vector2 direction, float power,
            out Vector3 firstLanding, out Vector3 rest, out int bounces, BallObstacles obstacles = null)
        {
            var ball = new BallSimulation(ground, settings, tee, obstacles);
            firstLanding = ball.Position;
            ball.Shoot(direction, power);
            bool landed = false;

            for (int i = 0; i < StepLimit && ball.State != BallState.Resting && ball.State != BallState.Holed; i++)
            {
                ball.Step();

                // The first time the ball is on the ground and not flying again is the first landing.
                if (!landed && (ball.Bounces > 0 || ball.State == BallState.Rolling))
                {
                    landed = true;
                    firstLanding = ball.Position;
                }

                if (ball.State == BallState.InWater || ball.State == BallState.OffIsland)
                {
                    break;
                }
            }

            rest = ball.Position;
            bounces = ball.Bounces;
            return ball.State;
        }
    }
}
