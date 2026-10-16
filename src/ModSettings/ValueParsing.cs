using System;
using System.Globalization;

namespace ModSettings
{
    /// <summary>Number parsing and formatting for typed and slider settings. Always the invariant culture, like BepInEx's config files.</summary>
    internal static class ValueParsing
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static bool TryParseNumber(string text, Type type, out object value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            text = text.Trim();

            const NumberStyles integer = NumberStyles.Integer;
            const NumberStyles real = NumberStyles.Float;
            if (type == typeof(int)) return Wrap(int.TryParse(text, integer, Inv, out var i), i, out value);
            if (type == typeof(long)) return Wrap(long.TryParse(text, integer, Inv, out var l), l, out value);
            if (type == typeof(short)) return Wrap(short.TryParse(text, integer, Inv, out var s), s, out value);
            if (type == typeof(byte)) return Wrap(byte.TryParse(text, integer, Inv, out var b), b, out value);
            if (type == typeof(sbyte)) return Wrap(sbyte.TryParse(text, integer, Inv, out var sb), sb, out value);
            if (type == typeof(uint)) return Wrap(uint.TryParse(text, integer, Inv, out var ui), ui, out value);
            if (type == typeof(ushort)) return Wrap(ushort.TryParse(text, integer, Inv, out var us), us, out value);
            if (type == typeof(ulong)) return Wrap(ulong.TryParse(text, integer, Inv, out var ul), ul, out value);
            if (type == typeof(float))
                return Wrap(float.TryParse(text, real, Inv, out var f) && !float.IsNaN(f) && !float.IsInfinity(f), f, out value);
            if (type == typeof(double))
                return Wrap(double.TryParse(text, real, Inv, out var d) && !double.IsNaN(d) && !double.IsInfinity(d), d, out value);
            if (type == typeof(decimal))
                return Wrap(decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowExponent, Inv, out var m), m, out value);
            return false;
        }

        private static bool Wrap<T>(bool ok, T parsed, out object value)
        {
            value = ok ? (object)parsed : null;
            return ok;
        }

        /// <summary>Inclusive range check; the three values share the setting's type.</summary>
        public static bool InRange(object value, object min, object max)
        {
            var comparable = (IComparable)value;
            return comparable.CompareTo(min) >= 0 && comparable.CompareTo(max) <= 0;
        }

        public static double SnapInteger(double value) => Math.Round(value, MidpointRounding.AwayFromZero);

        /// <summary>Rounds to a step that suits the range (1/100 of its order of magnitude) so files don't fill with float noise.</summary>
        public static float RoundToStep(float value, float low, float high)
        {
            var step = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(high - low, 1e-6))) - 2);
            var rounded = Math.Round(value / step) * step;
            return (float)Math.Min(high, Math.Max(low, rounded));
        }

        public static string Format(object value) =>
            value is IFormattable formattable ? formattable.ToString(null, Inv) : value?.ToString() ?? "";

        public static string FormatSlider(float value, bool integer) => value.ToString(integer ? "0" : "0.##", Inv);
    }
}
