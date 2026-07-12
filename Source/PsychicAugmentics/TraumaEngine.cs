using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace PsychicAugmentics
{
    [DefOf]
    public static class PA_DefOf
    {
        public static JobDef PA_UseTraumaEngine;
        public static JobDef PA_UseVisionInducer;
        public static JobDef PA_EltexAutoMend;
        public static ThoughtDef PA_TraumaEngineSubject;
        public static ThoughtDef PA_ColonistTortured;
        public static ThoughtDef PA_PleasantVisions;

        static PA_DefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(PA_DefOf));
    }

    public class PA_TraumaEngineExtension : DefModExtension
    {
        // chance per session to gain a psylink level
        public float psylinkChance = 0.25f;
        // session length; 5000 ticks = 2 in-game hours
        public int durationTicks = 5000;
    }

    /// The psychic trauma engine: right-click with a colonist selected to
    /// begin a session. All consequences live in the job driver.
    public class Building_TraumaEngine : Building
    {
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (var option in base.GetFloatMenuOptions(selPawn))
                yield return option;

            string label = "PA_UseTraumaEngineOption".Translate();
            if (!selPawn.CanReach(this, PathEndMode.Touch, Danger.Deadly))
            {
                yield return new FloatMenuOption(
                    label + ": " + "NoPath".Translate(), null);
            }
            else if (this.TryGetComp<CompPowerTrader>()?.PowerOn != true)
            {
                yield return new FloatMenuOption(
                    label + ": " + "CannotUseNoPower".Translate(), null);
            }
            else
            {
                yield return new FloatMenuOption(label, () =>
                    selPawn.jobs.TryTakeOrderedJob(
                        JobMaker.MakeJob(PA_DefOf.PA_UseTraumaEngine, this),
                        JobTag.Misc));
            }
        }
    }

    public class JobDriver_UseTraumaEngine : JobDriver
    {
        private Building Machine => (Building)job.targetA.Thing;

        private PA_TraumaEngineExtension Ext =>
            Machine.def.GetModExtension<PA_TraumaEngineExtension>();

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(Machine, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Machine.TryGetComp<CompPowerTrader>()?.PowerOn != true);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            var session = Toils_General.Wait(Ext?.durationTicks ?? 5000, TargetIndex.A);
            session.WithProgressBarToilDelay(TargetIndex.A);
            session.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return session;

            yield return Toils_General.Do(ApplyOutcome);
        }

        private void ApplyOutcome()
        {
            // the subject relives the machine's hallucinations for days
            pawn.needs?.mood?.thoughts?.memories?
                .TryGainMemory(PA_DefOf.PA_TraumaEngineSubject);

            // everyone else knows exactly what that scream was
            foreach (var colonist in pawn.Map.mapPawns.FreeColonistsSpawned.ToList())
                if (colonist != pawn)
                    colonist.needs?.mood?.thoughts?.memories?
                        .TryGainMemory(PA_DefOf.PA_ColonistTortured);

            if (Rand.Chance(Ext?.psylinkChance ?? 0.25f))
            {
                pawn.ChangePsylinkLevel(1, sendLetter: false);
                Find.LetterStack.ReceiveLetter(
                    "PA_TraumaAwakeningLabel".Translate(),
                    "PA_TraumaAwakeningText".Translate(pawn.Named("PAWN")),
                    LetterDefOf.PositiveEvent, pawn);
            }
            else
            {
                Messages.Message(
                    "PA_TraumaNoAwakening".Translate(pawn.Named("PAWN")),
                    pawn, MessageTypeDefOf.NeutralEvent);
            }
        }
    }
}
