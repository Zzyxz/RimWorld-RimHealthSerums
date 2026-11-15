using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimHealthSerums;

public enum SerumTrader
{
    Ship,
    Caravan,
    Prostheses
}

public class StockGenerator_SerumChance : StockGenerator
{
    public ThingDef thingDef;

    public SerumTrader traderGroup;

    public override IEnumerable<Thing> GenerateThings(PlanetTile forTile, Faction faction = null)
    {
        if (thingDef == null || !Rand.Chance(RimHealthMod.Settings.Chance(thingDef, traderGroup)))
        {
            yield break;
        }
        foreach (Thing thing in StockGeneratorUtility.TryMakeForStock(thingDef, RandomCountOf(thingDef), faction))
        {
            yield return thing;
        }
    }

    public override bool HandlesThingDef(ThingDef def)
    {
        return def == thingDef && RimHealthMod.Settings.Enabled(def);
    }
}
