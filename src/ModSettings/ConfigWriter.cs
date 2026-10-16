using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace ModSettings
{
    /// <summary>
    /// Writes settings through <see cref="ConfigEntryBase.BoxedValue"/>, so the owning mod gets its normal
    /// SettingChanged event immediately. Saving is batched (dragging a slider would otherwise rewrite the file on
    /// every step): within a second of the first change, and on <see cref="Flush"/> when Options closes.
    /// </summary>
    internal static class ConfigWriter
    {
        private static readonly HashSet<ConfigFile> Dirty = new HashSet<ConfigFile>();
        private static readonly HashSet<string> LoggedSaveFailures = new HashSet<string>();
        private static readonly SaveSchedule Schedule = new SaveSchedule();

        /// <returns>False if the entry rejected the value (the error is logged).</returns>
        public static bool Set(ConfigEntryBase entry, object value)
        {
            var file = entry.ConfigFile;
            var saveOnSet = file.SaveOnConfigSet;
            try
            {
                file.SaveOnConfigSet = false;
                entry.BoxedValue = value;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Couldn't set {entry.Definition} to '{value}': {e}");
                return false;
            }
            finally
            {
                file.SaveOnConfigSet = saveOnSet;
            }
            // Mods that turned SaveOnConfigSet off save on their own terms.
            if (saveOnSet)
            {
                Dirty.Add(file);
                Schedule.MarkDirty(Time.unscaledTime);
            }
            return true;
        }

        public static void Tick(float now)
        {
            if (Schedule.ShouldSave(now))
                Save(now);
        }

        public static void Flush() => Save(Time.unscaledTime);

        private static void Save(float now)
        {
            Schedule.Saved();
            var failed = new List<ConfigFile>();
            foreach (var file in Dirty)
            {
                try
                {
                    file.Save();
                    LoggedSaveFailures.Remove(file.ConfigFilePath);
                }
                catch (Exception e)
                {
                    failed.Add(file);
                    if (LoggedSaveFailures.Add(file.ConfigFilePath))
                        Plugin.Log.LogError($"Couldn't save {file.ConfigFilePath}; retrying. {e}");
                }
            }
            Dirty.Clear();
            foreach (var file in failed)
            {
                Dirty.Add(file);
                Schedule.MarkDirty(now);
            }
        }
    }
}
