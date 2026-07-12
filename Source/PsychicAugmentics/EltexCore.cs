using System.Linq;
using Verse;

namespace PsychicAugmentics
{
    /// Placed on core hediff defs: links the core to the cannon it modifies
    /// and carries the verb-side behavior tweaks.
    public class PA_CoreExtension : DefModExtension
    {
        // hediff the bearer must have for the core to attune (the cannon)
        public HediffDef requiresCannon;

        // projectile the cannon fires while this core is attuned;
        // null keeps the cannon's default bolt (arcane)
        public ThingDef projectileOverride;

        // fraction of remaining miss chance converted to hits (arcane)
        public float missConversion;
    }

    /// Core implant hediff, installed near the heart. Severity 1 = attuned:
    /// socketed to a cannon and inert in the body. Severity below that =
    /// unregulated: the core discharges into the bearer's nervous system
    /// (the harmful stage lives in XML). Only one core can be attuned at a
    /// time — installing a new one ejects the old sphere onto the ground.
    public class Hediff_EltexCore : HediffWithComps
    {
        private const int RegulationCheckInterval = 150;

        public PA_CoreExtension Core => def.GetModExtension<PA_CoreExtension>();

        public bool Attuned => Severity >= 0.9f;

        private bool HasCannon =>
            Core?.requiresCannon != null
            && pawn.health.hediffSet.HasHediff(Core.requiresCannon);

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            // one socket: eject any other core as an item
            var others = pawn.health.hediffSet.hediffs
                .Where(h => h != this && h is Hediff_EltexCore).ToList();
            foreach (var other in others)
            {
                if (other.def.spawnThingOnRemoved != null && pawn.SpawnedOrAnyParentSpawned)
                    GenSpawn.Spawn(other.def.spawnThingOnRemoved, pawn.PositionHeld, pawn.MapHeld);
                pawn.health.RemoveHediff(other);
            }
            RefreshRegulation();
        }

        public override void Tick()
        {
            base.Tick();
            if (pawn.IsHashIntervalTick(RegulationCheckInterval))
                RefreshRegulation();
        }

        private void RefreshRegulation() => Severity = HasCannon ? 1f : 0.1f;
    }
}
