using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimHealthSerums;

public class HediffCompProperties_SerumTreatment : HediffCompProperties
{
    public TreatmentKind kind;

    [MustTranslate]
    public string finishedMessage;

    public List<HediffDef> cures;

    public List<ChemicalDef> excludedChemicals;

    public FloatRange yearsRange = new FloatRange(1f, 3f);

    public ThingDef serum;

    public List<AfterEffect> aftereffects;

    public HediffCompProperties_SerumTreatment()
    {
        compClass = typeof(HediffComp_SerumTreatment);
    }
}

public class HediffComp_SerumTreatment : HediffComp
{
    public BodyPartRecord part;

    public HediffDef target;

    private bool done;

    public HediffCompProperties_SerumTreatment Props => (HediffCompProperties_SerumTreatment)props;

    public override bool CompShouldRemove => done || (Serums.NeedsPart(Props.kind) && part == null);

    public override string CompLabelInBracketsExtra
    {
        get
        {
            string subject = part?.Label ?? target?.label;
            string progress = parent.Severity.ToStringPercent();
            return subject == null ? progress : subject + ", " + progress;
        }
    }

    public override void CompPostTickInterval(ref float severityAdjustment, int delta)
    {
        if (done)
        {
            return;
        }
        if (!Serums.CanTreat(Pawn, Props, part, target))
        {
            HediffDef next = Props.kind == TreatmentKind.CureChronic ? Serums.FirstPresent(Pawn.health.hediffSet, Props.cures) : null;
            if (next != null)
            {
                target = next;
                return;
            }
            done = true;
            if (PawnUtility.ShouldSendNotificationAbout(Pawn))
            {
                Messages.Message("ZZRH_TreatmentEnded".Translate(Pawn.LabelShort, Props.serum?.label ?? parent.LabelBase, Pawn.Named("PAWN")), Pawn, MessageTypeDefOf.NeutralEvent);
            }
            return;
        }
        if (parent.Severity < parent.def.maxSeverity)
        {
            if (RimHealthMod.Settings.devFastTreatments && Prefs.DevMode)
            {
                severityAdjustment += delta / 600f;
            }
            return;
        }
        done = true;
        string subject = Serums.Finish(Pawn, Props, part, target);
        if (!Props.finishedMessage.NullOrEmpty() && PawnUtility.ShouldSendNotificationAbout(Pawn))
        {
            Messages.Message(Props.finishedMessage.Formatted(Pawn.LabelShort, subject ?? "", Pawn.Named("PAWN")), Pawn, MessageTypeDefOf.PositiveEvent);
        }
        AfterEffects.Roll(Pawn, Props);
    }

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_BodyParts.Look(ref part, "part");
        Scribe_Defs.Look(ref target, "target");
        Scribe_Values.Look(ref done, "done", false);
    }
}
