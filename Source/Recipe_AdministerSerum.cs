using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimHealthSerums;

public class Recipe_AdministerSerum : Recipe_Surgery
{
    private SerumRecipeExtension Ext => recipe.GetModExtension<SerumRecipeExtension>();

    private ThingDef SerumDef => recipe.fixedIngredientFilter?.AnyAllowedDef;

    private HediffCompProperties_SerumTreatment TreatmentProps => Ext?.treatment?.CompProps<HediffCompProperties_SerumTreatment>();

    private bool PartAllowed(BodyPartRecord part)
    {
        if (part == null)
        {
            return false;
        }
        if (!recipe.appliedOnFixedBodyParts.NullOrEmpty() && !recipe.appliedOnFixedBodyParts.Contains(part.def))
        {
            return false;
        }
        List<BodyPartDef> excluded = Ext?.excludedParts;
        return excluded == null || !excluded.Contains(part.def);
    }

    private bool Usable => TreatmentProps != null && RimHealthMod.Settings.Enabled(SerumDef);

    private bool Ready(Pawn pawn)
    {
        return Usable && !Serums.UnderTreatment(pawn) && !Serums.IsAndroid(pawn);
    }

    private bool Treatable(Pawn pawn, BodyPartRecord part)
    {
        HediffCompProperties_SerumTreatment props = TreatmentProps;
        if (Serums.NeedsPart(props.kind))
        {
            return PartAllowed(part) && Serums.CanTreat(pawn, props, part, null);
        }
        return Serums.CanTreat(pawn, props, null, ChronicTarget(pawn));
    }

    public override bool CompletableEver(Pawn surgeryTarget)
    {
        return Usable;
    }

    private HediffDef ChronicTarget(Pawn pawn)
    {
        HediffCompProperties_SerumTreatment props = TreatmentProps;
        return props?.kind == TreatmentKind.CureChronic ? Serums.FirstPresent(pawn.health.hediffSet, props.cures) : null;
    }

    public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
    {
        if (!Usable)
        {
            yield break;
        }
        HediffCompProperties_SerumTreatment props = TreatmentProps;
        HashSet<BodyPartRecord> seen = new HashSet<BodyPartRecord>();
        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            Hediff hediff = hediffs[i];
            bool candidate = props.kind == TreatmentKind.Regrow ? hediff is Hediff_MissingPart : Serums.IsScar(hediff) || (props.cures != null && props.cures.Contains(hediff.def));
            if (candidate && PartAllowed(hediff.Part) && seen.Add(hediff.Part) && Serums.CanTreat(pawn, props, hediff.Part, null))
            {
                yield return hediff.Part;
            }
        }
    }

    public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
    {
        if (!base.AvailableOnNow(thing, part) || !(thing is Pawn pawn) || !Ready(pawn))
        {
            return false;
        }
        return (part == null && Serums.NeedsPart(TreatmentProps.kind)) || Treatable(pawn, part);
    }

    public override string GetLabelWhenUsedOn(Pawn pawn, BodyPartRecord part)
    {
        HediffDef target = ChronicTarget(pawn);
        string label = base.GetLabelWhenUsedOn(pawn, part);
        return target == null ? label : label + " (" + target.label + ")";
    }

    public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
    {
        SerumRecipeExtension ext = Ext;
        HediffCompProperties_SerumTreatment props = TreatmentProps;
        if (!Usable || Serums.UnderTreatment(pawn) || !Treatable(pawn, part))
        {
            Refund(pawn, billDoer, Serums.UnderTreatment(pawn) ? "ZZRH_RefundBusy" : "ZZRH_RefundNothing");
            return;
        }
        HediffDef target = ChronicTarget(pawn);
        HediffDef treatment = ext.brainTreatment != null && part != null && part.def == ext.brainPart ? ext.brainTreatment : ext.treatment;
        Hediff hediff = HediffMaker.MakeHediff(treatment, pawn);
        HediffComp_SerumTreatment comp = hediff.TryGetComp<HediffComp_SerumTreatment>();
        if (comp == null)
        {
            return;
        }
        comp.part = Serums.NeedsPart(props.kind) ? part : null;
        comp.target = target;
        pawn.health.AddHediff(hediff);
    }

    private void Refund(Pawn pawn, Pawn billDoer, string reasonKey)
    {
        ThingDef serum = SerumDef;
        Map map = billDoer?.MapHeld ?? pawn.MapHeld;
        if (serum == null || map == null)
        {
            return;
        }
        Thing thing = ThingMaker.MakeThing(serum);
        IntVec3 cell = billDoer != null && billDoer.MapHeld == map ? billDoer.PositionHeld : pawn.PositionHeld;
        if (!SerumBills.PutBack(thing, cell, map, billDoer))
        {
            return;
        }
        Messages.Message(reasonKey.Translate(pawn.LabelShort, serum.label, pawn.Named("PAWN")), new LookTargets(cell, map), MessageTypeDefOf.RejectInput, historical: false);
    }
}

public class Recipe_AdministerUsableSerum : Recipe_Surgery
{
    private ThingDef SerumDef => recipe.fixedIngredientFilter?.AnyAllowedDef;

    public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
    {
        if (!base.AvailableOnNow(thing, part) || !(thing is Pawn pawn) || !RimHealthMod.Settings.Enabled(SerumDef) || Serums.IsAndroid(pawn))
        {
            return false;
        }
        CompProperties_UseEffectAddHediff props = SerumDef?.GetCompProperties<CompProperties_UseEffectAddHediff>();
        return props?.hediffDef == null || !pawn.health.hediffSet.HasHediff(props.hediffDef);
    }

    public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
    {
        if (ingredients.Count > 0 && ingredients[0].TryGetComp(out CompUsable comp))
        {
            AcceptanceReport report = comp.CanBeUsedBy(pawn, forced: false, ignoreReserveAndReachable: true);
            if (report.Accepted)
            {
                comp.UsedBy(pawn);
            }
            else
            {
                if (!string.IsNullOrEmpty(report.Reason))
                {
                    comp.SendCannotUseMessage(pawn, report.Reason);
                }
                Map map = billDoer?.MapHeld ?? pawn.MapHeld;
                if (!ingredients[0].Destroyed && !ingredients[0].Spawned && map != null)
                {
                    IntVec3 cell = billDoer != null && billDoer.MapHeld == map ? billDoer.PositionHeld : pawn.PositionHeld;
                    SerumBills.PutBack(ingredients[0], cell, map, billDoer);
                }
            }
        }
    }

    public override void ConsumeIngredient(Thing ingredient, RecipeDef recipe, Map map)
    {
    }
}

public class CompUseEffect_SerumHediff : CompUseEffect_AddHediff
{
    public override AcceptanceReport CanBeUsedBy(Pawn p)
    {
        if (!RimHealthMod.Settings.Enabled(parent.def))
        {
            return "ZZRH_Disabled".Translate();
        }
        if (Serums.IsAndroid(p))
        {
            return "ZZRH_NotForAndroids".Translate();
        }
        return base.CanBeUsedBy(p);
    }
}

public class RecipeWorker_CraftSerum : RecipeWorker
{
    public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
    {
        return SerumBills.CraftAllowed(recipe) && base.AvailableOnNow(thing, part);
    }
}
