using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PsychicAugmentics
{
    /// Ranged verb for the eltex arm cannon implant. Before each warmup begins
    /// it clones its VerbProperties into a per-instance copy and rewrites the
    /// fields that scale with the caster's psychic sensitivity, so the shared
    /// def object is never mutated and pawns never see each other's scaling.
    ///
    /// Scaling is an uncapped power law (see EltexCannonVerbProps): every stat
    /// keeps improving forever, but sublinearly. Burst count and (via the
    /// DamageWorker) per-bolt damage carry the growth at extreme sensitivity,
    /// since fire-rate ticks bottom out at the engine floor of 1.
    public class Verb_EltexCannon : Verb_Shoot
    {
        private bool ownedProps;

        // Cached base values from the original (shared) VerbProperties.
        private int   baseBurst;
        private int   baseTicks;
        private float baseWarmup;
        private float baseCooldown;
        private float baseAccTouch, baseAccShort, baseAccMedium, baseAccLong;

        private EltexCannonVerbProps EltexProps => verbProps as EltexCannonVerbProps;

        /// The bearer's attuned core, if any: swaps the projectile and can
        /// carry verb tweaks like arcane guidance.
        private Hediff_EltexCore ActiveCore =>
            CasterPawn?.health.hediffSet.hediffs
                .OfType<Hediff_EltexCore>()
                .FirstOrDefault(c => c.Attuned);

        public override ThingDef Projectile =>
            ActiveCore?.Core?.projectileOverride ?? base.Projectile;

        private float Sensitivity =>
            PA_ScalingMath.Sensitivity(CasterPawn, EltexProps?.minSensitivity ?? 0.05f);

        // Called once: snapshot base values then replace verbProps with our own
        // clone so modifications don't bleed onto other verb instances.
        private void EnsureOwned()
        {
            if (ownedProps) return;
            ownedProps = true;

            if (verbProps is EltexCannonVerbProps ep)
            {
                baseBurst     = ep.burstShotCount;
                baseTicks     = ep.ticksBetweenBurstShots;
                baseWarmup    = ep.warmupTime;
                baseCooldown  = ep.defaultCooldownTime;
                baseAccTouch  = ep.accuracyTouch;
                baseAccShort  = ep.accuracyShort;
                baseAccMedium = ep.accuracyMedium;
                baseAccLong   = ep.accuracyLong;
                verbProps     = ep.Clone();
            }
        }

        private void ApplyScaling()
        {
            var ep = EltexProps;
            if (ep == null) return;

            float s = Sensitivity;
            // arcane core: convert a flat fraction of remaining miss chance
            // into hits, multiplicative with the sensitivity curve
            float conversion = ActiveCore?.Core?.missConversion ?? 0f;

            ep.burstShotCount         = PA_ScalingMath.Burst(baseBurst, s, ep.burstExponent);
            ep.ticksBetweenBurstShots = PA_ScalingMath.TicksBetween(baseTicks, s, ep.fireRateExponent);
            ep.warmupTime             = PA_ScalingMath.Warmup(baseWarmup, s, ep.aimExponent);
            ep.defaultCooldownTime    = PA_ScalingMath.Cooldown(baseCooldown, s, ep.cooldownExponent);

            ep.accuracyTouch  = PA_ScalingMath.Accuracy(baseAccTouch,  s, ep.accuracyExponent, conversion);
            ep.accuracyShort  = PA_ScalingMath.Accuracy(baseAccShort,  s, ep.accuracyExponent, conversion);
            ep.accuracyMedium = PA_ScalingMath.Accuracy(baseAccMedium, s, ep.accuracyExponent, conversion);
            ep.accuracyLong   = PA_ScalingMath.Accuracy(baseAccLong,   s, ep.accuracyExponent, conversion);
        }

        public override bool TryStartCastOn(
            LocalTargetInfo castTarg, LocalTargetInfo destTarg,
            bool surpriseAttack = false, bool canHitNonTargetPawns = true,
            bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            EnsureOwned();
            ApplyScaling();
            return base.TryStartCastOn(castTarg, destTarg, surpriseAttack,
                canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
        }
    }
}
