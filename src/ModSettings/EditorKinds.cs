using System;

namespace ModSettings
{
    internal enum EditorKind
    {
        Toggle,
        Cycle,
        Dropdown,
        SliderInt,
        Slider,
        NumberText,
        Text,
        Key,
        ReadOnly,
    }

    /// <summary>The kind of AcceptableValueBase a setting declares.</summary>
    internal enum Acceptable
    {
        None,
        List,
        Range,
    }

    /// <summary>Picks the control for a setting from its type and acceptable values (the spec's control table).</summary>
    internal static class EditorKinds
    {
        public const int MaxCycleChoices = 6;

        private static readonly Type[] Integers =
            { typeof(int), typeof(long), typeof(short), typeof(byte), typeof(sbyte), typeof(uint), typeof(ushort), typeof(ulong) };

        private static readonly Type[] Reals = { typeof(float), typeof(double), typeof(decimal) };

        public static bool IsInteger(Type type) => Array.IndexOf(Integers, type) >= 0;

        public static bool IsReal(Type type) => Array.IndexOf(Reals, type) >= 0;

        /// <param name="isShortcut">The type is BepInEx's KeyboardShortcut (not referenced here so this stays testable).</param>
        /// <param name="listCount">Number of values when <paramref name="acceptable"/> is List.</param>
        public static EditorKind Choose(Type type, bool isShortcut, Acceptable acceptable, int listCount)
        {
            if (type == typeof(bool))
                return EditorKind.Toggle;
            if (isShortcut)
                return EditorKind.Key;
            if (acceptable == Acceptable.List)
                return listCount > MaxCycleChoices ? EditorKind.Dropdown : EditorKind.Cycle;
            if (type.IsEnum)
            {
                if (type.IsDefined(typeof(FlagsAttribute), false))
                    return EditorKind.ReadOnly;
                return Enum.GetValues(type).Length > MaxCycleChoices ? EditorKind.Dropdown : EditorKind.Cycle;
            }
            if (acceptable == Acceptable.Range)
            {
                if (IsInteger(type))
                    return EditorKind.SliderInt;
                if (IsReal(type))
                    return EditorKind.Slider;
            }
            if (IsInteger(type) || IsReal(type))
                return EditorKind.NumberText;
            if (type == typeof(string))
                return EditorKind.Text;
            return EditorKind.ReadOnly;
        }
    }
}
