using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PsychicAugmentics
{
    // ---------- Tox ----------

    public class PA_DotDamageDef : PA_EltexDamageDef
    {
        public HediffDef dotHediff;
        public float dotSeconds = 3f;
    }

    /// Hits refresh (never stack) a timed DoT hediff on fleshy targets;
    /// the hediff's HediffComp_PercentDot deals the body-size-scaled damage.
    public class DamageWorker_EltexTox : DamageWorker_EltexBolt
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            var result = base.Apply(dinfo, thing);
            if (def is PA_DotDamageDef d && d.dotHediff != null
                && thing is Pawn victim && !victim.Dead && victim.RaceProps.IsFlesh)
            {
                var dot = victim.health.hediffSet.GetFirstHediffOfDef(d.dotHediff)
                          ?? victim.health.AddHediff(d.dotHediff);
                var timer = dot.TryGetComp<HediffComp_Disappears>();
                if (timer != null)
                    timer.ticksToDisappear = Mathf.RoundToInt(d.dotSeconds * 60f);
            }
            return result;
        }
    }

    // ---------- Heat ----------

    public class PA_HeatDamageDef : PA_EltexDamageDef
    {
        // entropy drained from the SHOOTER per hit and converted to Burn
        public float entropyVentPerHit = 8f;
        public float burnPerEntropy = 0.75f;
    }

    /// The cannon as pressure valve: each hit vents the shooter's own
    /// psychic entropy into the target as bonus Burn damage.
    public class DamageWorker_EltexHeat : DamageWorker_EltexBolt
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            var result = base.Apply(dinfo, thing);
            if (def is PA_HeatDamageDef d
                && dinfo.Instigator is Pawn shooter
                && shooter.psychicEntropy != null && !thing.Destroyed)
            {
                float vent = Mathf.Min(shooter.psychicEntropy.EntropyValue, d.entropyVentPerHit);
                if (vent > 0.01f)
                {
                    shooter.psychicEntropy.TryAddEntropy(-vent, scale: false, overLimit: true);
                    thing.TakeDamage(new DamageInfo(DamageDefOf.Burn, vent * d.burnPerEntropy,
                        instigator: shooter, weapon: dinfo.Weapon));
                }
            }
            return result;
        }
    }

    // ---------- Water of Tranquility / Mercury ----------

    public class PA_MendDamageDef : PA_EltexDamageDef
    {
        public bool healFlesh;             // water: biologics
        public bool healNonFleshPawns;     // mercury: mechs
        public bool repairBuildings;       // mercury: walls, turrets, ...
        public float buildingRepairFactor = 3f;
    }

    /// Bolts that mend instead of harm. Anything the def doesn't cover is
    /// simply untouched, so stray shots are never harmful. Fresh injuries
    /// only — permanent scars and diseases stay a doctor's job.
    public class DamageWorker_EltexMend : DamageWorker_EltexBolt
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            var d = def as PA_MendDamageDef;
            var result = new DamageResult();
            if (d == null)
                return result;

            float amount = dinfo.Amount * Power(dinfo);

            if (thing is Pawn pawn && !pawn.Dead
                && (pawn.RaceProps.IsFlesh ? d.healFlesh : d.healNonFleshPawns))
            {
                HealInjuries(pawn, amount);
            }
            else if (d.repairBuildings && thing is Building building && building.def.useHitPoints)
            {
                building.HitPoints = Mathf.Min(building.MaxHitPoints,
                    building.HitPoints + Mathf.RoundToInt(amount * d.buildingRepairFactor));
            }
            return result;
        }

        private static void HealInjuries(Pawn pawn, float amount)
        {
            var injuries = pawn.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(i => !i.IsPermanent() && i.CanHealNaturally())
                .OrderByDescending(i => i.Severity)
                .ToList();
            foreach (var injury in injuries)
            {
                if (amount <= 0f) break;
                float heal = Mathf.Min(amount, injury.Severity);
                injury.Heal(heal);
                amount -= heal;
            }
            if (pawn.Spawned)
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "+", 2.5f);
        }
    }

    // ---------- Cryo ----------

    public class PA_CryoDamageDef : PA_EltexDamageDef
    {
        public HediffDef chillHediff;
        public float severityPerHit = 0.12f;
    }

    /// Hits stack a decaying slow hediff; the hediff's maxSeverity caps it.
    public class DamageWorker_EltexCryo : DamageWorker_EltexBolt
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            var result = base.Apply(dinfo, thing);
            if (def is PA_CryoDamageDef d && d.chillHediff != null
                && thing is Pawn victim && !victim.Dead)
            {
                var chill = victim.health.hediffSet.GetFirstHediffOfDef(d.chillHediff);
                if (chill == null)
                {
                    chill = victim.health.AddHediff(d.chillHediff);
                    chill.Severity = d.severityPerHit;
                }
                else
                {
                    chill.Severity += d.severityPerHit;
                }
            }
            return result;
        }
    }

    // ---------- Vacuum ----------

    public class PA_VacuumDamageDef : PA_EltexDamageDef
    {
        // knockback distance in cells at body size 1, divided by body size:
        // raiders fly, bosses barely twitch (and can't be displacement-locked)
        public float pushCells = 2.5f;
    }

    /// Damage plus body-size-resisted knockback away from the shooter.
    public class DamageWorker_EltexVacuum : DamageWorker_EltexBolt
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            var result = base.Apply(dinfo, thing);
            if (def is PA_VacuumDamageDef d
                && thing is Pawn victim && !victim.Dead && victim.Spawned
                && dinfo.Instigator != null && dinfo.Instigator.Spawned)
            {
                int cells = Mathf.RoundToInt(d.pushCells / Mathf.Max(victim.BodySize, 0.01f));
                if (cells > 0)
                    Push(victim, dinfo.Instigator.Position, cells);
            }
            return result;
        }

        private static void Push(Pawn victim, IntVec3 from, int cells)
        {
            var dir = (victim.Position - from);
            if (dir == IntVec3.Zero)
                return;
            var step = new IntVec3(System.Math.Sign(dir.x), 0, System.Math.Sign(dir.z));
            var dest = victim.Position;
            for (int i = 0; i < cells; i++)
            {
                var next = dest + step;
                if (!next.InBounds(victim.Map) || !next.Standable(victim.Map))
                    break;
                dest = next;
            }
            if (dest != victim.Position)
            {
                victim.Position = dest;
                victim.Notify_Teleported(false, false);
                victim.stances?.stagger.StaggerFor(60);
            }
        }
    }

    // ---------- Void-shard ----------

    public class PA_VoidMutation
    {
        public HediffDef hediff;
        public BodyPartDef part;   // null = whole body
        public float weight = 1f;
    }

    public class PA_VoidDamageDef : PA_EltexDamageDef
    {
        public List<PA_VoidMutation> mutations = new List<PA_VoidMutation>();
        public float mutationChance = 0.15f;
    }

    /// Each hit has a chance to warp the target's flesh with a random entry
    /// from the XML mutation table (Anomaly hediffs, loaded conditionally).
    public class DamageWorker_EltexVoid : DamageWorker_EltexBolt
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            var result = base.Apply(dinfo, thing);
            if (def is PA_VoidDamageDef d && d.mutations.Count > 0
                && thing is Pawn victim && !victim.Dead && victim.RaceProps.IsFlesh
                && Rand.Chance(d.mutationChance))
            {
                var pick = d.mutations.RandomElementByWeight(m => m.weight);
                if (pick?.hediff == null)
                    return result;
                BodyPartRecord part = null;
                if (pick.part != null)
                {
                    part = victim.health.hediffSet.GetNotMissingParts()
                        .Where(p => p.def == pick.part)
                        .RandomElementWithFallback();
                    if (part == null)
                        return result; // nothing left to mutate there
                }
                victim.health.AddHediff(pick.hediff, part);
                if (victim.Spawned)
                    MoteMaker.ThrowText(victim.DrawPos, victim.Map,
                        "PA_VoidMutated".Translate(), 3.5f);
            }
            return result;
        }
    }
}
