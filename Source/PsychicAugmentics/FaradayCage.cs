using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PsychicAugmentics
{
    public class HediffCompProperties_FaradayToggle : HediffCompProperties
    {
        public string gizmoIconPath = "PA/FaradayCage";

        // Grounded = sensitivity CAPPED at this absolute value (not
        // multiplied): a cage blocks the field entirely or not at all.
        // A multiplicative factor fails at VPE scale — 90,000% sensitivity
        // x0.2 is still 18,000%, so the pawns who need drone protection
        // most would get the least.
        public float groundedSensitivityCap = 0.15f;

        public HediffCompProperties_FaradayToggle() =>
            compClass = typeof(HediffComp_FaradayToggle);
    }

    /// Injects the clamp into the vanilla stat so every consumer of
    /// psychic sensitivity (drones, lances, psycasts, our cannon and its
    /// tooltip) sees the capped value with an explanation line.
    [StaticConstructorOnStartup]
    public static class PA_StatPartInjector
    {
        static PA_StatPartInjector()
        {
            var stat = StatDefOf.PsychicSensitivity;
            stat.parts ??= new List<StatPart>();
            stat.parts.Add(new StatPart_FaradayCage { parentStat = stat });
        }
    }

    public class StatPart_FaradayCage : StatPart
    {
        private static HediffComp_FaradayToggle GroundedCage(StatRequest req) =>
            req.Thing is Pawn pawn
                ? pawn.health?.hediffSet?.hediffs
                    .Select(h => h.TryGetComp<HediffComp_FaradayToggle>())
                    .FirstOrDefault(c => c != null && c.Grounded)
                : null;

        public override void TransformValue(StatRequest req, ref float val)
        {
            var cage = GroundedCage(req);
            if (cage != null)
                val = Mathf.Min(val, cage.Props.groundedSensitivityCap);
        }

        public override string ExplanationPart(StatRequest req)
        {
            var cage = GroundedCage(req);
            return cage != null
                ? "PA_FaradayCapExplanation".Translate(
                    cage.Props.groundedSensitivityCap.ToStringPercent())
                : null;
        }
    }

    /// Toggle gizmo for the faraday cage implant: severity 1 = grounded
    /// (the XML stage carries the psychic sensitivity factor), severity
    /// 0.1 = open (inert). Severity persists in the save for free.
    public class HediffComp_FaradayToggle : HediffComp
    {
        private const float GroundedSeverity = 1f;
        private const float OpenSeverity = 0.1f;

        public HediffCompProperties_FaradayToggle Props =>
            (HediffCompProperties_FaradayToggle)props;

        public bool Grounded => parent.Severity >= 0.9f;

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (Pawn.Faction != Faction.OfPlayer)
                yield break;
            yield return new Command_Toggle
            {
                defaultLabel = "PA_FaradayToggleLabel".Translate(),
                defaultDesc = "PA_FaradayToggleDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get(Props.gizmoIconPath),
                isActive = () => Grounded,
                toggleAction = () =>
                    parent.Severity = Grounded ? OpenSeverity : GroundedSeverity,
            };
        }
    }
}
