using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSettings
{
    /// <summary>What ordering and grouping need to know about one setting.</summary>
    internal struct SettingFacts
    {
        public string Section;
        public string Name;
        public int Order;
        public bool Advanced;
        public bool Hidden;
    }

    internal sealed class SettingGroup<T>
    {
        public string Heading;
        public bool IsAdvanced;
        public List<T> Items = new List<T>();
    }

    internal static class SettingOrder
    {
        public const string AdvancedHeading = "Advanced";

        /// <summary>
        /// Sections in the order they first appear (config order); inside each, higher Order first, then name.
        /// Advanced settings from every section are gathered into one last group. Hidden ones are dropped.
        /// </summary>
        public static List<SettingGroup<T>> Group<T>(IEnumerable<T> items, Func<T, SettingFacts> facts)
        {
            var groups = new List<SettingGroup<T>>();
            var bySection = new Dictionary<string, SettingGroup<T>>(StringComparer.Ordinal);
            var advanced = new SettingGroup<T> { Heading = AdvancedHeading, IsAdvanced = true };

            foreach (var item in items)
            {
                var f = facts(item);
                if (f.Hidden)
                    continue;
                if (f.Advanced)
                {
                    advanced.Items.Add(item);
                    continue;
                }
                var section = f.Section ?? "";
                if (!bySection.TryGetValue(section, out var group))
                {
                    group = new SettingGroup<T> { Heading = section };
                    bySection.Add(section, group);
                    groups.Add(group);
                }
                group.Items.Add(item);
            }

            if (advanced.Items.Count > 0)
                groups.Add(advanced);
            foreach (var group in groups)
            {
                group.Items = group.Items
                    .OrderByDescending(i => facts(i).Order)
                    .ThenBy(i => facts(i).Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            return groups;
        }

        public static bool AnyVisible<T>(IEnumerable<T> items, Func<T, SettingFacts> facts) => items.Any(i => !facts(i).Hidden);
    }
}
