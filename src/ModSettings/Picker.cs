using System;
using System.Collections.Generic;

namespace ModSettings
{
    /// <summary>The Mods tab's mod selector: wrap-around stepping and restoring the session's last pick.</summary>
    internal static class Picker
    {
        public static int Wrap(int index, int count) => count <= 0 ? -1 : ((index % count) + count) % count;

        public static int IndexOf(IReadOnlyList<string> ids, string remembered)
        {
            if (ids.Count == 0)
                return -1;
            for (var i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], remembered, StringComparison.Ordinal))
                    return i;
            }
            return 0;
        }
    }
}
