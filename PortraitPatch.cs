using Newtonsoft.Json;
using SpecificPortraits.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using YKF;

namespace SpecificPortraits
{
    class PortraitPatch
    {
        public static bool parseExtraSucceeded = false;

        static Chara lastTalkTarget = null;
        static DramaActor lastDramaActor = null;
        private class SpriteComparer : IEqualityComparer<ModItem<Sprite>>
        {
            public bool Equals(ModItem<Sprite> a, ModItem<Sprite> b)
            {
                return a.id == b.id;
            }

            public int GetHashCode(ModItem<Sprite> obj)
            {
                return obj.id.GetHashCode();
            }

        }

        public static void LoadAllSubdirectoryPortraits(string path, string modId)
        {

            try
            {
                var fileList = new List<FileInfo>();
                var files = new DirectoryInfo(path).GetFiles();
                foreach (FileInfo file in files)
                {
                    if (file.Name.EndsWith(".png"))
                    {
                        if (file.Name.StartsWith("BG_"))
                            Portrait.modPortraitBGs.Add(file);
                        else if (file.Name.StartsWith("BGF_"))
                            Portrait.modPortraitBGFs.Add(file);
                        else if (file.Name.EndsWith("-full.png"))
                            Portrait.modFull.Add(file);
                        else if (file.Name.EndsWith("-overlay.png"))
                            Portrait.modOverlays.Add(file);
                        else
                        {
                            SpecificPortraits.spriteListDataByMod[modId].Add(file);
                            Portrait.modPortraits.Add(file);
                            var tag = SpecificPortraits.ResolvePoolTagFromString(Path.GetFileNameWithoutExtension(file.FullName).Split("-")[0], modId, new string[] { "c", "guard", "foxfolk" });
                            if (tag != null)
                            {
                                if (!SpecificPortraits.InferredPortraitPoolTags.ContainsKey(tag.GetHashCode()))
                                {
                                    SpecificPortraits.InferredPortraitPoolTags.Add(tag.GetHashCode(), tag);
                                    tag.Portraits.Add(Path.GetFileNameWithoutExtension(file.FullName));
                                }
                                else if (!SpecificPortraits.InferredPortraitPoolTags[tag.GetHashCode()].Portraits.Contains(Path.GetFileNameWithoutExtension(file.FullName)))
                                {
                                    SpecificPortraits.InferredPortraitPoolTags[tag.GetHashCode()].Portraits.Add(Path.GetFileNameWithoutExtension(file.FullName));
                                }

                            }

                        }
                        Portrait.allIds.Add(file.Name);
                    }
                }

                foreach (var dir in new DirectoryInfo(path).GetDirectories())
                {
                    LoadAllSubdirectoryPortraits(dir.FullName, modId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PortraitSpecificationFramework::LoadAllSubdirectoryPortraits] UNRECOVERABLE ERROR: {ex.Message}");
                Console.WriteLine($"[PortraitSpecificationFramework::LoadAllSubdirectoryPortraits] Anomalous path: {path}");
                Console.WriteLine($"[PortraitSpecificationFramework::LoadAllSubdirectoryPortraits] Some files in this directory may have failed to load");
            }
        }

        public static void RerollPortrait()
        {
            if (lastTalkTarget != null)
            {
                SetPortraitPrefix(lastTalkTarget, lastTalkTarget.bio);
                var children = lastDramaActor.GetComponentsInChildren<Image>(includeInactive: true);
                lastDramaActor.gameObject.SetActive(false);
                lastDramaActor.gameObject.SetActive(true);
                foreach(var child in children)
                {
                    Console.WriteLine($"{child.name}");
                }
            }
        }

        public static void OpenWindowChara()
        {
            LayerChara layerChara = EClass.ui.ToggleLayer<LayerChara>();
            if (layerChara != null)
            {
                layerChara.SetChara(lastTalkTarget);

                EClass.ui.AddLayer<LayerEditPCC>().Activate(lastTalkTarget, UIPCC.Mode.Body, _onKill: (Action)(() => layerChara.windowChara.portrait.SetChara(layerChara.windowChara.chara)));
            }
        }

        public static void AddButtonPrefix(string idLang, UIContextMenu __instance)
        {
            if (idLang == "LayerMod")
            {
                __instance.AddButton("ポートレート ツール"._("Portrait Tools"), (Action)(() =>
                {
                    YK.CreateLayer<PSFLayerMain>();
                }));
            }
        }

        public static void TalkPrefix(string text, ref List<DramaChoice> choices, bool center, bool unknown, DramaActor __instance)
        {
            if (SpecificPortraits.EnableRerollDialog.Value)
            {
                var currentLanguage = Lang.isJP ? "JP" : (choices.Any(c => c.text == "再见") ? "CN" : "EN");

                string rerollText = currentLanguage == "JP" ? "ポートレートを再ロールする" : (currentLanguage == "CN" ? "更改肖像" : "Reroll your portrait.");
                //string editCharaText = currentLanguage == "JP" ? "EditCharacter_JP" : (currentLanguage == "CN" ? "EditCharacter_CN" : "Edit character.");
                lastDramaActor = __instance;
                if (choices.Any(c => c.idJump == "_bye") && !__instance.owner.chara.IsPC && !__instance.owner.chara.IsUnique)
                {
                    if (!choices.Any(c => c.text == rerollText))
                        choices.Add(new DramaChoice(rerollText, "_bye")
                        {
                            onClick = RerollPortrait
                        });
                }
            }

            lastTalkTarget = __instance?.owner?.chara;

            //if (lastTalkTarget != null)
            //{
            //    //Console.WriteLine($"{lastTalkTarget.bio.gender}_{lastTalkTarget.race.id}_{lastTalkTarget.id}_{lastTalkTarget.job.id}");
            //    //Console.WriteLine($"    Human?: {lastTalkTarget.race.IsHuman}");
            //    //Console.WriteLine($"    Unique?: {lastTalkTarget.IsUnique}");
            //    //Console.WriteLine($"    Agent?: {lastTalkTarget.IsAgent}");
            //    //Console.WriteLine($"    Adv?: {lastTalkTarget.IsAdventurer}");
            //}
        }

        public static void TalkPostfix(DramaActor __instance) {

            if (lastTalkTarget != null && __instance?.dialog?.portrait != null)
            {
                if (string.IsNullOrEmpty(lastTalkTarget.c_idPortrait) || !lastTalkTarget.c_idPortrait.EndsWith("+color"))
                {
                    return;
                }

                var viableTag = SpecificPortraits.PortraitPoolTags.Select(t => t.Value).FirstOrDefault(t => t.Portraits.Contains(lastTalkTarget.c_idPortrait));
                if (viableTag == null)
                {
                    return;
                }

                Color charaColor = EClass.setting.elements[lastTalkTarget.MainElement.source.alias].colorSprite;

                __instance.dialog.portrait.overlay.color = charaColor;
            }
        }


        private static void ParseLinkFile(FileInfo linkFile, string modID)
        {
            var linkText = File.ReadAllText(linkFile.FullName);

            var lines = linkText.Split(Environment.NewLine);
            if (lines[0].Trim().StartsWith("version") && lines[0].Trim().EndsWith("1"))
            {

                for (int i = 1; i < lines.Length; i++)
                {
                    var lineSplit = lines[i].Split(":");
                    var originalId = lineSplit[0].Trim();
                    if (!Portrait.modPortraits.dict.ContainsKey(originalId))
                        continue;

                    List<string> tagStrings = lineSplit[1].Split(",").Select(key => key.Trim()).ToList();
                    foreach (var tagString in tagStrings)
                    {
                        var tag = SpecificPortraits.ResolvePoolTagFromString(tagString, modID);
                        if (tag == null)
                            continue;

                        if (!SpecificPortraits.PortraitPoolTags.ContainsKey(tag.GetHashCode()))
                        {
                            SpecificPortraits.PortraitPoolTags[tag.GetHashCode()] = tag;
                        }
                        SpecificPortraits.PortraitPoolTags[tag.GetHashCode()].Portraits.Add(originalId);
                        SpecificPortraits.PortraitPoolTags[tag.GetHashCode()].Portraits = SpecificPortraits.PortraitPoolTags[tag.GetHashCode()].Portraits.Distinct().ToList();
                    }
                }
            }
        }

        private static void ParseLinkFileJSON(FileInfo linkFile, string modID)
        {

            PortraitPoolFile poolFile = JsonConvert.DeserializeObject<PortraitPoolFile>(File.ReadAllText(linkFile.FullName));

            if (poolFile.Version == 3)
            {
                foreach (var tag in poolFile.Tags)
                {
                    if (string.IsNullOrEmpty(tag.ModID))
                        tag.ModID = modID;
                    if (!SpecificPortraits.PortraitPoolTags.ContainsKey(tag.GetHashCode()))
                    {
                        SpecificPortraits.PortraitPoolTags.Add(tag.GetHashCode(), tag);
                    }
                    else
                    {
                        SpecificPortraits.PortraitPoolTags[tag.GetHashCode()].Portraits.AddRange(tag.Portraits);
                        SpecificPortraits.PortraitPoolTags[tag.GetHashCode()].Portraits = SpecificPortraits.PortraitPoolTags[tag.GetHashCode()].Portraits.Distinct().ToList();
                    }
                }
            }
            else if (poolFile.Version == 2)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                foreach (var kv in poolFile.PoolsByPortrait)
                {
                    var spriteID = kv.Key;
                    if (!Portrait.modPortraits.dict.ContainsKey(spriteID))
                        continue;

                    var sourcePortrait = Portrait.modPortraits.dict[spriteID];
                    foreach (var tagString in kv.Value)
                    {
                        var tag = SpecificPortraits.ResolvePoolTagFromString(tagString, modID);
                        if (tag == null)
                            continue;

                        if (!SpecificPortraits.PortraitPoolTags.ContainsKey(tag.GetHashCode()))
                        {
                            SpecificPortraits.PortraitPoolTags[tag.GetHashCode()] = tag;
                        }
                        SpecificPortraits.PortraitPoolTags[tag.GetHashCode()].Portraits.Add(spriteID);
                        SpecificPortraits.PortraitPoolTags[tag.GetHashCode()].Portraits = SpecificPortraits.PortraitPoolTags[tag.GetHashCode()].Portraits.Distinct().ToList();
                    }
                }
                sw.Stop();
            }
        }
        public static void InitPostfix(Core __instance)
        {
            try
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                Console.WriteLine("[PortraitSpecificationFramework] Loading Subdirectories");
                SpecificPortraits.loadedModPackages.AddRange(__instance.mods.packages.Where(p => p.activated
                && (Directory.Exists(Path.Combine(p.dirInfo.FullName, "Portrait"))
                    || p.dirInfo.GetFiles("portrait_specification_framework_links.txt").Length > 0
                    || p.dirInfo.GetFiles("PSFLinks*.json").Length > 0)));

                foreach (var package in SpecificPortraits.loadedModPackages)
                {
                    if (Directory.Exists(Path.Combine(package.dirInfo.FullName, "Portrait")))
                    {
                        Console.WriteLine($"[PortraitSpecificationFramework] <<<< {package.title}");
                        var modIDstring = package.dirInfo.FullName.Split(Path.DirectorySeparatorChar).Last();
                        SpecificPortraits.spriteListDataByMod.Add(modIDstring, new ModItemList<Sprite>());
                        LoadAllSubdirectoryPortraits(Path.Combine(package.dirInfo.FullName, "Portrait"), modIDstring);
                    }
                }
                Console.WriteLine($"[PortraitSpecificationFramework] Done at: {sw.Elapsed}");
                Console.WriteLine("[PortraitSpecificationFramework] Retrieving mod links");

                //vanilla, private, and workshop mods
                foreach (var package in SpecificPortraits.loadedModPackages)
                {
                    var linkFiles = package.dirInfo.GetFiles("portrait_specification_framework_links.txt");
                    var linkFilesJSON = package.dirInfo.GetFiles("PSFLinks*.json");
                    if (linkFilesJSON.Length >= 1)
                    {
                        foreach (var file in linkFilesJSON)
                            ParseLinkFileJSON(file, package.dirInfo.Name);
                    }
                    if (linkFiles.Length >= 1)
                    {
                        ParseLinkFile(linkFiles[0], package.dirInfo.Name);
                    }
                }

                parseExtraSucceeded = true;
                Console.WriteLine($"[PortraitSpecificationFramework] Portrait Lists Generated in {sw.Elapsed}");

                sw.Restart();
                Console.WriteLine($"[PortraitSpecificationFramework] Preloading Sprites");
                SpecificPortraits.LoadAllPortraitSprites();
                Console.WriteLine($"[PortraitSpecificationFramework] Preloading Sprites done in {sw.Elapsed}");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PortraitSpecificationFramework] UNRECOVERABLE ERROR: {ex.Message}. Falling back to vanilla portrait picker");
                Console.WriteLine(ex.StackTrace);
                return;
            }
        }

        public static void OnEnterZonePrefix()
        {
            foreach (var chara in ELayer._map.charas)
            {
                if (string.IsNullOrEmpty(chara.c_idPortrait) || !Portrait.modPortraits.dict.ContainsKey(chara.c_idPortrait))
                    SetPortraitPrefix(chara, chara.bio);
            }
        }

        public static bool SetPortraitPrefix(Chara c, Biography __instance)
        {
            try
            {
                if (c is null)
                {
                    return true; //no character handle, don't think this ever happens but fall back to vanilla just in case if it does
                }

                string cat = "";
                bool importantCat = false;

                if (c.trait is TraitGuard)
                {
                    importantCat = true;
                    cat = "guard";
                }

                if (c.race.id == "mifu" || c.race.id == "nefu")
                {
                    cat = "foxfolk";
                    importantCat = true;
                }

                if (c.id == "shojo" || c.id == "sister")
                {
                    importantCat = true;
                    cat = "special";
                }
                if (string.IsNullOrEmpty(cat))
                    cat = "c";

                //get gender string from gender id int
                string genderString = __instance.gender == 2 ? "m" : (__instance.gender == 1 ? "f" : (EClass.rnd(2) == 0 ? "m" : "f"));

                //exclude non-viable tags
                var viableTags = SpecificPortraits.PortraitPoolTags
                    .Concat(SpecificPortraits.InferredPortraitPoolTags)
                    .Where(tag =>
                        ((!importantCat && tag.Value.Type == "all") || tag.Value.Type == cat || (tag.Value.Type == "c" && cat == "foxfolk"))
                        && (tag.Value.Gender == "all" || tag.Value.Gender == genderString)
                        && (tag.Value.Race == "all" || tag.Value.Race == c.race.id)
                        && (tag.Value.Job == "all" || tag.Value.Job == c.job.id)
                        && (tag.Value.CardID == "all" || tag.Value.CardID == c.id)
                        && (tag.Value.AgeMin == 0 || tag.Value.AgeMin < c.bio.GetAge(c))
                        && (tag.Value.AgeMax == 0 || tag.Value.AgeMax > c.bio.GetAge(c))
                        )
                    .ToList();

                //Matching ID will take priority over tags where id is not matched but other fields are
                bool idMatch = viableTags.Any(tag => tag.Value.CardID == c.id);

                if (idMatch)
                {
                    viableTags = viableTags.Where(tag => tag.Value.CardID == c.id).ToList();
                }

                //remove vanilla tags from pool unless specified not to in settings
                if (!SpecificPortraits.AlwaysIncludeVanillaPortraits.Value)
                {
                    viableTags = viableTags.Where(tag => tag.Value.Type != "c" || tag.Value.Race != "all" || tag.Value.CardID != "all" || tag.Value.Job != "all").ToList();
                }

                //flatten portraits, and select only the ones currently loaded (ignore stale links)
                var viablePortraitIDs = viableTags
                    .SelectMany(tag => tag.Value.Portraits)
                    .Where(portrait => Portrait.modPortraits.dict.ContainsKey(portrait))
                    .ToList();

                //Console.WriteLine($"{cat}_{genderString}_{c.race.id}_{c.id}_{c.job.id}");
                //Console.WriteLine($"    Human?: {c.race.IsHuman}");
                //Console.WriteLine($"    ID Match?: {idMatch}");
                //Console.WriteLine($"    Portraits?: {viablePortraitIDs.Count}");
                //Console.WriteLine($"    Unique?: {c.IsUnique}");
                //Console.WriteLine($"    Agent?: {c.IsAgent}");
                //Console.WriteLine($"    Adv?: {c.IsAdventurer}");

                if (viablePortraitIDs.Count > 0)
                    c.c_idPortrait = viablePortraitIDs.RandomItem();
                else if (c.race.IsHuman)
                    if(SpecificPortraits.RandomizePortraitOnNoResults.Value)
                        c.c_idPortrait = SpecificPortraits.spriteListDataByMod["_Elona"].dict.Where(kv => kv.Key.StartsWith($"{cat}_{genderString}")).RandomItem().Key;
                    else 
                        return true;
                else
                        c.c_idPortrait = null;


                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SpecificPortraits]UNRECOVERABLE ERROR: {ex.Message}. Falling back to vanilla portrait picker");
                return true;
            }
        }

        //selection sampling impl
        private static List<T> RandomSubset<T>(List<T> list, int num)
        {
            if (num > list.Count) return list;

            var random = new System.Random();
            var result = new List<T>();
            int picked = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var numerator = num - picked;
                //c# random nextint is half open -> [min,max) so add 1 to max to make it [min,max]
                var denominator = random.Next(1, list.Count - i + 1);
                if (numerator >= denominator)
                {
                    picked++;
                    result.Add(list[i]);
                }

                if (picked >= num)
                {
                    break;
                }
            }

            return result;
        }
    }
}
