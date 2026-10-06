using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;

namespace SpecificPortraits
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    class SpecificPortraits : BaseUnityPlugin
    {
        public const string pluginGuid = "SpecificPortraits";
        public const string pluginName = "Specific Portraits";
        public const string pluginVersion = "0.8";

        public static List<BaseModPackage> loadedModPackages = new List<BaseModPackage>();

        public static string PersonalSettingsLocation = "Package/Mod_Specific_Portraits_Settings";

        public static Harmony harmony = null;
        public static Dictionary<string, Sprite> spriteListData = new Dictionary<string, Sprite>();
        public static Dictionary<string, ModItemList<Sprite>> spriteListDataByMod = new Dictionary<string, ModItemList<Sprite>>();

        public static Dictionary<int, PortraitPoolTag> PortraitPoolTags = new Dictionary<int, PortraitPoolTag>();
        public static Dictionary<int, PortraitPoolTag> InferredPortraitPoolTags = new Dictionary<int, PortraitPoolTag>();
        //public static Dictionary<string, Dictionary<int, PortraitPoolTag>> PortraitPoolTagsByMod = new Dictionary<string, Dictionary<int, PortraitPoolTag>>();

        public const string allText = "all";
        public const string allTextJP = "すべて";
        public const string unspecifiedText = "unspecified";
        public const string unspecifiedTextJP = "未指定";

        public static ConfigEntry<bool> EnableRerollDialog { get; set; }
        public static ConfigEntry<bool> AlwaysIncludeVanillaPortraits { get; set; }

        public static ConfigEntry<bool> RandomizePortraitOnNoResults { get; set; }

        public void Awake()
        {
            RandomizePortraitOnNoResults = Config.Bind("Portrait Specification Framework",
                "RandomizePortraitOnNoResults",
                false,
                "If true and no overrides are set randomizes portrait from all vanilla loaded portraits, else falls back to vanilla");

            EnableRerollDialog = Config.Bind("Portrait Specification Framework",
                "EnableRerollDialog",
                false,
                "Enables legacy reroll option within character drama dialog, false by default");

            AlwaysIncludeVanillaPortraits = Config.Bind("Always Include Vanilla Portraits",
                "MinimumPortraitsWhenMatched",
                false,
                "When true; this option will make vanilla portraits available for selection on all corresponding male/female pools. When false; vanilla portraits will only be used as defaults when no modded portraits are present");

            //create separate mod for persistent settings
            if (!Directory.Exists(PersonalSettingsLocation))
            {
                Directory.CreateDirectory(PersonalSettingsLocation);

                File.WriteAllText(Path.Combine(PersonalSettingsLocation, "package.xml"),
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<Meta>
  <title>Portrait Specification Framework Local Settings</title>
  <id>android_specific_portraits_settings</id>
  <author>Android</author>
  <loadPriority>999</loadPriority>
  <description>
  Settings mod for PSF, it persists through updates of the main mod so people don't lose their settings
  </description>
  <version>0.23.74</version>
</Meta>");

                File.WriteAllText(Path.Combine(PersonalSettingsLocation, "PSFLinks.json"), "{\"Version\":3,\"PoolsByPortrait\":{},\"Tags\":[]}");
            }

            harmony = new Harmony(pluginGuid);

            //override elin set portrait method to use this one instead (defaults to base game method on failure)
            harmony.Patch(AccessTools.Method(typeof(Biography), nameof(Biography.SetPortrait)),
                new HarmonyMethod(AccessTools.Method(typeof(PortraitPatch), nameof(PortraitPatch.SetPortraitPrefix))));

            //populate portrait dictionaries at game start from vanilla and other mods once they have been loaded
            harmony.Patch(AccessTools.Method(typeof(Core), nameof(Core.Init)),
               postfix: new HarmonyMethod(AccessTools.Method(typeof(PortraitPatch), nameof(PortraitPatch.InitPostfix))));

            //responsible for assigning portraits for characters without portraits on map load, things like fairies, animals, enemies, etc.
            harmony.Patch(AccessTools.Method(typeof(Player.Flags), nameof(Player.Flags.OnEnterZone)),
               new HarmonyMethod(AccessTools.Method(typeof(PortraitPatch), nameof(PortraitPatch.OnEnterZonePrefix))));

            //adds button to tools menu
            harmony.Patch(AccessTools.Method(typeof(UIContextMenu), nameof(UIContextMenu.AddButton), parameters: new Type[] { typeof(string), typeof(Action), typeof(bool) }),
               new HarmonyMethod(AccessTools.Method(typeof(PortraitPatch), nameof(PortraitPatch.AddButtonPrefix))));

            //adds portrait reroll option based on flag
            harmony.Patch(AccessTools.Method(typeof(DramaActor), nameof(DramaActor.Talk)),
                new HarmonyMethod(AccessTools.Method(typeof(PortraitPatch), nameof(PortraitPatch.TalkPrefix))));

            //applies portrait color overlay settings based on element for things like fire hounds
            harmony.Patch(AccessTools.Method(typeof(DramaActor), nameof(DramaActor.Talk)), null,
                new HarmonyMethod(AccessTools.Method(typeof(PortraitPatch), nameof(PortraitPatch.TalkPostfix))));

            Logger.LogMessage("Portrait Specification Framework Loaded");
        }

        public static void Patch(Type originalClass, string originalMethodName, Type patchClass, string patchMethodName, Type[] originalParameters = null, Type[] patchParameters = null)
        {
            harmony.Patch(AccessTools.Method(originalClass, originalMethodName, parameters: originalParameters),
               new HarmonyMethod(AccessTools.Method(patchClass, patchMethodName)));
        }

        public static void Unpatch(Type originalClass, string originalMethodName, Type patchClass, string patchMethodName, Type[] originalParameters = null, Type[] patchParameters = null)
        {
            harmony.Unpatch(AccessTools.Method(originalClass, originalMethodName, parameters: originalParameters),
               AccessTools.Method(patchClass, patchMethodName));
        }

        public static void LoadAllPortraitSprites()
        {
            try {
                for (int i = 0; i < Portrait.modPortraits.list.Count; i++)
                {
                    var sprite = Portrait.modPortraits.list[i];         
                    spriteListData.Add(sprite.id, sprite.GetObject());
                }
            }
            catch(Exception ex) {
                Console.WriteLine($"[PortraitSpecificationFramework] LOADING PORTRAIT SPRITES FAILED, UNRECOVERABLE ERROR: {ex.Message}");
            }
        }

        public static PortraitPoolTag ResolvePoolTagFromString(string tagString, string modID = null, string[] includeTypeList = null)
        {
            var tag = new PortraitPoolTag();
            if (modID != null)
                tag.ModID = modID;

            var splitTagString = tagString.Split("_");

            if (splitTagString.Length < 2)
                return null;

            if (includeTypeList == null)
                includeTypeList = new string[] { "c", "guard", "foxfolk", "special" };
            if (!includeTypeList.Contains(splitTagString[0]))
                return null;

            if (!new string[] { "m", "f" }.Contains(splitTagString[1]))
                return null;

            tag.Type = splitTagString[0];
            tag.Gender = splitTagString[1];

            if (splitTagString.Length > 2)
            {
                var potentialIds = splitTagString.Skip(2).ToArray();
                var potentialRace = EClass.sources.races.rows.Where(r => potentialIds.Contains(r.id)).Select(r => r.id).FirstOrDefault();
                var potentialJob = EClass.sources.jobs.rows.Where(r => potentialIds.Contains(r.id)).Select(r => r.id).FirstOrDefault();
                var potentialCard = EClass.sources.cards.rows.Where(r => potentialIds.Contains(r.id)).Select(r => r.id).FirstOrDefault();
                
                //attempt to find matches with any 2 consecutive elements like citizen_fairy or horse_yowyn
                if((potentialCard == null || potentialCard == potentialRace) && potentialIds.Length >= 2)
                {
                    var potentialLongIDs = new List<string>();
                    for(int i = 0; i < potentialIds.Length-1; i++)
                    {
                        potentialLongIDs.Add($"{potentialIds[i]}_{potentialIds[i + 1]}");
                    }
                    potentialCard = EClass.sources.cards.rows.Where(r => potentialLongIDs.Contains(r.id)).Select(r => r.id).FirstOrDefault();
                }

                if (potentialRace != null)
                    tag.Race = potentialRace;
                if (potentialJob != null)
                    tag.Job = potentialJob;
                if (potentialCard != null)
                    tag.CardID = potentialCard;

                //handle fairy, there are entries for "fairy" in both the race table and id table so default to just race if equal
                if (tag.CardID == tag.Race)
                    tag.CardID = "all";
            }

            return tag;

        }

    }

    
}
