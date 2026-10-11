using BepInEx.Configuration;
using EvilMask.Elin.ModOptions;
using EvilMask.Elin.ModOptions.UI;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SpecificPortraits
{
    /// <summary>
    /// Shows the settings in the Mod Options menu when that mod is installed.
    /// ModOptions.dll is a build-only reference; every call touching its types sits
    /// behind the Present() check, so the mod runs without it.
    /// </summary>
    internal static class ModOptionsIntegration
    {
        internal static void TryRegister()
        {
            try
            {
                if (!Present())
                {
                    SpecificPortraits.Log.LogInfo("Mod Options not installed; settings are in the BepInEx config and the Portrait Tools window.");
                    return;
                }
                Register();
            }
            catch (Exception ex)
            {
                SpecificPortraits.Log.LogWarning("Mod Options integration skipped: " + ex.Message);
            }
        }

        static bool Present()
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a.GetName().Name == "ModOptions")
                    return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Register()
        {
            ModOptionController c = ModOptionController.Register(SpecificPortraits.pluginGuid, "psf.tip");
            if (c == null)
                return;
            c.SetTranslation(SpecificPortraits.pluginGuid, "Portrait Specification Framework");
            c.SetTranslation("psf.tip", "Portrait pools: which characters get which portraits.");
            c.OnBuildUI += delegate (OptionUIBuilder builder)
            {
                try
                {
                    Build(builder);
                }
                catch (Exception ex)
                {
                    SpecificPortraits.Log.LogError("Mod Options UI build failed: " + ex);
                }
            };
            SpecificPortraits.Log.LogInfo("Mod Options integration registered.");
        }

        static void Build(OptionUIBuilder builder)
        {
            OptVLayout root = builder.Root;

            OptVLayout pools = root.AddVLayoutWithBorder("Portraits");
            Toggle(pools, "Always include vanilla portraits", SpecificPortraits.AlwaysIncludeVanillaPortraits,
                "Mix the game's default portraits into every pool.\nOff: defaults are only used when no pool matches.");
            Toggle(pools, "Random portrait when no pool matches", SpecificPortraits.RandomizePortraitOnNoResults,
                "Characters with no matching pool get a random portrait\nof their gender and type.\nOff: they keep the game's own choice.");
            Toggle(pools, "Reroll line in dialogue", SpecificPortraits.EnableRerollDialog,
                "Adds \"Reroll your portrait\" to NPC dialogue.\nNot shown for unique characters.");

            OptVLayout tools = root.AddVLayoutWithBorder("Tools");
            OptButton open = tools.AddButton("Open Portrait Tools",
                "Portrait picker, pool editor and settings.\nNeeds a loaded game and the YK Framework mod.");
            open.OnClicked += OpenTools;

            root.AddText(" ", (TextAnchor)4, 20);
        }

        static void Toggle(OptLayout parent, string label, ConfigEntry<bool> cfg, string tip)
        {
            OptToggle t = parent.AddToggle(label, cfg.Value, 16, tip);
            t.OnValueChanged += delegate (bool v)
            {
                cfg.Value = v;
            };
        }

        static void OpenTools()
        {
            if (SpecificPortraits.CurrentMap == null)
            {
                Msg.SayRaw(Lang.isJP ? "ゲームをロードしてから開いてください。" : "Load a game first, then open Portrait Tools.");
                return;
            }
            try
            {
                PortraitPatch.OpenToolsWindow();
            }
            catch (Exception ex)
            {
                SpecificPortraits.Log.LogWarning("Portrait Tools could not open: " + ex.Message);
                Msg.SayRaw(Lang.isJP ? "ポートレートツールには YK Framework が必要です。" : "Portrait Tools needs the YK Framework mod.");
            }
        }
    }
}
