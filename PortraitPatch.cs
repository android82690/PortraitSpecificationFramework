using SpecificPortraits.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using YKF;

namespace SpecificPortraits
{
    /// <summary>What a pool filter is matched against for one character.</summary>
    public struct MatchContext
    {
        public string Category;
        public bool ImportantCategory;
        /// <summary>"m", "f", or null when either gender should match.</summary>
        public string Gender;
        public int Age;
        public string RaceId;
        public string JobId;
        public string CardId;
    }

    public static class PortraitPatch
    {
        public static bool parseExtraSucceeded = false;
        static bool menuRegistered = false;

        /// <summary>Per-character flag: portrait chosen by hand or cleared on purpose, rerolls leave it alone.</summary>
        public const string LockKey = "psf_lock";

        static Chara lastTalkTarget = null;
        static DramaActor lastDramaActor = null;

        /// <summary>Characters the game gives a fixed portrait of its own (see Biography.SetPortrait).</summary>
        static readonly HashSet<string> specialIds = new HashSet<string>
        {
            "shojo", "sister", "sister_shark", "sister_penguin", "imotoroid", "imotoroid_origin", "citizen_exile"
        };

        static readonly string[] inferredTypes = { "c", "guard", "foxfolk" };

        #region lock

        public static bool IsLocked(Chara c)
        {
            return c != null && c.GetStr(LockKey) == "1";
        }

        public static void SetLocked(Chara c, bool locked)
        {
            c?.SetStr(LockKey, locked ? "1" : null);
        }

        /// <summary>
        /// Give a character a portrait by hand and lock it. Uniques and characters with a
        /// portrait override keep showing their own picture unless the override follows.
        /// </summary>
        public static void AssignManual(Chara c, string id)
        {
            if (c == null)
                return;
            c.c_idPortrait = id;
            if (string.IsNullOrEmpty(id))
                c.SetPortraitOverride(null);
            else if (c.GetIdPortrait() != id)
                c.SetPortraitOverride(id);
            SetLocked(c, true);
        }

        /// <summary>Remove the portrait and lock the character so nothing hands out a new one.</summary>
        public static void ClearManual(Chara c)
        {
            AssignManual(c, null);
        }

        #endregion

        #region portraits

