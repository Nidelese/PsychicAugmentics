using System.Collections.Generic;
using Verse;

namespace PsychicAugmentics
{
    /// Deferred damage queue. Dealing damage from inside a hediff's own tick
    /// can kill the pawn mid-iteration of its hediff list (corpse silently
    /// vanishes); queueing to the game component tick avoids that entirely.
    public class PA_GameComponent : GameComponent
    {
        private static readonly Queue<(Pawn pawn, DamageDef damage, float amount)> queue
            = new Queue<(Pawn, DamageDef, float)>();

        public PA_GameComponent(Game game)
        {
            // Static state survives returning to the main menu and starting a
            // new game in the same process. Never let a queued hit retain a
            // pawn from the previous save.
            queue.Clear();
        }

        public static void QueueDamage(Pawn pawn, DamageDef damage, float amount) =>
            queue.Enqueue((pawn, damage, amount));

        public override void GameComponentTick()
        {
            while (queue.Count > 0)
            {
                var (pawn, damage, amount) = queue.Dequeue();
                if (pawn != null && !pawn.Dead && !pawn.Destroyed)
                    pawn.TakeDamage(new DamageInfo(damage, amount));
            }
        }
    }

    public class HediffCompProperties_PercentDot : HediffCompProperties
    {
        // "percent of max HP" analogue: RimWorld has no pawn HP pool, so the
        // DoT scales with body size — which is also what makes it melt
        // big-bodied bosses without one-shotting rats
        public float damagePerSecondPerBodySize = 2f;
        public DamageDef damage;

        public HediffCompProperties_PercentDot() =>
            compClass = typeof(HediffComp_PercentDot);
    }

    public class HediffComp_PercentDot : HediffComp
    {
        public HediffCompProperties_PercentDot Props =>
            (HediffCompProperties_PercentDot)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn.IsHashIntervalTick(60) && Props.damage != null)
                PA_GameComponent.QueueDamage(Pawn, Props.damage,
                    Props.damagePerSecondPerBodySize * Pawn.BodySize);
        }
    }
}
