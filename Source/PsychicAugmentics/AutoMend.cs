using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PsychicAugmentics
{
    /// Shared logic for automatic mend-bolt work. A pawn qualifies when
    /// their cannon's CURRENT projectile mends — i.e. a water of tranquility
    /// or mercury core is attuned. Detection is data-driven off
    /// PA_MendDamageDef's flags, not core defNames: healFlesh cores treat
    /// colonists and animals, healNonFleshPawns cores treat colony mechs,
    /// and any future mending core inherits the behavior for free.
    /// Building repair (repairBuildings) is deliberately NOT automated —
    /// walls are mended by manually targeting the cannon.
    public static class PA_MendUtility
    {
        public static Verb MendVerb(Pawn pawn)
        {
            if (pawn?.health == null)
                return null;
            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                var giver = (hediff as HediffWithComps)?.TryGetComp<HediffComp_VerbGiver>();
                if (giver == null)
                    continue;
                foreach (var verb in giver.VerbTracker.AllVerbs)
                {
                    var mend = MendDef(verb);
                    if (mend != null && (mend.healFlesh || mend.healNonFleshPawns))
                        return verb;
                }
            }
            return null;
        }

        public static PA_MendDamageDef MendDef(Verb verb) =>
            (verb as Verb_EltexCannon)?.Projectile?.projectile?.damageDef
                as PA_MendDamageDef;

        // Must mirror DamageWorker_EltexMend.Apply's routing.
        public static bool CanMendPawn(Verb verb, Pawn target)
        {
            var mend = MendDef(verb);
            return mend != null
                && (target.RaceProps.IsFlesh ? mend.healFlesh : mend.healNonFleshPawns);
        }

        // Must mirror DamageWorker_EltexMend.HealInjuries: fresh injuries
        // only — scars and diseases stay a doctor's job.
        public static bool HasHealableInjury(Pawn target) =>
            target.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Any(i => !i.IsPermanent() && i.CanHealNaturally());

        public static bool AutoMendEnabled(Pawn pawn) =>
            pawn.health.hediffSet.hediffs
                .Select(h => (h as HediffWithComps)?.TryGetComp<HediffComp_AutoMendToggle>())
                .FirstOrDefault(c => c != null)?.AutoMend ?? true;
    }

    public class HediffCompProperties_AutoMendToggle : HediffCompProperties
    {
        public HediffCompProperties_AutoMendToggle() =>
            compClass = typeof(HediffComp_AutoMendToggle);
    }

    /// Lives on the cannon hediff so the setting survives core swaps.
    /// The gizmo only shows while a mending core is attuned.
    public class HediffComp_AutoMendToggle : HediffComp
    {
        private bool autoMend = true;

        public bool AutoMend => autoMend;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref autoMend, "autoMend", true);
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (Pawn.Faction != Faction.OfPlayer
                || PA_MendUtility.MendVerb(Pawn) == null)
                yield break;
            // show the attuned core's sphere on the button
            var coreItem = Pawn.health.hediffSet.hediffs
                .OfType<Hediff_EltexCore>()
                .FirstOrDefault(c => c.Attuned)?.def.spawnThingOnRemoved;
            yield return new Command_Toggle
            {
                defaultLabel = "PA_AutoMendLabel".Translate(),
                defaultDesc = "PA_AutoMendDesc".Translate(),
                icon = coreItem != null
                    ? coreItem.uiIcon
                    : ContentFinder<Texture2D>.Get("PA/EltexArmCannon"),
                isActive = () => autoMend,
                toggleAction = () => autoMend = !autoMend,
            };
        }
    }

    /// Doctor work: pawns with an attuned mending core seek out damaged
    /// faction pawns their bolts can help and shoot them better.
    public class WorkGiver_EltexAutoMend : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) =>
            pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);

        public override bool ShouldSkip(Pawn pawn, bool forced = false) =>
            !PA_MendUtility.AutoMendEnabled(pawn)
            || PA_MendUtility.MendVerb(pawn) == null;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Pawn target) || target == pawn
                || target.Dead || !target.Spawned
                || target.HostileTo(pawn)  // berserk colonists don't get free heals
                || !PA_MendUtility.CanMendPawn(PA_MendUtility.MendVerb(pawn), target)
                || !PA_MendUtility.HasHealableInjury(target))
                return false;
            return pawn.CanReserve(target, 1, -1, null, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var job = JobMaker.MakeJob(PA_DefOf.PA_EltexAutoMend, t);
            job.verbToUse = PA_MendUtility.MendVerb(pawn);
            // let emergencies (real tending, firefighting) take over
            job.expiryInterval = 2000;
            job.checkOverrideOnExpire = true;
            return job;
        }
    }

    /// Walk into cannon range of the target, then keep casting mend bolts
    /// until no fresh injuries remain. The verb's own warmup, burst and
    /// cooldown stances pace the shots.
    public class JobDriver_EltexAutoMend : JobDriver
    {
        private const int MaxRepositions = 4;
        private int repositions;

        private Pawn Target => (Pawn)job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref repositions, "repositions");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => job.verbToUse == null);

            Toil gotoCastPos = Toils_Combat.GotoCastPosition(TargetIndex.A);
            yield return gotoCastPos;

            Toil cast = ToilMaker.MakeToil("AutoMendCast");
            cast.tickAction = () =>
            {
                Pawn target = Target;
                if (target == null || target.Dead || !target.Spawned
                    || !PA_MendUtility.HasHealableInjury(target))
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                if (!job.verbToUse.CanHitTarget(target))
                {
                    // target wandered off; chase a few times, then give up
                    // (the work giver will re-issue if still worthwhile)
                    if (++repositions > MaxRepositions)
                        EndJobWith(JobCondition.Incompletable);
                    else
                        JumpToToil(gotoCastPos);
                    return;
                }
                if (!pawn.stances.FullBodyBusy)
                    job.verbToUse.TryStartCastOn(target);
            };
            cast.defaultCompleteMode = ToilCompleteMode.Never;
            cast.socialMode = RandomSocialMode.Off;
            cast.activeSkill = () => SkillDefOf.Shooting;
            yield return cast;
        }
    }
}
