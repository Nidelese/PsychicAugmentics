using System.Linq;
using System.Text;
using Verse;

namespace PsychicAugmentics
{
    /// Cannon implant hediff: the health-tab tooltip shows the bearer's
    /// current scaled combat numbers (and attuned core, if any) so scaling
    /// is inspectable without test-firing.
    public class Hediff_EltexCannon : Hediff_Implant
    {
        public override string TipStringExtra
        {
            get
            {
                var sb = new StringBuilder(base.TipStringExtra);
                var ep = def.CompProps<HediffCompProperties_VerbGiver>()?
                    .verbs?.OfType<EltexCannonVerbProps>().FirstOrDefault();
                if (ep == null)
                    return sb.ToString();

                float s = PA_ScalingMath.Sensitivity(pawn, ep.minSensitivity);
                var core = pawn.health.hediffSet.hediffs
                    .OfType<Hediff_EltexCore>().FirstOrDefault(c => c.Attuned);
                float conversion = core?.Core?.missConversion ?? 0f;
                var projectile = core?.Core?.projectileOverride ?? ep.defaultProjectile;
                var eltexDamage = projectile?.projectile?.damageDef as PA_EltexDamageDef;
                float damageExp = eltexDamage?.sensitivityExponent ?? 0.75f;

                string Acc(float a) =>
                    PA_ScalingMath.Accuracy(a, s, ep.accuracyExponent, conversion)
                        .ToStringPercent();

                sb.AppendLine();
                if (core != null)
                    sb.AppendLine($"Attuned core: {core.def.label}");
                sb.AppendLine($"Psychic sensitivity: {s.ToStringPercent()}");
                if (projectile?.projectile != null)
                {
                    float mult = PA_ScalingMath.DamageMultiplier(s, damageExp);
                    int baseDamage = projectile.projectile.GetDamageAmount((Thing)null);
                    sb.AppendLine($"Bolt power: {baseDamage * mult:0.#} ({baseDamage} x {mult.ToStringPercent()})");
                }
                sb.AppendLine($"Burst: {PA_ScalingMath.Burst(ep.burstShotCount, s, ep.burstExponent)} shots, {PA_ScalingMath.TicksBetween(ep.ticksBetweenBurstShots, s, ep.fireRateExponent)} ticks apart");
                sb.AppendLine($"Aim time: {PA_ScalingMath.Warmup(ep.warmupTime, s, ep.aimExponent):0.##}s, cooldown: {PA_ScalingMath.Cooldown(ep.defaultCooldownTime, s, ep.cooldownExponent):0.##}s");
                sb.Append($"Accuracy (T/S/M/L): {Acc(ep.accuracyTouch)} / {Acc(ep.accuracyShort)} / {Acc(ep.accuracyMedium)} / {Acc(ep.accuracyLong)}");
                return sb.ToString();
            }
        }
    }
}
