using Verse;

namespace PsychicAugmentics
{
    /// VerbProperties subclass carrying per-stat sensitivity scaling exponents.
    /// Declare as <li Class="PsychicAugmentics.EltexCannonVerbProps"> in the hediff XML.
    ///
    /// All curves are uncapped power laws with diminishing returns:
    ///     multiplier = sensitivity ^ exponent      (exponent < 1)
    /// Sensitivity 1.0 is always the baseline. There is no ceiling — a pawn
    /// with absurd sensitivity gets absurd output — but each additional point
    /// buys less than the last, so "ridiculous DPS" genuinely requires
    /// ridiculous sensitivity rather than merely high sensitivity.
    ///
    /// Damage scaling does NOT live here — it belongs to PA_EltexDamageDef /
    /// DamageWorker_EltexBolt, so future implants can swap the damage element
    /// without touching the verb.
    public class EltexCannonVerbProps : VerbProperties
    {
        // burst = max(1, round(baseBurst * s^exp)) — uncapped
        public float burstExponent = 0.50f;

        // ticksBetweenShots = max(1, round(baseTicks / s^exp)) — engine-floored at 1 tick
        public float fireRateExponent = 0.35f;

        // warmup = baseWarmup / s^exp
        public float aimExponent = 0.40f;

        // cooldown = baseCooldown / s^exp
        public float cooldownExponent = 0.30f;

        // missChance = baseMissChance / s^exp — accuracy approaches but never
        // reaches 100%, and degrades symmetrically below sensitivity 1
        public float accuracyExponent = 0.50f;

        // sensitivity floor: psychically deaf pawns fire at this effective
        // sensitivity instead of dividing by zero / dealing zero damage
        public float minSensitivity = 0.05f;

        public EltexCannonVerbProps Clone() => (EltexCannonVerbProps)MemberwiseClone();
    }
}
