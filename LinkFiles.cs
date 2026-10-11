using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpecificPortraits
{
    /// <summary>
    /// Reading and writing of pool link files, and the record of which packages
    /// have unsaved pool edits. No UI types here so it loads without YK Framework.
    /// </summary>
    public static class LinkFiles
    {
        /// <summary>Package folder names (ModID) whose pools were edited and not saved.</summary>
        public static readonly HashSet<string> Dirty = new HashSet<string>();

        public static bool IsSettingsPackage(BaseModPackage package)
        {
            return package.dirInfo.Name == SpecificPortraits.SettingsPackageName;
        }

        /// <summary>True for packages inside the game's Package folder that are not the game core.</summary>
        public static bool IsLocalPackage(BaseModPackage package)
        {
            var packageRoot = Path.GetFullPath(SpecificPortraits.PackageRoot).TrimEnd(Path.DirectorySeparatorChar);
            var parent = package.dirInfo.Parent?.FullName.TrimEnd(Path.DirectorySeparatorChar);
            return !package.builtin && string.Equals(parent, packageRoot, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Where the pools of a package are saved. Local packages keep their own PSFLinks.json;
        /// the game core and workshop packages are saved in the settings package so an update
        /// cannot wipe them.
        /// </summary>
        public static string GetPath(BaseModPackage package)
        {
            if (IsSettingsPackage(package) || IsLocalPackage(package))
                return Path.Combine(package.dirInfo.FullName, SpecificPortraits.LinkFileName);
            return Path.Combine(SpecificPortraits.SettingsPackagePath, $"PSFLinks.{package.dirInfo.Name}.json");
        }

        static string OverridePath(BaseModPackage package)
        {
            return Path.Combine(SpecificPortraits.SettingsPackagePath, $"PSFLinks.{package.dirInfo.Name}.json");
        }

        public static BaseModPackage FindPackage(string modId)
        {
            return SpecificPortraits.loadedModPackages.FirstOrDefault(p => p.dirInfo.Name == modId);
        }

        public static string Save(BaseModPackage package)
        {
            var poolFile = new PortraitPoolFile
            {
                Version = 3,
                Tags = SpecificPortraits.PortraitPoolTags.Values.Where(t => t.ModID == package.dirInfo.Name).ToList()
            };
            SpecificPortraits.EnsureSettingsPackage();
            var path = GetPath(package);
            File.WriteAllText(path, JsonConvert.SerializeObject(poolFile, Formatting.Indented));
            Dirty.Remove(package.dirInfo.Name);
            SpecificPortraits.Log.LogInfo($"Saved {poolFile.Tags.Count} pools for {package.title} to {path}");
            return path;
        }

        /// <summary>Save every package with unsaved edits. Returns the number saved.</summary>
        public static int SaveAll()
        {
            int saved = 0;
            foreach (var modId in Dirty.ToList())
            {
                var package = FindPackage(modId);
                if (package == null)
                {
                    Dirty.Remove(modId);
                    continue;
                }
                try
                {
                    Save(package);
                    saved++;
                }
                catch (Exception ex)
                {
                    SpecificPortraits.Log.LogError($"Could not save pools for {package.title}: {ex.Message}");
                }
            }
            return saved;
        }

        /// <summary>Throw away unsaved edits by reading every link file again.</summary>
        public static void ReloadAll()
        {
            SpecificPortraits.PortraitPoolTags.Clear();
            Dirty.Clear();
            LoadAll();
        }

        /// <summary>
        /// Read the link files of every loaded package. A package whose pools were moved into the
        /// settings package (PSFLinks.&lt;folder&gt;.json) is read from there only. Old-format files
        /// are converted to the current format once and then ignored.
        /// </summary>
        public static void LoadAll()
        {
            int files = 0;
            foreach (var package in SpecificPortraits.loadedModPackages)
            {
                var dir = package.dirInfo;
                var modId = dir.Name;

                if (!IsSettingsPackage(package) && File.Exists(OverridePath(package)))
                    continue;

                var jsonFiles = dir.GetFiles("PSFLinks*.json");
                var legacyText = dir.GetFiles(SpecificPortraits.LegacyLinkFileName);
                bool hasCurrent = false;
                bool hasLegacy = legacyText.Length > 0;

                foreach (var file in jsonFiles)
                {
                    try
                    {
                        int version = ParseJson(file, modId);
                        files++;
                        if (version == 3)
                            hasCurrent = true;
                        else
                            hasLegacy = true;
                    }
                    catch (Exception ex)
                    {
                        SpecificPortraits.Log.LogError($"Could not read {file.FullName}: {ex.Message}");
                    }
                }

                if (legacyText.Length > 0 && !hasCurrent)
                {
                    try
                    {
                        ParseText(legacyText[0], modId);
                        files++;
                    }
                    catch (Exception ex)
                    {
                        SpecificPortraits.Log.LogError($"Could not read {legacyText[0].FullName}: {ex.Message}");
                    }
                }

                if (hasLegacy && !hasCurrent)
                    Upgrade(package);
            }
            SpecificPortraits.Log.LogInfo($"{SpecificPortraits.PortraitPoolTags.Count} pools from {files} link files");
        }

        static void Upgrade(BaseModPackage package)
        {
            try
            {
                var path = Save(package);
                SpecificPortraits.Log.LogInfo($"Converted the old link file of {package.title} to {path}");
            }
            catch (Exception ex)
            {
                SpecificPortraits.Log.LogWarning($"Could not convert the old link file of {package.title}: {ex.Message}");
            }
        }

        public static PortraitPoolTag AddToPool(PortraitPoolTag tag, IEnumerable<string> portraitIds)
        {
            if (!SpecificPortraits.PortraitPoolTags.TryGetValue(tag.Key, out var pool))
            {
                pool = tag;
                pool.Portraits = new List<string>();
                SpecificPortraits.PortraitPoolTags.Add(tag.Key, pool);
            }
            foreach (var id in portraitIds)
            {
                if (!pool.Portraits.Contains(id))
                    pool.Portraits.Add(id);
            }
            return pool;
        }

        /// <summary>Version 1 text format: "version 1" header, then "portraitId: tag, tag" lines.</summary>
        static void ParseText(FileInfo linkFile, string modID)
        {
            var lines = File.ReadAllLines(linkFile.FullName);
            if (lines.Length == 0 || !lines[0].Trim().StartsWith("version") || !lines[0].Trim().EndsWith("1"))
                return;

            for (int i = 1; i < lines.Length; i++)
            {
                var lineSplit = lines[i].Split(":");
                if (lineSplit.Length < 2)
                    continue;
                var originalId = lineSplit[0].Trim();
                if (!Portrait.Exists(originalId))
                    continue;

                foreach (var tagString in lineSplit[1].Split(",").Select(key => key.Trim()))
                {
                    var tag = SpecificPortraits.ResolvePoolTagFromString(tagString, modID);
                    if (tag != null)
                        AddToPool(tag, new[] { originalId });
                }
            }
        }

        /// <summary>Returns the file's format version (0 when unreadable).</summary>
        static int ParseJson(FileInfo linkFile, string modID)
        {
            PortraitPoolFile poolFile = JsonConvert.DeserializeObject<PortraitPoolFile>(File.ReadAllText(linkFile.FullName));
            if (poolFile == null)
                return 0;

            if (poolFile.Version == 3)
            {
                foreach (var tag in poolFile.Tags)
                {
                    if (string.IsNullOrEmpty(tag.ModID))
                        tag.ModID = modID;
                    AddToPool(tag, tag.Portraits.ToList());
                }
            }
            else if (poolFile.Version == 2)
            {
                foreach (var kv in poolFile.PoolsByPortrait)
                {
                    var spriteID = kv.Key;
                    if (!Portrait.Exists(spriteID))
                        continue;

                    foreach (var tagString in kv.Value)
                    {
                        var tag = SpecificPortraits.ResolvePoolTagFromString(tagString, modID);
                        if (tag != null)
                            AddToPool(tag, new[] { spriteID });
                    }
                }
            }
            return poolFile.Version;
        }
    }
}
