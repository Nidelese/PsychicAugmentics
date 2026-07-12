using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace PsychicAugmentics
{
    public class PA_VisionInducerExtension : DefModExtension
    {
        // chance per session for the visions to crystallize an inspiration
        public float inspirationChance = 0.15f;
        // session length; 2500 ticks = 1 in-game hour
        public int durationTicks = 2500;
    }

    /// The trauma engine's benign sibling: pawns visit it on their own as
    /// recreation (JoyGiver in XML), or can be sent by right-click.
    public class Building_VisionInducer : Building
    {
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (var option in base.GetFloatMenuOptions(selPawn))
                yield return option;

            string label = "PA_UseVisionInducerOption".Translate();
            if (!selPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly))
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
                        JobMaker.MakeJob(PA_DefOf.PA_UseVisionInducer, this),
                        JobTag.Misc));
            }
        }
    }

    /// Recreation flows during the session; completing it leaves a pleasant
    /// afterglow and may crystallize an inspiration.
    public class JobDriver_UseVisionInducer : JobDriver
    {
        private Building Machine => (Building)job.targetA.Thing;

        private PA_VisionInducerExtension Ext =>
            Machine.def.GetModExtension<PA_VisionInducerExtension>();

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(Machine, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Machine.TryGetComp<CompPowerTrader>()?.PowerOn != true);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            var session = Toils_General.Wait(Ext?.durationTicks ?? 2500, TargetIndex.A);
            session.WithProgressBarToilDelay(TargetIndex.A);
            session.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            // joy caps mid-session rather than ending the job: the outcome
            // roll only happens on a completed session
            session.tickAction = () =>
            {
                pawn.rotationTracker.FaceTarget(Machine);
                JoyUtility.JoyTickCheckEnd(pawn, 1, JoyTickFullJoyAction.None, 1f, Machine);
            };
            session.socialMode = RandomSocialMode.Off;
            yield return session;

            yield return Toils_General.Do(ApplyOutcome);
        }

        private void ApplyOutcome()
        {
            var memories = pawn.needs?.mood?.thoughts?.memories;
            // the afterglow memory doubles as the inspiration cooldown:
            // no new roll while the last session's glow still lingers
            bool stillGlowing =
                memories?.GetFirstMemoryOfDef(PA_DefOf.PA_PleasantVisions) != null;
            memories?.TryGainMemory(PA_DefOf.PA_PleasantVisions);

            if (stillGlowing || !Rand.Chance(Ext?.inspirationChance ?? 0.15f))
                return;

            // same weighted pick vanilla uses for random inspirations
            var inspiration = DefDatabase<InspirationDef>.AllDefsListForReading
                .Where(d => d.Worker.InspirationCanOccur(pawn))
                .RandomElementByWeightWithFallback(d => d.Worker.CommonalityFor(pawn));
            if (inspiration != null)
                pawn.mindState.inspirationHandler.TryStartInspiration(inspiration,
                    "PA_VisionInspirationReason".Translate(pawn.Named("PAWN")));
        }
    }
}
