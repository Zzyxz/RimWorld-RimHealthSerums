using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimHealthSerums;

public class SerumConfig : IExposable
{
    public bool enabled = true;

    public float ship;

    public float caravan;

    public float prostheses;

    public float price = 1f;

    public float duration = 1f;

    public float pain = 1f;

    public float sideEffects = 1f;

    public float aftereffects = 0.2f;

    public float yearsMin = -1f;

    public float yearsMax = -1f;

    public float minAge = -1f;

    public FloatRange Years => new FloatRange(yearsMin, yearsMax);

    public static SerumConfig Defaults(ThingDef def)
    {
        SerumExtension ext = def.GetModExtension<SerumExtension>();
        return new SerumConfig
        {
            ship = ext?.shipChance ?? 0f,
            caravan = ext?.caravanChance ?? 0f,
            prostheses = ext?.prosthesesChance ?? 0f,
            aftereffects = ext?.aftereffectChance ?? 0.2f,
            yearsMin = Serums.TreatmentPropsFor(def)?.yearsRange.min ?? 1f,
            yearsMax = Serums.TreatmentPropsFor(def)?.yearsRange.max ?? 3f,
            minAge = 18f
        };
    }

    public void FillMissing(ThingDef def)
    {
        SerumConfig d = Defaults(def);
        if (ship < 0f) ship = d.ship;
        if (caravan < 0f) caravan = d.caravan;
        if (prostheses < 0f) prostheses = d.prostheses;
        if (aftereffects < 0f) aftereffects = d.aftereffects;
        if (yearsMin < 0f) yearsMin = d.yearsMin;
        if (yearsMax < 0f) yearsMax = d.yearsMax;
        if (minAge < 0f) minAge = d.minAge;
    }

    public bool SameAs(SerumConfig o)
    {
        return enabled == o.enabled && Mathf.Approximately(ship, o.ship) && Mathf.Approximately(caravan, o.caravan) && Mathf.Approximately(prostheses, o.prostheses)
            && Mathf.Approximately(price, o.price) && Mathf.Approximately(duration, o.duration) && Mathf.Approximately(pain, o.pain)
            && Mathf.Approximately(sideEffects, o.sideEffects) && Mathf.Approximately(aftereffects, o.aftereffects)
            && Mathf.Approximately(yearsMin, o.yearsMin) && Mathf.Approximately(yearsMax, o.yearsMax) && Mathf.Approximately(minAge, o.minAge);
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref enabled, "enabled", true);
        Scribe_Values.Look(ref ship, "ship", -1f);
        Scribe_Values.Look(ref caravan, "caravan", -1f);
        Scribe_Values.Look(ref prostheses, "prostheses", -1f);
        Scribe_Values.Look(ref price, "price", 1f);
        Scribe_Values.Look(ref duration, "duration", 1f);
        Scribe_Values.Look(ref pain, "pain", 1f);
        Scribe_Values.Look(ref sideEffects, "sideEffects", 1f);
        Scribe_Values.Look(ref aftereffects, "aftereffects", -1f);
        Scribe_Values.Look(ref yearsMin, "yearsMin", -1f);
        Scribe_Values.Look(ref yearsMax, "yearsMax", -1f);
        Scribe_Values.Look(ref minAge, "minAge", -1f);
    }
}

public class SideEffectConfig : IExposable
{
    public bool enabled = true;

    public float minDays = -1f;

    public float maxDays = -1f;

    public FloatRange Days => new FloatRange(minDays, maxDays);

    public void ExposeData()
    {
        Scribe_Values.Look(ref enabled, "enabled", true);
        Scribe_Values.Look(ref minDays, "minDays", -1f);
        Scribe_Values.Look(ref maxDays, "maxDays", -1f);
    }
}

public class RimHealthSettings : ModSettings
{
    public bool allowCrafting;

    public bool devFastTreatments;

    private Dictionary<string, SerumConfig> configs = new Dictionary<string, SerumConfig>();

    private Dictionary<string, SideEffectConfig> sideEffects = new Dictionary<string, SideEffectConfig>();

