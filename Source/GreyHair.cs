using RimWorld;
using UnityEngine;
using Verse;

namespace RimHealthSerums;

public class HediffCompProperties_GreyHair : HediffCompProperties
{
    public Color grey = new Color(0.78f, 0.78f, 0.76f);

    public float holdFraction = 0.2f;

    public int updateInterval = 15000;

    public HediffCompProperties_GreyHair()
    {
        compClass = typeof(HediffComp_GreyHair);
    }
}

public class HediffComp_GreyHair : HediffComp
{
    private Color original;

    private Color lastSet;

    private bool tracking;

    private int ticksSinceUpdate;

    private HediffCompProperties_GreyHair Props => (HediffCompProperties_GreyHair)props;

    public override void CompPostPostAdd(DamageInfo? dinfo)
    {
        base.CompPostPostAdd(dinfo);
        if (Pawn.story == null)
        {
            return;
        }
        original = Pawn.story.HairColor;
        tracking = true;
        SetColor(Props.grey);
    }

    public override void CompPostMerged(Hediff other)
    {
        base.CompPostMerged(other);
        if (Pawn.story == null)
        {
            return;
        }
        if (!tracking)
        {
            original = Pawn.story.HairColor;
            tracking = true;
        }
        HediffComp_Disappears timer = parent.TryGetComp<HediffComp_Disappears>();
        if (timer != null)
        {
            timer.disappearsAfterTicks = timer.ticksToDisappear;
        }
        SetColor(Props.grey);
    }

    public override void CompPostTickInterval(ref float severityAdjustment, int delta)
    {
        if (!tracking)
        {
            return;
        }
        ticksSinceUpdate += delta;
        if (ticksSinceUpdate < Props.updateInterval)
        {
            return;
        }
        ticksSinceUpdate = 0;
        if (!StillOurs())
        {
            tracking = false;
            return;
        }
        HediffComp_Disappears timer = parent.TryGetComp<HediffComp_Disappears>();
        if (timer == null || timer.disappearsAfterTicks <= 0)
        {
            return;
        }
        float progress = 1f - (float)timer.ticksToDisappear / timer.disappearsAfterTicks;
        float t = Mathf.Clamp01((progress - Props.holdFraction) / (1f - Props.holdFraction));
        SetColor(Color.Lerp(Props.grey, original, t));
    }

    public override void CompPostPostRemoved()
    {
        base.CompPostPostRemoved();
        if (tracking && !Pawn.Dead && StillOurs())
        {
            SetColor(original);
        }
        tracking = false;
    }

    private bool StillOurs()
    {
        if (Pawn.story == null)
        {
            return false;
        }
        Color current = Pawn.story.HairColor;
        return Mathf.Abs(current.r - lastSet.r) < 0.01f && Mathf.Abs(current.g - lastSet.g) < 0.01f && Mathf.Abs(current.b - lastSet.b) < 0.01f;
    }

    private void SetColor(Color color)
    {
        if (tracking && StillOurs() && Mathf.Abs(color.r - lastSet.r) < 0.004f && Mathf.Abs(color.g - lastSet.g) < 0.004f && Mathf.Abs(color.b - lastSet.b) < 0.004f)
        {
            return;
        }
        Pawn.story.HairColor = color;
        lastSet = Pawn.story.HairColor;
        Pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        PortraitsCache.SetDirty(Pawn);
    }

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref original, "original");
        Scribe_Values.Look(ref lastSet, "lastSet");
        Scribe_Values.Look(ref tracking, "tracking", false);
        Scribe_Values.Look(ref ticksSinceUpdate, "ticksSinceUpdate", 0);
    }
}
