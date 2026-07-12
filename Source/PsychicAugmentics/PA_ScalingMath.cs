using RimWorld;
using UnityEngine;
using Verse;

namespace PsychicAugmentics
{
    /// The single home of the sensitivity power-law formulas, shared by
    /// Verb_EltexCannon (live combat) and Hediff_EltexCannon (health-tab
    /// tooltip) so displayed numbers always match fired ones.
    public static class PA_ScalingMath
    {
        public static float Sensitivity(Pawn pawn, float minSensitivity) =>
            Mathf.Max(pawn?.GetStatValue(StatDefOf.PsychicSensitivity) ?? 1f,
                      minSensitivity);

        public static float DamageMultiplier(float s, float exp) =>
            Mathf.Pow(s, exp);

        public static int Burst(int baseBurst, float s, float exp) =>
            Mathf.Max(1, Mathf.RoundToInt(baseBurst * Mathf.Pow(s, exp)));

        public static int TicksBetween(int baseTicks, float s, float exp) =>
            Mathf.Max(1, Mathf.RoundToInt(baseTicks / Mathf.Pow(s, exp)));

        public static float Warmup(float baseWarmup, float s, float exp) =>
            baseWarmup / Mathf.Pow(s, exp);

        public static float Cooldown(float baseCooldown, float s, float exp) =>
            baseCooldown / Mathf.Pow(s, exp);

        // miss chance shrinks by s^exp (asymptotic below 100%), then
        // missConversion removes a flat fraction of what remains (arcane)
        public static float Accuracy(float baseAcc, float s, float exp,
            float missConversion) =>
            Mathf.Clamp01(1f - (1f - baseAcc) / Mathf.Pow(s, exp)
                          * (1f - missConversion));
    }
}
