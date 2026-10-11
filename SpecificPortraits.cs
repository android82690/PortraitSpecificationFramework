using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SpecificPortraits
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInDependency("jp.cmbc.mod.elin.yk-framework", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("evilmask.elinplugins.modoptions", BepInDependency.DependencyFlags.SoftDependency)]
    public class SpecificPortraits : BaseUnityPlugin
    {
        public const string pluginGuid = "SpecificPortraits";
        public const string pluginName = "Specific Portraits";
        public const string pluginVersion = "0.11.1";

        /// <summary>Local package that keeps the player's pool links across updates of this mod.</summary>
        public const string SettingsPackageName = "Mod_Specific_Portraits_Settings";
        public const string SettingsPackageId = "android_specific_portraits_settings";
        public const string LinkFileName = "PSFLinks.json";
        public const string LegacyLinkFileName = "portrait_specification_framework_links.txt";

        public static ManualLogSource Log;
        public static Harmony harmony;

        /// <summary>Activated packages that ship portraits or pool links.</summary>
        public static List<BaseModPackage> loadedModPackages = new List<BaseModPackage>();
        /// <summary>Portrait files per package folder name. Sprites load on first display, not at startup.</summary>
        public static Dictionary<string, ModItemList<Sprite>> spriteListDataByMod = new Dictionary<string, ModItemList<Sprite>>();

        /// <summary>Pools defined in link files or the in-game editor, by <see cref="PortraitPoolTag.Key"/>.</summary>
        public static Dictionary<string, PortraitPoolTag> PortraitPoolTags = new Dictionary<string, PortraitPoolTag>();
        /// <summary>Pools inferred from file names such as c_f-xxx or guard_m-xxx, by key.</summary>
        public static Dictionary<string, PortraitPoolTag> InferredPortraitPoolTags = new Dictionary<string, PortraitPoolTag>();

        public const string allText = "all";
        public const string allTextJP = "すべて";
        public const string unspecifiedText = "unspecified";
        public const string unspecifiedTextJP = "未指定";

        public static ConfigEntry<bool> EnableRerollDialog { get; set; }
        public static ConfigEntry<bool> AlwaysIncludeVanillaPortraits { get; set; }
        public static ConfigEntry<bool> RandomizePortraitOnNoResults { get; set; }
        /// <summary>The plugin object; hosts coroutines that must outlive hidden UI tabs.</summary>
        public static SpecificPortraits Instance { get; private set; }

        /// <summary>The active map, or null on the title screen (the game's own accessor throws there).</summary>
        public static Map CurrentMap => EClass.core?.game?.activeZone?.map;

        public static string PackageRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Package"));
        public static string SettingsPackagePath => Path.Combine(PackageRoot, SettingsPackageName);

        public void Awake()
        {
            Log = Logger;
            Instance = this;

            RandomizePortraitOnNoResults = Config.Bind("Portrait Specification Framework",
                "RandomizePortraitOnNoResults",
                false,
                "Characters with no matching pool get a random portrait.\n" +
                "Picks from every loaded portrait of their gender and type.\n" +
                "Off: those characters keep the game's own choice.");

            EnableRerollDialog = Config.Bind("Portrait Specification Framework",
                "EnableRerollDialog",
                false,
                "Adds a \"Reroll your portrait\" line to NPC dialogue.\n" +
                "Not shown for unique characters.");

            AlwaysIncludeVanillaPortraits = Config.Bind("Always Include Vanilla Portraits",
                "MinimumPortraitsWhenMatched",
                false,
                "Mix the game's default portraits into every pool.\n" +
                "On: default portraits can be picked alongside linked ones.\n" +
                "Off: default portraits are only used when no pool matches.");

            EnsureSettingsPackage();

            harmony = new Harmony(pluginGuid);

            // Portrait selection for new characters (falls back to the game's picker when nothing matches).
            harmony.Patch(AccessTools.Method(typeof(Biography), nameof(Biography.SetPortrait)),
                new HarmonyMethod(AccessTools.Method(typeof(PortraitPatch), nameof(PortraitPatch.SetPortraitPrefix))));

            // Collect portraits and pool links once the game has loaded every package.
            harmony.Patch(AccessTools.Method(typeof(Core), nameof(Core.Init)),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(PortraitPatch), nameof(PortraitPatch.InitPostfix))));

            // Characters the game never gives a portrait (animals, monsters) get one on zone entry when a pool matches.
            harmony.Patch(AccessTools.Method(typeof(Player.Flags), nameof(Player.Flags.OnEnterZone)),
                new HarmonyMethod(AccessTools.Method(typeof(PortraitPatch), nameof(PortraitPatch.OnEnterZonePrefix))));

            // Optional reroll line in dialogue, and element tint for "+color" portraits.
            harmony.Patch(AccessTools.Method(typeof(DramaActor), nameof(DramaActor.Talk)),
                new HarmonyMethod(AccessTools.Method(typeof(PortraitPatch), nameof(PortraitPatch.TalkPrefix))),
                new HarmonyMethod(AccessTools.Method(typeof(PortraitPatch), nameof(PortraitPatch.TalkPostfix))));

            ModOptionsIntegration.TryRegister();

            Log.LogMessage("Portrait Specification Framework Loaded");
        }

        /// <summary>Create the local settings package on first run so pool links survive mod updates.</summary>
        public static void EnsureSettingsPackage(string gameVersion = "0.23.352")
        {
            try
            {
                var dir = SettingsPackagePath;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var meta = Path.Combine(dir, "package.xml");
                if (!File.Exists(meta))
                    File.WriteAllText(meta, SettingsPackageXml(gameVersion));

                var links = Path.Combine(dir, LinkFileName);
                if (!File.Exists(links))
                    File.WriteAllText(links, "{\"Version\":3,\"PoolsByPortrait\":{},\"Tags\":[]}");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Could not prepare the settings package: {ex.Message}");
            }
        }

        static string SettingsPackageXml(string gameVersion)
        {
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<Meta>\n" +
                "  <title>Portrait Specification Framework Local Settings</title>\n" +
                $"  <id>{SettingsPackageId}</id>\n" +
                "  <author>Android</author>\n" +
                "  <loadPriority>999</loadPriority>\n" +
                "  <description>\n" +
                "  Settings mod for PSF, it persists through updates of the main mod so people don't lose their settings\n" +
                "  </description>\n" +
                $"  <version>{gameVersion}</version>\n" +
                "</Meta>";
        }

        /// <summary>
        /// The game skips packages whose version is older than its minimum mod version.
        /// Rewrite the settings package version to the running game when that happens.
        /// </summary>
        public static void RefreshSettingsPackageVersion(Core core)
        {
            try
            {
                var package = core.mods.packages.FirstOrDefault(p => p.dirInfo != null && p.dirInfo.Name == SettingsPackageName);
                if (package == null || package.IsValidVersion())
                    return;

                var v = core.version;
                var current = $"{v.major}.{v.minor}.{v.batch}";
                var meta = Path.Combine(package.dirInfo.FullName, "package.xml");
                var text = File.ReadAllText(meta);
                text = Regex.Replace(text, "<version>[^<]*</version>", $"<version>{current}</version>");
                File.WriteAllText(meta, text);
                Log.LogWarning($"The settings package was skipped by the game (made for {package.version}). " +
                    $"Its version is now {current}; saved pool links load again on the next launch.");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Could not update the settings package version: {ex.Message}");
            }
        }

        /// <summary>Portrait sprite by id, loaded on first use. Null when the id is unknown.</summary>
        public static Sprite GetSprite(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            var item = Portrait.modPortraits.GetItem(id, returnNull: true);
            return item?.GetObject();
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

                // Try any two consecutive parts as one id, like citizen_fairy or horse_yowyn.
                if ((potentialCard == null || potentialCard == potentialRace) && potentialIds.Length >= 2)
                {
                    var potentialLongIDs = new List<string>();
                    for (int i = 0; i < potentialIds.Length - 1; i++)
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

                // "fairy" is both a race and a card id; treat it as the race.
                if (tag.CardID == tag.Race)
                    tag.CardID = "all";
            }

            return tag;
        }
    }
}
