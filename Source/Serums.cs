using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimHealthSerums;

public enum TreatmentKind
{
    Regrow,
    HealScars,
    CureHediffs,
    CureAddictions,
    CureChronic,
    Rejuvenate
}

public class SerumExtension : DefModExtension
{
    public float shipChance;

    public float caravanChance;

    public float prosthesesChance;

    [MustTranslate]
    public string summary;

    public float aftereffectChance = 0.2f;
}

public class SerumRecipeExtension : DefModExtension
{
    public HediffDef treatment;

    public HediffDef brainTreatment;

    public BodyPartDef brainPart;

    public List<BodyPartDef> excludedParts;
}

public static class Serums
{
    public static bool UnderTreatment(Pawn pawn)
    {
        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            if (hediffs[i] is HediffWithComps withComps && withComps.TryGetComp<HediffComp_SerumTreatment>() != null)
            {
                return true;
            }
        }
        return false;
    }

    private static GeneDef syntheticBody;

    private static bool syntheticBodyLooked;

    public static bool IsAndroid(Pawn pawn)
    {
        if (!syntheticBodyLooked)
        {
            syntheticBody = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_SyntheticBody");
            syntheticBodyLooked = true;
        }
        if (syntheticBody == null)
        {
            return false;
        }
        List<Gene> genes = pawn.genes?.GenesListForReading;
        if (genes == null)
        {
            return false;
        }
        for (int i = 0; i < genes.Count; i++)
        {
            if (genes[i].def == syntheticBody)
            {
                return true;
            }
        }
        return false;
    }

    public static bool NeedsPart(TreatmentKind kind)
    {
        return kind == TreatmentKind.Regrow || kind == TreatmentKind.HealScars;
    }

    public static bool CanTreat(Pawn pawn, HediffCompProperties_SerumTreatment props, BodyPartRecord part, HediffDef target)
    {
        HediffSet hediffSet = pawn.health.hediffSet;
        switch (props.kind)
        {
            case TreatmentKind.Regrow:
                return part != null && hediffSet.PartIsMissing(part) && !AncestorMissing(hediffSet, part) && !hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(part);
            case TreatmentKind.HealScars:
                return part != null && (HasScars(hediffSet, part) || HasOnPart(hediffSet, part, props.cures)) && !hediffSet.PartIsMissing(part) && !AncestorMissing(hediffSet, part);
            case TreatmentKind.CureHediffs:
                return HasAny(hediffSet, props.cures);
            case TreatmentKind.CureAddictions:
                return HasCurableAddiction(pawn, props);
            case TreatmentKind.CureChronic:
                return target != null && hediffSet.HasHediff(target);
            case TreatmentKind.Rejuvenate:
                return CanRejuvenate(pawn, props);
            default:
                return false;
        }
    }

    public static bool AncestorMissing(HediffSet hediffSet, BodyPartRecord part)
    {
        for (BodyPartRecord parent = part.parent; parent != null; parent = parent.parent)
        {
            if (hediffSet.PartIsMissing(parent))
            {
                return true;
            }
        }
        return false;
    }

    public static bool IsScar(Hediff hediff)
    {
        return hediff is Hediff_Injury && hediff.IsPermanent() && hediff.Visible && hediff.def.everCurableByItem;
    }

    public static bool HasOnPart(HediffSet hediffSet, BodyPartRecord part, List<HediffDef> defs)
    {
        if (defs == null)
        {
            return false;
        }
        List<Hediff> hediffs = hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            if (hediffs[i].Part == part && defs.Contains(hediffs[i].def))
            {
                return true;
            }
        }
        return false;
    }

    public static bool HasScars(HediffSet hediffSet, BodyPartRecord part)
    {
        List<Hediff> hediffs = hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            if (hediffs[i].Part == part && IsScar(hediffs[i]))
            {
                return true;
            }
        }
        return false;
    }

    public static bool HasAny(HediffSet hediffSet, List<HediffDef> defs)
    {
        if (defs == null)
        {
            return false;
        }
        for (int i = 0; i < defs.Count; i++)
        {
            if (hediffSet.HasHediff(defs[i]))
            {
                return true;
            }
        }
        return false;
    }

    public static HediffDef FirstPresent(HediffSet hediffSet, List<HediffDef> defs)
    {
        if (defs == null)
        {
            return null;
        }
        for (int i = 0; i < defs.Count; i++)
        {
            if (hediffSet.HasHediff(defs[i]))
            {
                return defs[i];
            }
        }
        return null;
    }

    public static bool IsCurableAddiction(Hediff hediff, HediffCompProperties_SerumTreatment props)
    {
        if (!(hediff is Hediff_Addiction addiction))
        {
            return false;
        }
        if (!addiction.Visible || !addiction.def.everCurableByItem)
        {
            return false;
        }
        ChemicalDef chemical = addiction.Chemical;
        return chemical == null || props.excludedChemicals == null || !props.excludedChemicals.Contains(chemical);
    }

    public static bool HasCurableAddiction(Pawn pawn, HediffCompProperties_SerumTreatment props)
    {
        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            if (IsCurableAddiction(hediffs[i], props))
            {
                return true;
            }
        }
        return false;
    }

    private static Dictionary<ThingDef, HediffCompProperties_SerumTreatment> treatmentProps;

    public static HediffCompProperties_SerumTreatment TreatmentPropsFor(ThingDef serum)
    {
        if (treatmentProps == null)
        {
            treatmentProps = new Dictionary<ThingDef, HediffCompProperties_SerumTreatment>();
            foreach (HediffDef hediff in DefDatabase<HediffDef>.AllDefs)
            {
                HediffCompProperties_SerumTreatment props = hediff.CompProps<HediffCompProperties_SerumTreatment>();
                if (props?.serum != null && !treatmentProps.ContainsKey(props.serum))
                {
                    treatmentProps[props.serum] = props;
                }
            }
        }
        return serum != null && treatmentProps.TryGetValue(serum, out HediffCompProperties_SerumTreatment result) ? result : null;
    }

    public static bool CanRejuvenate(Pawn pawn, HediffCompProperties_SerumTreatment props)
    {
        Pawn_AgeTracker age = pawn.ageTracker;
        return age.Adult && age.AgeBiologicalTicks > RejuvenationFloor(pawn, props) + 3600000L;
    }

    public static long RejuvenationFloor(Pawn pawn, HediffCompProperties_SerumTreatment props)
    {
        float minAge = props.serum != null ? RimHealthMod.Settings.Config(props.serum).minAge : 18f;
        return (long)(3600000f * UnityEngine.Mathf.Max(pawn.ageTracker.AdultMinAge, minAge));
    }

    public static void RemoveAll(Pawn pawn, System.Predicate<Hediff> match)
    {
        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = hediffs.Count - 1; i >= 0; i--)
        {
            if (i < hediffs.Count && match(hediffs[i]))
            {
                pawn.health.RemoveHediff(hediffs[i]);
            }
        }
    }

    public static string Finish(Pawn pawn, HediffCompProperties_SerumTreatment props, BodyPartRecord part, HediffDef target)
    {
        switch (props.kind)
        {
            case TreatmentKind.Regrow:
                pawn.health.RestorePart(part);
                return part.Label;
            case TreatmentKind.HealScars:
                RemoveAll(pawn, h => h.Part == part && (IsScar(h) || (props.cures != null && props.cures.Contains(h.def))));
                return part.Label;
            case TreatmentKind.CureHediffs:
                RemoveAll(pawn, h => props.cures.Contains(h.def));
                return null;
            case TreatmentKind.CureAddictions:
            {
                List<HediffDef> tolerances = new List<HediffDef>();
                foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                {
                    if (IsCurableAddiction(hediff, props) && ((Hediff_Addiction)hediff).Chemical?.toleranceHediff != null)
                    {
                        tolerances.Add(((Hediff_Addiction)hediff).Chemical.toleranceHediff);
                    }
                }
                RemoveAll(pawn, h => IsCurableAddiction(h, props) || tolerances.Contains(h.def));
                return null;
            }
            case TreatmentKind.CureChronic:
                RemoveAll(pawn, h => h.def == target);
                return target.labelNoun.NullOrEmpty() ? target.label : target.labelNoun;
            case TreatmentKind.Rejuvenate:
            {
                Pawn_AgeTracker age = pawn.ageTracker;
                long before = age.AgeBiologicalTicks;
                FloatRange range = props.serum != null ? RimHealthMod.Settings.Config(props.serum).Years : props.yearsRange;
                long reduce = (long)(3600000f * range.RandomInRange);
                age.AgeBiologicalTicks = System.Math.Max(RejuvenationFloor(pawn, props), before - reduce);
                age.ResetAgeReversalDemand(Pawn_AgeTracker.AgeReversalReason.ViaTreatment);
                float years = (before - age.AgeBiologicalTicks) / 3600000f;
                string text = years.ToString("0.#");
                return text == "1" ? "ZZRH_OneYear".Translate().ToString() : "ZZRH_Years".Translate(text).ToString();
            }
            default:
                return null;
        }
    }
}
