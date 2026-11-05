using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class PanMomentumTests
    {
        private const float TimeConstant = 0.5f;
        private const float Window = 0.1f;
        private const float MinRelease = 0.2f;
        private const float StopSpeed = 0.03f;
        private const float Frame = 1f / 60f;

        private static PanMomentum Create()
        {
            return new PanMomentum(TimeConstant, Window, MinRelease, StopSpeed);
        }

        private static void Drag(PanMomentum momentum, Vector3 velocity, float seconds, float frame = Frame)
        {
            for (float time = 0f; time < seconds; time += frame)
            {
                momentum.AddSample(velocity * frame, frame);
            }
        }

        private static Vector3 Glide(PanMomentum momentum, float frame)
        {
            Vector3 total = Vector3.zero;
            for (int i = 0; i < 100000 && momentum.IsGliding; i++)
            {
                total += momentum.Step(frame);
            }

            return total;
        }

        [Test]
        public void Release_TakesTheSpeedOfTheDrag()
        {
            PanMomentum momentum = Create();
            Drag(momentum, new Vector3(4f, 0f, -2f), 0.3f);
            momentum.Release();

            Assert.That((momentum.Velocity - new Vector3(4f, 0f, -2f)).magnitude, Is.LessThan(0.001f));
        }

        [Test]
        public void Release_OnlyCountsTheEndOfTheDrag()
        {
            PanMomentum momentum = Create();
            Drag(momentum, new Vector3(20f, 0f, 0f), 0.2f);
            Drag(momentum, new Vector3(2f, 0f, 0f), 0.2f);
            momentum.Release();

            Assert.That(momentum.Velocity.x, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void Release_TooSlow_DoesNotGlide()
        {
            PanMomentum momentum = Create();
            Drag(momentum, new Vector3(MinRelease * 0.5f, 0f, 0f), 0.3f);
            momentum.Release();

            Assert.That(momentum.IsGliding, Is.False);
        }

        [Test]
        public void Release_WithNoDrag_DoesNotGlide()
        {
            PanMomentum momentum = Create();
            momentum.Release();

            Assert.That(momentum.IsGliding, Is.False);
        }

        [Test]
        public void Release_ForgetsTheDrag()
        {
            PanMomentum momentum = Create();
            Drag(momentum, new Vector3(4f, 0f, 0f), 0.3f);
            momentum.Release();
            momentum.Stop();
            momentum.Release();

            Assert.That(momentum.IsGliding, Is.False);
        }

        [Test]
        public void Glide_CoversSpeedTimesTimeConstant_LessTheStoppedTail()
        {
            PanMomentum momentum = Create();
            Drag(momentum, new Vector3(4f, 0f, 0f), 0.3f);
            momentum.Release();

            // Exponential decay integrates to v * tau, and stopping early drops the last stopSpeed * tau.
            float expected = (4f - StopSpeed) * TimeConstant;
            Assert.That(Glide(momentum, Frame).x, Is.EqualTo(expected).Within(0.02f));
        }

        [Test]
        public void Glide_CoversTheSameDistanceAtAnyFrameRate()
        {
            PanMomentum slow = Create();
            PanMomentum fast = Create();
            Drag(slow, new Vector3(4f, 0f, 0f), 0.3f, 1f / 30f);
            Drag(fast, new Vector3(4f, 0f, 0f), 0.3f, 1f / 120f);
            slow.Release();
            fast.Release();

            Assert.That(Glide(slow, 1f / 30f).x, Is.EqualTo(Glide(fast, 1f / 120f).x).Within(0.02f));
        }

        [Test]
        public void Glide_KeepsSlowingDown()
        {
            PanMomentum momentum = Create();
            Drag(momentum, new Vector3(4f, 0f, 0f), 0.3f);
            momentum.Release();

            float previous = float.MaxValue;
            for (int i = 0; i < 30; i++)
            {
                float step = momentum.Step(Frame).x;
                Assert.That(step, Is.LessThan(previous));
                previous = step;
            }
        }

        [Test]
        public void Stop_EndsTheGlide()
        {
            PanMomentum momentum = Create();
            Drag(momentum, new Vector3(4f, 0f, 0f), 0.3f);
            momentum.Release();
            momentum.Stop();

            Assert.That(momentum.Step(Frame), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ApplyLimits_StopsOnlyTheBlockedAxis()
        {
            PanMomentum momentum = Create();
            Drag(momentum, new Vector3(4f, 0f, 3f), 0.3f);
            momentum.Release();

            Vector3 requested = momentum.Step(Frame);
            momentum.ApplyLimits(requested, new Vector3(0f, 0f, requested.z));

            Assert.That(momentum.Velocity.x, Is.EqualTo(0f));
            Assert.That(momentum.Velocity.z, Is.GreaterThan(0f));
        }

        [Test]
        public void ApplyLimits_BothAxesBlocked_EndsTheGlide()
        {
            PanMomentum momentum = Create();
            Drag(momentum, new Vector3(4f, 0f, 3f), 0.3f);
            momentum.Release();

            momentum.ApplyLimits(momentum.Step(Frame), Vector3.zero);

            Assert.That(momentum.IsGliding, Is.False);
        }
    }
}