        /// <summary>
        /// Register every png under a package's Portrait folder, including subfolders the game itself
        /// does not scan, and infer a pool from file names such as c_f-name or guard_m-name.
        /// </summary>
        public static void LoadAllSubdirectoryPortraits(string path, string modId)
        {
            try
            {
                foreach (FileInfo file in new DirectoryInfo(path).GetFiles())
                {
                    if (!file.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (file.Name.StartsWith("BG_", StringComparison.Ordinal))
                        Portrait.modPortraitBGs.Add(file);
                    else if (file.Name.StartsWith("BGF_", StringComparison.Ordinal))
                        Portrait.modPortraitBGFs.Add(file);
                    else if (file.Name.EndsWith("-full.png", StringComparison.Ordinal))
                        Portrait.modFull.Add(file);
                    else if (file.Name.EndsWith("-overlay.png", StringComparison.Ordinal))
                        Portrait.modOverlays.Add(file);
                    else
                    {
                        SpecificPortraits.spriteListDataByMod[modId].Add(file);
                        Portrait.modPortraits.Add(file);

                        string id = Path.GetFileNameWithoutExtension(file.Name);
                        var tag = SpecificPortraits.ResolvePoolTagFromString(id.Split("-")[0], modId, inferredTypes);
                        if (tag != null)
                        {
                            if (!SpecificPortraits.InferredPortraitPoolTags.TryGetValue(tag.Key, out var pool))
                            {
                                pool = tag;
                                SpecificPortraits.InferredPortraitPoolTags.Add(tag.Key, pool);
                            }
                            if (!pool.Portraits.Contains(id))
                                pool.Portraits.Add(id);
                        }
                    }
                    Portrait.allIds.Add(file.Name);
                }

                foreach (var dir in new DirectoryInfo(path).GetDirectories())
                {
                    LoadAllSubdirectoryPortraits(dir.FullName, modId);
                }
            }
            catch (Exception ex)
            {
                SpecificPortraits.Log.LogError($"Could not read portraits in {path}: {ex.Message}");
            }
        }

        public static void InitPostfix(Core __instance)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                SpecificPortraits.loadedModPackages.Clear();
                SpecificPortraits.spriteListDataByMod.Clear();
                SpecificPortraits.PortraitPoolTags.Clear();
                SpecificPortraits.InferredPortraitPoolTags.Clear();
                LinkFiles.Dirty.Clear();

                SpecificPortraits.loadedModPackages.AddRange(__instance.mods.packages.Where(p => p.activated && p.dirInfo != null
                    && (Directory.Exists(Path.Combine(p.dirInfo.FullName, "Portrait"))
                        || p.dirInfo.GetFiles(SpecificPortraits.LegacyLinkFileName).Length > 0
                        || p.dirInfo.GetFiles("PSFLinks*.json").Length > 0)));

                foreach (var package in SpecificPortraits.loadedModPackages)
                {
                    var portraitDir = Path.Combine(package.dirInfo.FullName, "Portrait");
                    if (!Directory.Exists(portraitDir))
                        continue;
                    var modId = package.dirInfo.Name;
                    SpecificPortraits.spriteListDataByMod[modId] = new ModItemList<Sprite>();
                    LoadAllSubdirectoryPortraits(portraitDir, modId);
                    SpecificPortraits.Log.LogInfo($"Portraits: {package.title} ({SpecificPortraits.spriteListDataByMod[modId].list.Count})");
                }

                // The game caches its own portrait pools; rebuild them now that subfolder portraits exist.
                Portrait.dictList.Clear();

                LinkFiles.LoadAll();

                parseExtraSucceeded = true;
                SpecificPortraits.Log.LogInfo($"{SpecificPortraits.InferredPortraitPoolTags.Count} inferred pools, " +
                    $"{Portrait.modPortraits.list.Count} portraits, {sw.ElapsedMilliseconds} ms");

                SpecificPortraits.RefreshSettingsPackageVersion(__instance);
                ThumbnailCache.Start();

                if (!menuRegistered)
                {
                    menuRegistered = true;
                    ModUtil.AddContextMenuEntry(OpenToolsWindow, "psf_portrait_tools", Lang.isJP ? "ポートレート ツール" : "Portrait Tools");
                }
            }
            catch (Exception ex)
            {
                SpecificPortraits.Log.LogError($"Portrait setup failed, the game's own portrait picker will be used: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>Opens the YK Framework window. Throws when YK Framework is not installed.</summary>
        public static void OpenToolsWindow()
        {
            YK.CreateLayer<PSFLayerMain>();
        }

        #endregion

        #region dialogue

        public static void RerollPortrait()
        {
            if (lastTalkTarget == null)
                return;
            SetLocked(lastTalkTarget, false);
            Assign(lastTalkTarget, lastTalkTarget.bio, force: true);
            try
            {
                lastDramaActor?.dialog?.portrait?.SetChara(lastTalkTarget);
            }
            catch (Exception ex)
            {
                SpecificPortraits.Log.LogWarning($"Portrait rerolled but the dialogue could not refresh it: {ex.Message}");
            }
        }

        static string RerollText(List<DramaChoice> choices)
        {
            var code = Lang.langCode ?? "";
            if (code == "JP")
                return "ポートレートを再ロールする";
            if (code.StartsWith("CN") || code.StartsWith("ZH") || choices.Any(c => c.text == "再见"))
                return "更改肖像";
            return "Reroll your portrait.";
        }

        public static void TalkPrefix(string text, List<DramaChoice> choices, bool center, bool unknown, DramaActor __instance)
        {
            lastDramaActor = __instance;
            lastTalkTarget = __instance?.owner?.chara;

            if (!SpecificPortraits.EnableRerollDialog.Value || choices == null || lastTalkTarget == null)
                return;

            string rerollText = RerollText(choices);
            if (choices.Any(c => c.idJump == "_bye") && !lastTalkTarget.IsPC && !lastTalkTarget.IsUnique && !choices.Any(c => c.text == rerollText))
            {
                choices.Add(new DramaChoice(rerollText, "_bye") { onClick = RerollPortrait });
            }
        }

        /// <summary>Portraits whose id ends in "+color" are tinted with the character's main element.</summary>
        public static void TalkPostfix(DramaActor __instance)
        {
            try
            {
                if (lastTalkTarget == null || __instance?.dialog?.portrait == null)
                    return;

                var id = lastTalkTarget.c_idPortrait;
                if (string.IsNullOrEmpty(id) || !id.EndsWith("+color"))
                    return;
                if (!SpecificPortraits.PortraitPoolTags.Values.Any(t => t.Portraits.Contains(id)))
                    return;

                var alias = lastTalkTarget.MainElement?.source?.alias;
                if (alias != null && EClass.setting.elements.TryGetValue(alias, out var elementRef))
                {
                    __instance.dialog.portrait.overlay.color = elementRef.colorSprite;
                }
            }
            catch (Exception ex)
            {
                SpecificPortraits.Log.LogWarning($"Could not tint the portrait: {ex.Message}");
            }
        }

        #endregion

        #region matching

        public static string Category(Chara c, out bool important)
        {
            important = false;
            string cat = "c";
            if (c.trait is TraitGuard)
            {
                cat = "guard";
                important = true;
            }
            if (c.race.id == "mifu" || c.race.id == "nefu")
            {
                cat = "foxfolk";
                important = true;
            }
            if (specialIds.Contains(c.id))
            {
                cat = "special";
                important = true;
            }
            return cat;
        }

        /// <summary>Context for previews: a character of unknown gender matches pools of either gender.</summary>
        public static MatchContext Context(Chara c, Biography bio)
        {
            string cat = Category(c, out bool important);
            return new MatchContext
            {
                Category = cat,
                ImportantCategory = important,
                Gender = bio.gender == 1 ? "f" : (bio.gender == 2 ? "m" : null),
                Age = bio.GetAge(c),
                RaceId = c.race.id,
                JobId = c.job.id,
                CardId = c.id
            };
        }

        public static bool Matches(PortraitPoolTag tag, in MatchContext m)
        {
            return ((!m.ImportantCategory && tag.Type == "all") || tag.Type == m.Category || (tag.Type == "c" && m.Category == "foxfolk"))
                && (tag.Gender == "all" || m.Gender == null || tag.Gender == m.Gender)
                && (tag.Race == "all" || tag.Race == m.RaceId)
                && (tag.Job == "all" || tag.Job == m.JobId)
                && (tag.CardID == "all" || tag.CardID == m.CardId)
                && (tag.AgeMin == 0 || m.Age >= tag.AgeMin)
                && (tag.AgeMax == 0 || m.Age <= tag.AgeMax);
        }

        /// <summary>Characters on the current map that a pool with this filter would apply to.</summary>
        public static List<Chara> PreviewMatches(PortraitPoolTag tag)
        {
            var result = new List<Chara>();
            var map = SpecificPortraits.CurrentMap;
            if (map == null)
                return result;
            foreach (var c in map.charas)
            {
                if (c == null || c.IsPC || c.bio == null)
                    continue;
                try
                {
                    if (Matches(tag, Context(c, c.bio)))
                        result.Add(c);
                }
                catch (Exception)
                {
                    // a character the game cannot describe (no race, no job) is simply not listed
                }
            }
            return result;
        }

        #endregion

        #region assignment

        public static void OnEnterZonePrefix()
        {
            try
            {
                var map = SpecificPortraits.CurrentMap;
                if (map == null)
                    return;

                foreach (var chara in map.charas)
                {
                    if (chara == null || chara.IsPC || IsLocked(chara))
                        continue;
                    var id = chara.c_idPortrait;
                    if (string.IsNullOrEmpty(id) || !Portrait.Exists(id))
                        Assign(chara, chara.bio, force: false);
                }
            }
            catch (Exception ex)
            {
                SpecificPortraits.Log.LogWarning($"Could not assign portraits on zone entry: {ex.Message}");
            }
        }

        /// <summary>Harmony prefix for Biography.SetPortrait.</summary>
        public static bool SetPortraitPrefix(Chara c, Biography __instance)
        {
            return Assign(c, __instance, force: false);
        }

        /// <summary>
        /// Pick a portrait from the pools that match the character.
        /// Returns true (run the game's own picker) when nothing matches.
        /// Locked characters are left alone unless forced.
        /// </summary>
        public static bool Assign(Chara c, Biography bio, bool force)
        {
            try
            {
                if (c == null || c.IsPC)
                    return true;
                bio = bio ?? c.bio;
                if (bio == null)
                    return true;
                if (!force && IsLocked(c))
                    return false;

                int genderInt = (bio.gender == 1 || bio.gender == 2) ? bio.gender : (EClass.rnd(2) == 0 ? 2 : 1);
                var ctx = Context(c, bio);
                ctx.Gender = genderInt == 2 ? "m" : "f";

                IEnumerable<PortraitPoolTag> candidates = SpecificPortraits.PortraitPoolTags.Values;
                if (SpecificPortraits.AlwaysIncludeVanillaPortraits.Value)
                    candidates = candidates.Concat(SpecificPortraits.InferredPortraitPoolTags.Values);

                var viableTags = candidates.Where(tag => Matches(tag, ctx)).ToList();

                // A pool made for this exact character wins over broader ones.
                if (viableTags.Any(tag => tag.CardID == ctx.CardId))
                    viableTags = viableTags.Where(tag => tag.CardID == ctx.CardId).ToList();

                var viablePortraitIDs = viableTags
                    .SelectMany(tag => tag.Portraits)
                    .Where(Portrait.Exists)
                    .Distinct()
                    .ToList();

                if (viablePortraitIDs.Count > 0)
                {
                    c.c_idPortrait = viablePortraitIDs.RandomItem();
                    return false;
                }

                if (c.IsHuman && SpecificPortraits.RandomizePortraitOnNoResults.Value
                    && !specialIds.Contains(ctx.CardId) && !c.HasTag(CTAG.noPortrait))
                {
                    c.c_idPortrait = Portrait.GetRandomPortrait(genderInt, c.GetIdPortraitCat());
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                SpecificPortraits.Log.LogError($"Portrait selection failed for {c?.id}, using the game's picker: {ex.Message}");
                return true;
            }
        }

        #endregion
    }
}