    public SideEffectConfig SideEffect(string key)
    {
        if (!sideEffects.TryGetValue(key, out SideEffectConfig config))
        {
            config = new SideEffectConfig();
            sideEffects[key] = config;
        }
        if (config.minDays < 0f || config.maxDays < 0f)
        {
            FloatRange d = AfterEffects.DefaultDays(key);
            if (config.minDays < 0f) config.minDays = d.min;
            if (config.maxDays < 0f) config.maxDays = d.max;
        }
        return config;
    }

    public void ResetSideEffects()
    {
        sideEffects.Clear();
    }

    public SerumConfig Config(ThingDef def)
    {
        if (!configs.TryGetValue(def.defName, out SerumConfig config))
        {
            config = SerumConfig.Defaults(def);
            configs[def.defName] = config;
        }
        config.FillMissing(def);
        return config;
    }

    public void ResetSerum(ThingDef def)
    {
        configs.Remove(def.defName);
    }

    public bool Enabled(ThingDef def)
    {
        return def == null || Config(def).enabled;
    }

    public float Chance(ThingDef def, SerumTrader trader)
    {
        SerumConfig config = Config(def);
        if (!config.enabled)
        {
            return 0f;
        }
        switch (trader)
        {
            case SerumTrader.Caravan:
                return config.caravan;
            case SerumTrader.Prostheses:
                return config.prostheses;
            default:
                return config.ship;
        }
    }

    public void Reset()
    {
        allowCrafting = false;
        devFastTreatments = false;
        configs.Clear();
        sideEffects.Clear();
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref allowCrafting, "allowCrafting", false);
        Scribe_Values.Look(ref devFastTreatments, "devFastTreatments", false);
        Dictionary<string, SerumConfig> changed = configs;
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            changed = new Dictionary<string, SerumConfig>();
            foreach (KeyValuePair<string, SerumConfig> pair in configs)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(pair.Key);
                if (def == null || !pair.Value.SameAs(SerumConfig.Defaults(def)))
                {
                    changed[pair.Key] = pair.Value;
                }
            }
        }
        Scribe_Collections.Look(ref changed, "serums", LookMode.Value, LookMode.Deep);
        if (Scribe.mode != LoadSaveMode.Saving)
        {
            configs = changed ?? new Dictionary<string, SerumConfig>();
        }
        Dictionary<string, SideEffectConfig> changedEffects = sideEffects;
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            changedEffects = new Dictionary<string, SideEffectConfig>();
            foreach (KeyValuePair<string, SideEffectConfig> pair in sideEffects)
            {
                FloatRange d = AfterEffects.DefaultDays(pair.Key);
                if (!pair.Value.enabled || !Mathf.Approximately(pair.Value.minDays, d.min) || !Mathf.Approximately(pair.Value.maxDays, d.max))
                {
                    changedEffects[pair.Key] = pair.Value;
                }
            }
        }
        Scribe_Collections.Look(ref changedEffects, "sideEffects", LookMode.Value, LookMode.Deep);
        if (Scribe.mode != LoadSaveMode.Saving)
        {
            sideEffects = changedEffects ?? new Dictionary<string, SideEffectConfig>();
        }
    }
}

public class RimHealthMod : Mod
{
    public static RimHealthSettings Settings;

    private readonly HashSet<string> expanded = new HashSet<string>();

    private List<ThingDef> serumDefs;

    private readonly Dictionary<ThingDef, string> effectLists = new Dictionary<ThingDef, string>();

    private Vector2 scroll;

    private float viewHeight = 1200f;

