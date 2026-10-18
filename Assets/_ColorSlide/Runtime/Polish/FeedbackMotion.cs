using System;
using ColorSlide.Gameplay;
using UnityEngine;

namespace ColorSlide.Polish
{
    /// <summary>Small cosmetic envelopes; none advances a session or owns a countdown.</summary>
    public static class FeedbackMotion
    {
        public const float GateSeconds = 0.30f, BlockedSeconds = 0.18f, WinSeconds = 0.60f;
        public const float WarningSeconds = 15f;
        private static float Pulse(float age, float duration)
        {
            if (age <= 0f || age >= duration) return 0f;
            var sine = (float)Math.Sin(Math.PI * age / duration);
            return sine * sine;
        }
        public static float GateStrength(float age) => Pulse(age, GateSeconds);
        public static float BlockedStrength(float age) => Pulse(age, BlockedSeconds);
        public static Vector3 BlockedScale(float age)
        {
            var strength = BlockedStrength(age);
            return new Vector3(1f - 0.085f * strength, 1f - 0.10f * strength, 1f - 0.085f * strength);
        }
        public static float WinEase(float age) => SoftBurstMotion.Ease(age / WinSeconds);
        public static bool IsUrgent(GameplayState state, double remaining, float threshold) =>
            state == GameplayState.Playing && remaining > 0d && remaining <= threshold;
        public static Color TimerTint(Color original, double remaining, float threshold)
        {
            var age = (float)Math.Max(0d, threshold - remaining);
            var blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.4f));
            var breath = 0.5f - 0.5f * (float)Math.Cos(age * Math.PI * 2d / 2.4d);
            var warm = Color.Lerp(new Color(0.98f, 0.85f, 0.71f, original.a), new Color(0.96f, 0.77f, 0.69f, original.a), breath * 0.55f);
            return Color.Lerp(original, warm, blend);
        }
    }
}
