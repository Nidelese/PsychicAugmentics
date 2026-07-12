using RimWorld;
using UnityEngine;
using Verse;

namespace PsychicAugmentics
{
    /// DamageDef subclass so the sensitivity curve can live in XML alongside
    /// the rest of the damage def. Core damage defs subclass this further.
    public class PA_EltexDamageDef : DamageDef
    {
        // damage = baseDamage * sensitivity^exponent — uncapped power law,
        // diminishing returns, baseline at sensitivity 1.0
        public float sensitivityExponent = 0.75f;

        // floor so psychically deaf pawns deal token damage instead of zero
        public float minSensitivity = 0.05f;

        // optional secondary damage applied after the main hit (EMP for the
        // lightning core, VacuumBurn for the vacuum core, ...) scaled by its
        // own gentler exponent
        public DamageDef extraDamage;
        public float extraDamageAmount;
        public float extraDamageExponent = 0.35f;
    }

    /// Scales damage by the firing pawn's psychic sensitivity before
    /// delegating to vanilla injury logic, then applies any configured
    /// secondary damage. Non-pawn instigators fire at 1x base damage.
    public class DamageWorker_EltexBolt : DamageWorker_AddInjury
    {
        protected PA_EltexDamageDef EltexDef => def as PA_EltexDamageDef;

        protected float Sensitivity(DamageInfo dinfo) =>
            dinfo.Instigator is Pawn pawn
                ? Mathf.Max(pawn.GetStatValue(StatDefOf.PsychicSensitivity),
                            EltexDef?.minSensitivity ?? 0.05f)
                : 1f;

        /// sensitivity^exponent multiplier for the main hit
        protected float Power(DamageInfo dinfo) =>
            Mathf.Pow(Sensitivity(dinfo), EltexDef?.sensitivityExponent ?? 0.75f);

        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            dinfo.SetAmount(dinfo.Amount * Power(dinfo));
            var result = base.Apply(dinfo, thing);
            ApplyExtraDamage(dinfo, thing);
            return result;
        }

        private void ApplyExtraDamage(DamageInfo dinfo, Thing thing)
        {
            var d = EltexDef;
            if (d?.extraDamage == null || d.extraDamageAmount <= 0f || thing.Destroyed)
                return;
            float amount = d.extraDamageAmount
                * Mathf.Pow(Sensitivity(dinfo), d.extraDamageExponent);
            thing.TakeDamage(new DamageInfo(d.extraDamage, amount,
                instigator: dinfo.Instigator, weapon: dinfo.Weapon));
        }
    }
}
