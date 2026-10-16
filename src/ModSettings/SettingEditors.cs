using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine.UIElements;

namespace ModSettings
{
    /// <summary>Builds one setting's row with the control <see cref="EditorKinds.Choose"/> picks for it.</summary>
    internal static class SettingEditors
    {
        /// <param name="changed">Called after every successful write, so the tab can refresh all rows.</param>
        /// <returns>The row's refresh: re-reads the entry into the control.</returns>
        public static Action Build(VisualElement parent, SettingInfo setting, VisualElement skinRoot, Action changed)
        {
            var entry = setting.Entry;
            var type = entry.SettingType;
            var acceptable = entry.Description?.AcceptableValues;
            var row = Rows.Row(parent, setting.Name, setting.Description);
            var restart = setting.RestartRequired ? Rows.Note(row, "restart needed") : null;

            Func<object, bool> set = value =>
            {
                if (!ConfigWriter.Set(entry, value))
                {
                    changed();
                    return false;
                }
                if (restart != null)
                    restart.style.display = DisplayStyle.Flex;
                changed();
                return true;
            };

            var listValues = ListValues(acceptable);
            var kind = EditorKinds.Choose(type, type == typeof(KeyboardShortcut),
                listValues != null ? Acceptable.List : IsRange(acceptable) ? Acceptable.Range : Acceptable.None,
                listValues?.Count ?? 0);

            Action refresh;
            switch (kind)
            {
                case EditorKind.Toggle:
                    Rows.Cycle(row, () => (bool)entry.BoxedValue ? "On" : "Off",
                        () => set(!(bool)entry.BoxedValue), () => set(!(bool)entry.BoxedValue), out refresh);
                    break;

                case EditorKind.Cycle:
                {
                    var choices = listValues ?? Enum.GetValues(type).Cast<object>().ToList();
                    Rows.Cycle(row, () => Label(entry.BoxedValue, type),
                        () => set(choices[Picker.Wrap(IndexOf(choices, entry.BoxedValue) + 1, choices.Count)]),
                        () => set(choices[Picker.Wrap(IndexOf(choices, entry.BoxedValue) - 1, choices.Count)]),
                        out refresh);
                    break;
                }

                case EditorKind.Dropdown:
                {
                    var choices = listValues ?? Enum.GetValues(type).Cast<object>().ToList();
                    var labels = choices.Select(c => Label(c, type)).ToList();
                    Rows.Dropdown(row, labels, () => IndexOf(choices, entry.BoxedValue), () => Label(entry.BoxedValue, type),
                        i => set(choices[i]), skinRoot, out refresh);
                    break;
                }

                case EditorKind.SliderInt:
                case EditorKind.Slider:
                {
                    var (min, max) = Range(acceptable);
                    var integer = kind == EditorKind.SliderInt;
                    Rows.Slider(row, Convert.ToSingle(min, CultureInfo.InvariantCulture), Convert.ToSingle(max, CultureInfo.InvariantCulture), integer,
                        () => Convert.ToSingle(entry.BoxedValue, CultureInfo.InvariantCulture),
                        v => set(Convert.ChangeType(integer ? ValueParsing.SnapInteger(v) : v, type, CultureInfo.InvariantCulture)),
                        out refresh);
                    break;
                }

                case EditorKind.NumberText:
                    Rows.Text(row, () => ValueParsing.Format(entry.BoxedValue),
                        text => ValueParsing.TryParseNumber(text, type, out var value) && set(value), out refresh);
                    break;

                case EditorKind.Text:
                    Rows.Text(row, () => (string)entry.BoxedValue ?? "", text => set(text), out refresh);
                    break;

                case EditorKind.Key:
                    KeyRebind.Create(row, entry, set, out refresh);
                    break;

                default:
                    Rows.ReadOnly(row, () => $"{entry.GetSerializedValue()}  (edit in the config file)", out refresh);
                    break;
            }

            if (setting.ReadOnly)
                row.SetEnabled(false);
            return refresh;
        }

        /// <summary>A failed row: the setting's name and "can't edit here".</summary>
        public static void BuildBroken(VisualElement parent, SettingInfo setting)
        {
            var row = Rows.Row(parent, setting.Name, setting.Description);
            Rows.ReadOnly(row, () => "can't edit here", out _);
        }

        private static List<object> ListValues(AcceptableValueBase acceptable)
        {
            var type = acceptable?.GetType();
            if (type == null || !type.IsGenericType || type.GetGenericTypeDefinition() != typeof(AcceptableValueList<>))
                return null;
            return ((Array)type.GetProperty("AcceptableValues").GetValue(acceptable)).Cast<object>().ToList();
        }

        private static bool IsRange(AcceptableValueBase acceptable)
        {
            var type = acceptable?.GetType();
            return type != null && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AcceptableValueRange<>);
        }

        private static (object Min, object Max) Range(AcceptableValueBase acceptable)
        {
            var type = acceptable.GetType();
            return (type.GetProperty("MinValue").GetValue(acceptable), type.GetProperty("MaxValue").GetValue(acceptable));
        }

        private static int IndexOf(List<object> choices, object value)
        {
            for (var i = 0; i < choices.Count; i++)
            {
                if (Equals(choices[i], value))
                    return i;
            }
            return -1;
        }

        private static string Label(object value, Type type) =>
            type.IsEnum ? Names.Humanize(value.ToString()) : TomlTypeConverter.ConvertToString(value, type);
    }
}
