using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimHealthSerums;

[StaticConstructorOnStartup]
public static class SerumGraphics
{
    private const string ClassicFolder = "Classic/";

    private static readonly Dictionary<ThingDef, string> basePaths = new Dictionary<ThingDef, string>();

    private static readonly MethodInfo resolveIcon = typeof(BuildableDef).GetMethod("ResolveIcon", BindingFlags.NonPublic | BindingFlags.Instance);

    static SerumGraphics()
    {
        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs.Where(d => d.HasModExtension<SerumExtension>() && d.graphicData != null))
        {
            basePaths[def] = def.graphicData.texPath;
        }
        Apply(true);
    }

    private static string PathFor(string basePath, bool classic)
    {
        if (!classic)
        {
            return basePath;
        }
        int slash = basePath.LastIndexOf('/');
        return basePath.Substring(0, slash + 1) + ClassicFolder + basePath.Substring(slash + 1);
    }

    public static void Apply(bool startup = false)
    {
        bool classic = RimHealthMod.Settings.classicGraphics;
        List<ThingDef> changed = new List<ThingDef>();
        foreach (KeyValuePair<ThingDef, string> pair in basePaths)
        {
            ThingDef def = pair.Key;
            string path = PathFor(pair.Value, classic);
            if (def.graphicData.texPath == path)
            {
                continue;
            }
            GraphicData data = new GraphicData();
            data.CopyFrom(def.graphicData);
            data.texPath = path;
            def.graphicData = data;
            def.graphic = data.Graphic;
            if (startup)
            {
                def.graphic.TryInsertIntoAtlas(TextureAtlasGroup.Item);
            }
            resolveIcon?.Invoke(def, null);
            changed.Add(def);
        }
        if (!startup && changed.Count > 0)
        {
            RefreshThings(changed);
        }
    }

    private static void RefreshThings(List<ThingDef> defs)
    {
        if (Current.Game == null)
        {
            return;
        }
        HashSet<ThingDef> set = new HashSet<ThingDef>(defs);
        List<Thing> things = new List<Thing>();
        foreach (Map map in Find.Maps)
        {
            things.Clear();
            ThingOwnerUtility.GetAllThingsRecursively(map, ThingRequest.ForGroup(ThingRequestGroup.HaulableEver), things);
            foreach (Thing thing in things)
            {
                if (!set.Contains(thing.def))
                {
                    continue;
                }
                thing.Notify_DefsHotReloaded();
                if (thing.Spawned)
                {
                    map.mapDrawer.MapMeshDirty(thing.Position, MapMeshFlagDefOf.Things);
                }
            }
        }
        foreach (Caravan caravan in Find.WorldObjects.Caravans)
        {
            foreach (Thing thing in CaravanInventoryUtility.AllInventoryItems(caravan))
            {
                if (set.Contains(thing.def))
                {
                    thing.Notify_DefsHotReloaded();
                }
            }
        }
    }
}