    public RimHealthMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<RimHealthSettings>();
    }

    public override string SettingsCategory()
    {
        return Content.Name;
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
        SerumTuning.Apply();
        AfterEffects.ApplyDurations();
        SerumTuning.RefreshPawns();
        SerumBills.SuspendUnavailable();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        serumDefs ??= DefDatabase<ThingDef>.AllDefs.Where(d => d.HasModExtension<SerumExtension>()).ToList();
        Rect view = new Rect(0f, 0f, inRect.width - 20f, viewHeight);
        Widgets.BeginScrollView(inRect, ref scroll, view);
        Listing_Standard listing = new Listing_Standard { maxOneColumn = true };
        listing.Begin(view);
        listing.CheckboxLabeled("ZZRH_AllowCrafting".Translate(), ref Settings.allowCrafting, "ZZRH_AllowCraftingTip".Translate());
        if (Prefs.DevMode)
        {
            listing.CheckboxLabeled("ZZRH_DevFast".Translate(), ref Settings.devFastTreatments, "ZZRH_DevFastTip".Translate());
        }
        listing.Label("ZZRH_StockChanceIntro".Translate());
        if (listing.ButtonText("ZZRH_ResetDefaults".Translate()))
        {
            Settings.Reset();
        }
        DrawSideEffects(listing);
        foreach (ThingDef def in serumDefs)
        {
            DrawSerum(listing, def);
        }
        viewHeight = listing.CurHeight + 20f;
        listing.End();
        Widgets.EndScrollView();
    }

    private bool sideEffectsOpen;

    private void DrawSideEffects(Listing_Standard listing)
    {
        listing.GapLine();
        Rect row = listing.GetRect(Text.LineHeight + 4f);
        Widgets.Label(row.LeftPart(0.72f), "ZZRH_SideEffectsHeader".Translate());
        if (Widgets.ButtonText(row.RightPart(0.25f), sideEffectsOpen ? "ZZRH_HideSettings".Translate() : "ZZRH_ShowSettings".Translate()))
        {
            sideEffectsOpen = !sideEffectsOpen;
        }
        GUI.color = Color.gray;
        listing.Label("    " + "ZZRH_SideEffectsIntro".Translate());
        GUI.color = Color.white;
        if (!sideEffectsOpen)
        {
            return;
        }
        foreach (AfterEffect effect in AfterEffects.All)
        {
            SideEffectConfig config = Settings.SideEffect(effect.Key);
            listing.CheckboxLabeled("    " + effect.Label.CapitalizeFirst(), ref config.enabled, "ZZRH_SideEffectEnabledTip".Translate());
            if (!config.enabled)
            {
                continue;
            }
            string minText = config.minDays.ToString("0.#");
            string maxText = config.maxDays.ToString("0.#");
            listing.Label("        " + (minText == maxText ? "ZZRH_SideEffectDaysSingle".Translate(minText) : "ZZRH_SideEffectDays".Translate(minText, maxText)));
            FloatRange days = config.Days;
            Rect rangeRect = listing.GetRect(28f);
            rangeRect.xMin += 36f;
            Widgets.FloatRange(rangeRect, effect.Key.GetHashCode(), ref days, 0.5f, 20f);
            config.minDays = Mathf.Clamp(Mathf.Round(days.min * 2f) / 2f, 0.5f, 20f);
            config.maxDays = Mathf.Clamp(Mathf.Round(days.max * 2f) / 2f, config.minDays, 20f);
        }
        if (listing.ButtonText("ZZRH_ResetSideEffects".Translate()))
        {
            Settings.ResetSideEffects();
        }
    }

    private void DrawSerum(Listing_Standard listing, ThingDef def)
    {
        SerumConfig config = Settings.Config(def);
        listing.GapLine();
        Rect row = listing.GetRect(Text.LineHeight + 4f);
        Rect check = row.LeftPart(0.72f);
        Widgets.CheckboxLabeled(check, def.LabelCap, ref config.enabled);
        TooltipHandler.TipRegion(check, "ZZRH_EnabledTip".Translate());
        bool open = expanded.Contains(def.defName);
        if (config.enabled && Widgets.ButtonText(row.RightPart(0.25f), open ? "ZZRH_HideSettings".Translate() : "ZZRH_ShowSettings".Translate()))
        {
            if (open)
            {
                expanded.Remove(def.defName);
            }
            else
            {
                expanded.Add(def.defName);
            }
        }
        GUI.color = Color.gray;
        string summary = def.GetModExtension<SerumExtension>().summary;
        if (!summary.NullOrEmpty())
        {
            listing.Label("    " + summary);
        }
        if (!config.enabled)
        {
            listing.Label("    " + "ZZRH_SwitchedOff".Translate());
        }
        GUI.color = Color.white;
        if (!config.enabled || !open)
        {
            return;
        }
        Slider(listing, "ZZRH_ShipChance".Translate(config.ship.ToStringPercent()), null, ref config.ship, 0f, 1f, 0.05f);
        Slider(listing, "ZZRH_CaravanChance".Translate(config.caravan.ToStringPercent()), null, ref config.caravan, 0f, 1f, 0.05f);
        Slider(listing, "ZZRH_ProsthesesChance".Translate(config.prostheses.ToStringPercent()), null, ref config.prostheses, 0f, 1f, 0.05f);
        string price = Mathf.Max(1f, Mathf.Round(SerumTuning.BasePrice(def) * config.price)).ToString("0");
        Slider(listing, "ZZRH_Price".Translate(price, config.price.ToStringPercent()), null, ref config.price, 0.25f, 3f, 0.05f);
        string durationKey = SerumTuning.IsLasting(def) ? "ZZRH_EffectDuration" : "ZZRH_Duration";
        Slider(listing, durationKey.Translate(SerumTuning.DurationText(def, config.duration), config.duration.ToStringPercent()), "ZZRH_DurationTip".Translate(), ref config.duration, 0.5f, 2f, 0.1f);
        float basePain = SerumTuning.BasePain(def);
        if (basePain > 0f)
        {
            Slider(listing, "ZZRH_Pain".Translate((basePain * config.pain).ToStringPercent(), config.pain.ToStringPercent()), "ZZRH_PainTip".Translate(), ref config.pain, 0f, 2f, 0.1f);
        }
        if (SerumTuning.HasSideEffects(def))
        {
            Slider(listing, "ZZRH_SideEffects".Translate(config.sideEffects.ToStringPercent()), "ZZRH_SideEffectsTip".Translate(), ref config.sideEffects, 0f, 2f, 0.1f);
        }
        if (!effectLists.TryGetValue(def, out string list))
        {
            list = string.Join(", ", AfterEffects.For(def).Select(e => e.Label));
            effectLists[def] = list;
        }
        if (!list.NullOrEmpty())
        {
            Slider(listing, "ZZRH_AfterEffectChance".Translate(config.aftereffects.ToStringPercent()), "ZZRH_AfterEffectChanceTip".Translate(list), ref config.aftereffects, 0f, 1f, 0.05f);
        }
        if (Serums.TreatmentPropsFor(def)?.kind == TreatmentKind.Rejuvenate)
        {
            listing.Label("    " + "ZZRH_YearsRange".Translate(config.yearsMin.ToString("0.#"), config.yearsMax.ToString("0.#")));
            FloatRange years = config.Years;
            Rect rangeRect = listing.GetRect(28f);
            rangeRect.xMin += 24f;
            Widgets.FloatRange(rangeRect, def.shortHash + 7313, ref years, 0.5f, 10f);
            config.yearsMin = Mathf.Clamp(Mathf.Round(years.min * 2f) / 2f, 0.5f, 10f);
            config.yearsMax = Mathf.Clamp(Mathf.Round(years.max * 2f) / 2f, config.yearsMin, 10f);
            Slider(listing, "ZZRH_MinAge".Translate(config.minAge.ToString("0")), "ZZRH_MinAgeTip".Translate(), ref config.minAge, 18f, 80f, 1f);
        }
        if (listing.ButtonText("ZZRH_ResetSerum".Translate(def.label)))
        {
            Settings.ResetSerum(def);
        }
    }

    private static void Slider(Listing_Standard listing, string label, string tip, ref float value, float min, float max, float step)
    {
        Rect labelRect = listing.Label("    " + label);
        if (!tip.NullOrEmpty())
        {
            TooltipHandler.TipRegion(labelRect, tip);
        }
        value = Mathf.Clamp(Mathf.Round(listing.Slider(value, min, max) / step) * step, min, max);
    }
}
