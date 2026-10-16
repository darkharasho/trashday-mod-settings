using System;

namespace ModSettings
{
    /// <summary>
    /// Converts between Unity <c>KeyCode</c> names (what BepInEx's KeyboardShortcut stores) and Input System
    /// <c>Key</c> names (what the game's keyboard reports). Most names match; these are the ones that don't.
    /// </summary>
    internal static class KeyNames
    {
        private static readonly (string KeyCode, string Key)[] Renamed =
        {
            ("Return", "Enter"),
            ("KeypadEnter", "NumpadEnter"),
            ("KeypadPlus", "NumpadPlus"),
            ("KeypadMinus", "NumpadMinus"),
            ("KeypadMultiply", "NumpadMultiply"),
            ("KeypadDivide", "NumpadDivide"),
            ("KeypadPeriod", "NumpadPeriod"),
            ("KeypadEquals", "NumpadEquals"),
            ("LeftControl", "LeftCtrl"),
            ("RightControl", "RightCtrl"),
            ("LeftCommand", "LeftMeta"),
            ("RightCommand", "RightMeta"),
            ("BackQuote", "Backquote"),
            ("Print", "PrintScreen"),
            ("Numlock", "NumLock"),
            ("Menu", "ContextMenu"),
        };

        private static readonly string[] Modifiers =
        {
            "LeftControl", "RightControl", "LeftShift", "RightShift", "LeftAlt", "RightAlt", "LeftCommand", "RightCommand", "AltGr",
        };

        public static string ToKeyName(string keyCode)
        {
            foreach (var (code, key) in Renamed)
            {
                if (code == keyCode)
                    return key;
            }
            if (keyCode.StartsWith("Alpha", StringComparison.Ordinal))
                return "Digit" + keyCode.Substring("Alpha".Length);
            if (keyCode.StartsWith("Keypad", StringComparison.Ordinal))
                return "Numpad" + keyCode.Substring("Keypad".Length);
            return keyCode;
        }

        public static string ToKeyCodeName(string key)
        {
            foreach (var (code, name) in Renamed)
            {
                if (name == key)
                    return code;
            }
            if (key.StartsWith("Digit", StringComparison.Ordinal))
                return "Alpha" + key.Substring("Digit".Length);
            if (key.StartsWith("Numpad", StringComparison.Ordinal))
                return "Keypad" + key.Substring("Numpad".Length);
            return key;
        }

        public static bool IsModifier(string keyCode) => Array.IndexOf(Modifiers, keyCode) >= 0;
    }
}
