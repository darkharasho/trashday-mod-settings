using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ModSettings
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.darkharasho.trashday.modsettings";
        public const string PluginName = "ModSettings";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyboardShortcut> OpenKey;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            OpenKey = Config.Bind("General", "OpenKey", new KeyboardShortcut(KeyCode.F10), new ConfigDescription(
                "Opens the game's Options screen on the Mods tab.",
                null,
                new ConfigurationManagerAttributes { DispName = "Open key" }));

            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll();
            }
            catch (Exception e)
            {
                Log.LogError($"Patching failed, likely a game update; the Mods tab is unavailable. {e}");
                _harmony.UnpatchSelf();
                return;
            }

            Log.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void OnDestroy() => _harmony?.UnpatchSelf();
    }
}
