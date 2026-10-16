using System;
using HarmonyLib;
using Ox1;
using Trash;

namespace ModSettings.Patches
{
    [HarmonyPatch(typeof(OptionsUI), "OnEnable")]
    internal static class OptionsEnablePatch
    {
        private static bool _logged;

        private static void Postfix(OptionsUI __instance)
        {
            try
            {
                ModsTabInstaller.Install(__instance);
            }
            catch (Exception e)
            {
                if (!_logged)
                {
                    _logged = true;
                    Plugin.Log.LogError($"Couldn't add the Mods tab; Options is unchanged. {e}");
                }
            }
        }
    }

    [HarmonyPatch(typeof(OptionsUI), "OnDisable")]
    internal static class OptionsDisablePatch
    {
        private static void Postfix(OptionsUI __instance)
        {
            try
            {
                ModsTabInstaller.Closed(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Mods tab close failed. {e}");
            }
        }
    }

    /// <summary>The shoulder buttons don't change tab while a key rebind is listening.</summary>
    [HarmonyPatch(typeof(OptionsUI), "CycleTab")]
    internal static class OptionsCycleTabPatch
    {
        private static bool Prefix() => !KeyRebind.Listening;
    }

    /// <summary>Esc cancels a listening key rebind instead of closing the screen.</summary>
    [HarmonyPatch(typeof(MenuScene), "OnNavigationCancel")]
    internal static class MenuSceneCancelPatch
    {
        private static bool Prefix() => !KeyRebind.ConsumesCancel;
    }
}
