using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHealthSerums;

public class AfterEffect
{
    public HediffDef hediff;

    public List<ThoughtDef> thoughts;

    public float weight = 1f;

    public string Label => hediff?.label ?? thoughts?.FirstOrDefault()?.stages?.FirstOrDefault()?.label ?? "";

    public string Key => hediff?.defName ?? thoughts?.FirstOrDefault()?.defName ?? "";
}

public static class AfterEffects
{
    private const float DefaultThoughtMinDays = 1f;

    private const float DefaultThoughtMaxDays = 2.5f;

    private static List<AfterEffect> all;

    private static readonly Dictionary<string, FloatRange> defaultDays = new Dictionary<string, FloatRange>();

    public static List<AfterEffect> All
    {
        get
        {
            if (all == null)
            {
                all = new List<AfterEffect>();
                foreach (HediffDef hediff in DefDatabase<HediffDef>.AllDefs)
                {
                    List<AfterEffect> effects = hediff.CompProps<HediffCompProperties_SerumTreatment>()?.aftereffects;
                    if (effects == null)
                    {
                        continue;
                    }
                    foreach (AfterEffect effect in effects)
                    {
                        if (!all.Any(e => e.Key == effect.Key))
                        {
                            all.Add(effect);
                            defaultDays[effect.Key] = ReadDays(effect);
                        }
                    }
                }
            }
            return all;
        }
    }

    private static FloatRange ReadDays(AfterEffect effect)
    {
        HediffCompProperties_Disappears disappears = effect.hediff?.CompProps<HediffCompProperties_Disappears>();
        if (disappears != null)
        {
            return new FloatRange(disappears.disappearsAfterTicks.min / 60000f, disappears.disappearsAfterTicks.max / 60000f);
        }
        return new FloatRange(DefaultThoughtMinDays, DefaultThoughtMaxDays);
    }

    public static FloatRange DefaultDays(string key)
    {
        _ = All;
        return defaultDays.TryGetValue(key, out FloatRange days) ? days : new FloatRange(DefaultThoughtMinDays, DefaultThoughtMaxDays);
    }

    public static void ApplyDurations()
    {
        foreach (AfterEffect effect in All)
        {
            HediffCompProperties_Disappears disappears = effect.hediff?.CompProps<HediffCompProperties_Disappears>();
            if (disappears == null)
            {
                continue;
            }
            FloatRange days = RimHealthMod.Settings.SideEffect(effect.Key).Days;
            disappears.disappearsAfterTicks = new IntRange(Mathf.RoundToInt(days.min * 60000f), Mathf.RoundToInt(days.max * 60000f));
        }
    }

    public static void Roll(Pawn pawn, HediffCompProperties_SerumTreatment props)
    {
        if (pawn.Dead || props.serum == null || props.aftereffects.NullOrEmpty())
        {
            return;
        }
        if (!Rand.Chance(RimHealthMod.Settings.Config(props.serum).aftereffects))
        {
            return;
        }
        bool hasMood = pawn.needs?.mood?.thoughts?.memories != null;
        List<AfterEffect> options = props.aftereffects.Where(e => RimHealthMod.Settings.SideEffect(e.Key).enabled && (e.hediff != null || (hasMood && !e.thoughts.NullOrEmpty()))).ToList();
        if (options.Count == 0)
        {
            return;
        }
        AfterEffect effect = options.RandomElementByWeight(e => e.weight);
        if (effect.hediff != null)
        {
            pawn.health.AddHediff(effect.hediff);
        }
        else if (!effect.thoughts.NullOrEmpty() && pawn.needs?.mood?.thoughts?.memories != null)
        {
            MemoryThoughtHandler memories = pawn.needs.mood.thoughts.memories;
            foreach (ThoughtDef def in effect.thoughts)
            {
                memories.RemoveMemoriesOfDef(def);
            }
            FloatRange days = RimHealthMod.Settings.SideEffect(effect.Key).Days;
            Thought_Memory memory = (Thought_Memory)ThoughtMaker.MakeThought(effect.thoughts.RandomElement());
            memory.durationTicksOverride = Mathf.RoundToInt(days.RandomInRange * 60000f);
            memories.TryGainMemory(memory);
            if (!memories.Memories.Contains(memory))
            {
                return;
            }
        }
        else
        {
            return;
        }
        if (PawnUtility.ShouldSendNotificationAbout(pawn))
        {
            Messages.Message("ZZRH_AfterEffectMessage".Translate(pawn.LabelShort, effect.Label, props.serum.label, pawn.Named("PAWN")), pawn, MessageTypeDefOf.NeutralEvent);
        }
    }

    public static List<AfterEffect> For(ThingDef serum)
    {
        List<AfterEffect> result = new List<AfterEffect>();
        foreach (HediffDef hediff in DefDatabase<HediffDef>.AllDefs)
        {
            HediffCompProperties_SerumTreatment props = hediff.CompProps<HediffCompProperties_SerumTreatment>();
            if (props?.serum != serum || props.aftereffects == null)
            {
                continue;
            }
            foreach (AfterEffect effect in props.aftereffects)
            {
                if (!result.Any(e => e.Key == effect.Key))
                {
                    result.Add(effect);
                }
            }
        }
        return result;
    }
}
