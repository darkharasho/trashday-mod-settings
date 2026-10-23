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
        public const string PluginVersion = "0.1.1";

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

            OptionsAccess.Init();

            // Its own object, so it outlives scene loads and doesn't depend on BepInEx's manager object staying alive.
            var host = new GameObject("ModSettings");
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<HotkeyController>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded; press {OpenKey.Value} for the Mods tab");
        }

        private void OnDestroy() => _harmony?.UnpatchSelf();
    }
}
