using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHealthSerums;

[StaticConstructorOnStartup]
public static class SerumTuning
{
    private class StageBase
    {
        public HediffStage stage;

        public float pain;

        public float vomitMtbDays;

        public List<float> capOffsets = new List<float>();

        public List<float> capFactors = new List<float>();
    }

    private class SpeedBase
    {
        public HediffCompProperties_SeverityPerDay props;

        public FloatRange range;
    }

    private class LifetimeBase
    {
        public HediffCompProperties_Disappears props;

        public IntRange ticks;
    }

    private class SerumBase
    {
        public float price;

        public List<StageBase> stages = new List<StageBase>();

        public List<SpeedBase> speeds = new List<SpeedBase>();

        public List<LifetimeBase> lifetimes = new List<LifetimeBase>();
    }

    private static readonly Dictionary<ThingDef, SerumBase> bases = new Dictionary<ThingDef, SerumBase>();

    static SerumTuning()
    {
        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
        {
            if (def.HasModExtension<SerumExtension>())
            {
                bases[def] = Collect(def);
            }
        }
        Apply();
        AfterEffects.ApplyDurations();
    }

    private static SerumBase Collect(ThingDef def)
    {
        SerumBase result = new SerumBase { price = def.statBases.GetStatValueFromList(StatDefOf.MarketValue, 0f) };
        List<HediffDef> hediffs = new List<HediffDef>();
        foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefs)
        {
            SerumRecipeExtension ext = recipe.GetModExtension<SerumRecipeExtension>();
            if (ext == null || recipe.fixedIngredientFilter?.AnyAllowedDef != def)
            {
                continue;
            }
            hediffs.AddDistinct(ext.treatment);
            hediffs.AddDistinct(ext.brainTreatment);
        }
        hediffs.AddDistinct(def.GetCompProperties<CompProperties_UseEffectAddHediff>()?.hediffDef);
        foreach (HediffDef hediff in hediffs)
        {
            if (hediff == null)
            {
                continue;
            }
            if (hediff.stages != null)
            {
                foreach (HediffStage stage in hediff.stages)
                {
                    StageBase stageBase = new StageBase { stage = stage, pain = stage.painOffset, vomitMtbDays = stage.vomitMtbDays };
                    if (stage.capMods != null)
                    {
                        foreach (PawnCapacityModifier mod in stage.capMods)
                        {
                            stageBase.capOffsets.Add(mod.offset);
                            stageBase.capFactors.Add(mod.postFactor);
                        }
                    }
                    result.stages.Add(stageBase);
                }
            }
            if (hediff.comps != null)
            {
                foreach (HediffCompProperties comp in hediff.comps)
                {
                    if (comp is HediffCompProperties_SeverityPerDay speed)
                    {
                        result.speeds.Add(new SpeedBase { props = speed, range = speed.severityPerDayRange });
                    }
                    else if (comp is HediffCompProperties_Disappears disappears)
                    {
                        result.lifetimes.Add(new LifetimeBase { props = disappears, ticks = disappears.disappearsAfterTicks });
                    }
                }
            }
        }
        return result;
    }

    public static void Apply()
    {
        foreach (KeyValuePair<ThingDef, SerumBase> pair in bases)
        {
            SerumConfig config = RimHealthMod.Settings.Config(pair.Key);
            SerumBase b = pair.Value;
            pair.Key.SetStatBaseValue(StatDefOf.MarketValue, Mathf.Max(1f, Mathf.Round(b.price * config.price)));
            foreach (StageBase s in b.stages)
            {
                s.stage.painOffset = s.pain * config.pain;
                s.stage.vomitMtbDays = s.vomitMtbDays <= 0f ? s.vomitMtbDays : config.sideEffects <= 0f ? -1f : s.vomitMtbDays / config.sideEffects;
                for (int i = 0; i < s.capOffsets.Count && i < s.stage.capMods.Count; i++)
                {
                    if (s.capOffsets[i] < 0f)
                    {
                        s.stage.capMods[i].offset = s.capOffsets[i] * config.sideEffects;
                    }
                    if (s.capFactors[i] < 1f)
                    {
                        s.stage.capMods[i].postFactor = Mathf.Clamp01(1f - (1f - s.capFactors[i]) * config.sideEffects);
                    }
                }
            }
            foreach (SpeedBase s in b.speeds)
            {
                s.props.severityPerDayRange = new FloatRange(s.range.min / config.duration, s.range.max / config.duration);
            }
            foreach (LifetimeBase l in b.lifetimes)
            {
                l.props.disappearsAfterTicks = new IntRange(Mathf.RoundToInt(l.ticks.min * config.duration), Mathf.RoundToInt(l.ticks.max * config.duration));
            }
        }
    }

    public static void RefreshPawns()
    {
        if (Current.Game == null)
        {
            return;
        }
        HashSet<HediffDef> tuned = new HashSet<HediffDef>();
        foreach (ThingDef serum in bases.Keys)
        {
            foreach (HediffDef hediff in DefDatabase<HediffDef>.AllDefs)
            {
                HediffCompProperties_SerumTreatment p = hediff.CompProps<HediffCompProperties_SerumTreatment>();
                if (p != null && p.serum == serum)
                {
                    tuned.Add(hediff);
                }
            }
            HediffDef boost = serum.GetCompProperties<CompProperties_UseEffectAddHediff>()?.hediffDef;
            if (boost != null)
            {
                tuned.Add(boost);
            }
        }
        foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive)
        {
            List<Hediff> hediffs = pawn.health?.hediffSet?.hediffs;
            if (hediffs != null && hediffs.Exists(h => tuned.Contains(h.def)))
            {
                pawn.health.hediffSet.DirtyCache();
            }
        }
    }

    public static float BasePrice(ThingDef def)
    {
        return bases.TryGetValue(def, out SerumBase b) ? b.price : 0f;
    }

    public static float BasePain(ThingDef def)
    {
        float max = 0f;
        if (bases.TryGetValue(def, out SerumBase b))
        {
            foreach (StageBase s in b.stages)
            {
                max = Mathf.Max(max, s.pain);
            }
        }
        return max;
    }

    public static bool HasSideEffects(ThingDef def)
    {
        if (!bases.TryGetValue(def, out SerumBase b))
        {
            return false;
        }
        foreach (StageBase s in b.stages)
        {
            if (s.vomitMtbDays > 0f || s.capOffsets.Exists(o => o < 0f) || s.capFactors.Exists(f => f < 1f))
            {
                return true;
            }
        }
        return false;
    }

    public static bool IsLasting(ThingDef def)
    {
        return bases.TryGetValue(def, out SerumBase b) && b.speeds.Count == 0 && b.lifetimes.Count > 0;
    }

    public static string DurationText(ThingDef def, float factor)
    {
        if (!bases.TryGetValue(def, out SerumBase b))
        {
            return "";
        }
        float min, max;
        if (b.speeds.Count > 0)
        {
            FloatRange r = b.speeds[0].range;
            min = factor / r.max;
            max = factor / r.min;
        }
        else if (b.lifetimes.Count > 0)
        {
            min = b.lifetimes[0].ticks.min * factor / 60000f;
            max = b.lifetimes[0].ticks.max * factor / 60000f;
        }
        else
        {
            return "";
        }
        string a = min.ToString("0.#");
        string c = max.ToString("0.#");
        return a == c ? "ZZRH_DaysSingle".Translate(a) : "ZZRH_Days".Translate(a, c);
    }
}
