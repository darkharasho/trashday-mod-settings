using System.Collections.Generic;

namespace ModSettings
{
    internal static class Names
    {
        /// <summary>"CounterOffsetX" becomes "Counter Offset X"; keys that already have spaces are left alone.</summary>
        public static string Humanize(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Contains(" "))
                return key;

            var chars = new List<char>(key.Length + 8);
            for (var i = 0; i < key.Length; i++)
            {
                var c = key[i];
                if (c == '_')
                {
                    chars.Add(' ');
                    continue;
                }
                if (i > 0 && char.IsUpper(c) && key[i - 1] != '_'
                    && (char.IsLower(key[i - 1]) || (i + 1 < key.Length && char.IsLower(key[i + 1]) && char.IsUpper(key[i - 1]))))
                    chars.Add(' ');
                chars.Add(c);
            }
            return new string(chars.ToArray());
        }
    }
}
