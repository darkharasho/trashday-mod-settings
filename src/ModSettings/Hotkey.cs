using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ModSettings
{
    /// <summary>
    /// Reads a <see cref="KeyboardShortcut"/> straight from the keyboard through the Input System, so it works
    /// whatever the game's own input actions are doing.
    /// </summary>
    internal static class Hotkey
    {
        public static bool WasPressed(KeyboardShortcut shortcut)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || shortcut.MainKey == KeyCode.None)
                return false;
            if (!TryGetKey(shortcut.MainKey, out var main) || !keyboard[main].wasPressedThisFrame)
                return false;
            foreach (var modifier in shortcut.Modifiers)
            {
                if (!TryGetKey(modifier, out var key) || !keyboard[key].isPressed)
                    return false;
            }
            return true;
        }

        public static bool TryGetKey(KeyCode code, out Key key) =>
            Enum.TryParse(KeyNames.ToKeyName(code.ToString()), true, out key) && key != Key.None;

        public static bool TryGetKeyCode(Key key, out KeyCode code) =>
            Enum.TryParse(KeyNames.ToKeyCodeName(key.ToString()), true, out code) && code != KeyCode.None;
    }
}
