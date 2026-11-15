using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimHealthSerums;

public static class SerumBills
{
    public static bool CraftAllowed(RecipeDef recipe)
    {
        return RimHealthMod.Settings.allowCrafting && RimHealthMod.Settings.Enabled(recipe.ProducedThingDef);
    }

    public static bool PutBack(Thing thing, IntVec3 cell, Map map, Pawn carrier)
    {
        if (GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near))
        {
            return true;
        }
        if (carrier?.inventory != null && carrier.inventory.innerContainer.TryAdd(thing))
        {
            return true;
        }
        if (cell.IsValid && cell.InBounds(map))
        {
            GenSpawn.Spawn(thing, cell, map);
            return true;
        }
        return false;
    }

    public static void SuspendUnavailable()
    {
        if (Current.Game?.Maps == null)
        {
            return;
        }
        int count = 0;
        foreach (Map map in Current.Game.Maps)
        {
            foreach (Building building in map.listerBuildings.allBuildingsColonist)
            {
                if (!(building is IBillGiver giver) || giver.BillStack == null)
                {
                    continue;
                }
                List<Bill> bills = giver.BillStack.Bills;
                for (int i = 0; i < bills.Count; i++)
                {
                    if (bills[i].recipe.Worker is RecipeWorker_CraftSerum && !bills[i].suspended && !CraftAllowed(bills[i].recipe))
                    {
                        bills[i].suspended = true;
                        count++;
                    }
                }
            }
        }
        if (count > 0)
        {
            Messages.Message(count == 1 ? "ZZRH_BillSuspended".Translate() : "ZZRH_BillsSuspended".Translate(count), MessageTypeDefOf.NeutralEvent, historical: false);
        }
    }
}

public class GameComponent_SerumBills : GameComponent
{
    public GameComponent_SerumBills(Game game)
    {
    }

    public override void FinalizeInit()
    {
        SerumBills.SuspendUnavailable();
    }
}
