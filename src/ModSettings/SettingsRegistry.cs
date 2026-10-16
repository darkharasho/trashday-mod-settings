using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace ModSettings
{
    internal sealed class ModInfo
    {
        public string Guid;
        public string Name;
        public string Version;
        /// <summary>In config order; <see cref="SettingOrder"/> sorts and groups them for display.</summary>
        public List<SettingInfo> Settings = new List<SettingInfo>();
    }

    internal sealed class SettingInfo
    {
        public ModInfo Mod;
        public ConfigEntryBase Entry;
        public string Section;
        public string Name;
        public string Description;
        public int Order;
        public bool ReadOnly;
        public bool Advanced;
        public bool Hidden;
        public bool RestartRequired;

        public SettingFacts Facts => new SettingFacts { Section = Section, Name = Name, Order = Order, Advanced = Advanced, Hidden = Hidden };
    }

    /// <summary>
    /// Finds the settings of every loaded BepInEx plugin: its <see cref="BaseUnityPlugin.Config"/> plus any other
    /// <see cref="ConfigFile"/> fields on the plugin class. A tag object on a setting's <see cref="ConfigDescription"/>
    /// can refine how it's shown, using ConfigurationManagerAttributes' field names (Browsable, Category, DispName,
    /// Order, ReadOnly, IsAdvanced, Description) plus RestartRequired. Tags are read by name, so mods need no
    /// reference to this one.
    /// </summary>
    internal static class SettingsRegistry
    {
        private const BindingFlags AnyField = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly HashSet<string> LoggedFailures = new HashSet<string>();

        public static List<ModInfo> Collect()
        {
            var mods = new List<ModInfo>();
            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                if (plugin?.Instance == null)
                    continue;
                try
                {
                    var mod = new ModInfo
                    {
                        Guid = plugin.Metadata.GUID,
                        Name = plugin.Metadata.Name,
                        Version = plugin.Metadata.Version.ToString(),
                    };
                    foreach (var file in ConfigFiles(plugin.Instance))
                    {
                        foreach (var entry in ((IDictionary<ConfigDefinition, ConfigEntryBase>)file).Values)
                            mod.Settings.Add(Describe(mod, entry));
                    }
                    if (SettingOrder.AnyVisible(mod.Settings, s => s.Facts))
                        mods.Add(mod);
                }
                catch (Exception e)
                {
                    var guid = plugin.Metadata?.GUID ?? "?";
                    if (LoggedFailures.Add(guid))
                        Plugin.Log.LogWarning($"Couldn't read the settings of {guid}; it's left out of the Mods tab. {e}");
                }
            }
            return mods.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static IEnumerable<ConfigFile> ConfigFiles(BaseUnityPlugin plugin)
        {
            var seen = new HashSet<ConfigFile> { plugin.Config };
            yield return plugin.Config;

            for (var type = plugin.GetType(); type != null && type != typeof(BaseUnityPlugin); type = type.BaseType)
            {
                foreach (var field in type.GetFields(AnyField | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType != typeof(ConfigFile))
                        continue;
                    var target = field.IsStatic ? null : plugin;
                    if (field.GetValue(target) is ConfigFile file && seen.Add(file))
                        yield return file;
                }
            }
        }

        private static SettingInfo Describe(ModInfo mod, ConfigEntryBase entry)
        {
            var setting = new SettingInfo
            {
                Mod = mod,
                Entry = entry,
                Section = entry.Definition.Section,
                Name = Names.Humanize(entry.Definition.Key),
                Description = entry.Description?.Description,
            };

            var tags = entry.Description?.Tags;
            if (tags == null)
                return setting;

            foreach (var tag in tags)
            {
                if (tag == null)
                    continue;
                setting.Hidden |= Read<bool?>(tag, "Browsable") == false;
                setting.Section = Read<string>(tag, "Category") ?? setting.Section;
                setting.Name = Read<string>(tag, "DispName") ?? setting.Name;
                setting.Description = Read<string>(tag, "Description") ?? setting.Description;
                setting.Order = Read<int?>(tag, "Order") ?? setting.Order;
                setting.ReadOnly |= Read<bool?>(tag, "ReadOnly") == true;
                setting.Advanced |= Read<bool?>(tag, "IsAdvanced") == true;
                setting.RestartRequired |= Read<bool?>(tag, "RestartRequired") == true;
            }
            return setting;
        }

        private static T Read<T>(object tag, string name)
        {
            var type = tag.GetType();
            object value;
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
            {
                value = field.GetValue(tag);
            }
            else
            {
                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (property == null || !property.CanRead)
                    return default;
                value = property.GetValue(tag);
            }
            return value is T typed ? typed : default;
        }
    }
}
