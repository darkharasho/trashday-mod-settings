using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Trash;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ModSettings
{
    /// <summary>
    /// Rebind button for a <see cref="KeyboardShortcut"/>: click, then press a key with any modifiers held.
    /// Esc cancels and Backspace clears. Keys are polled from the Input System keyboard in <see cref="Tick"/>.
    /// While listening, Patches keep Esc from closing Options and the shoulder buttons from changing tab.
    /// </summary>
    internal static class KeyRebind
    {
        private static Button _listening;
        private static Func<object, bool> _set;
        private static Action _restore;
        private static int _startFrame;
        private static int _cancelledFrame = -10;

        public static bool Listening => _listening != null;

        /// <summary>True while listening, and on the frame after one ends, so that frame's Esc/cancel isn't seen by Options too.</summary>
        public static bool ConsumesCancel => _listening != null || Time.frameCount - _cancelledFrame <= 1;

        public static Button Create(SettingRow row, ConfigEntryBase entry, Func<object, bool> set, out Action refresh)
        {
            var control = new VisualElement();
            control.AddToClassList("cycle-control");
            var button = new Button();
            button.AddToClassList("options-text");
            button.AddToClassList("cycle-btn");
            button.AddToClassList("cycle-btn-single");

            Action update = () =>
            {
                if (_listening == button)
                    return;
                var shortcut = (KeyboardShortcut)entry.BoxedValue;
                button.text = shortcut.MainKey == KeyCode.None ? "Not bound" : shortcut.ToString();
            };
            button.clicked += () =>
            {
                if (_listening == button)
                    return;
                Cancel();
                _listening = button;
                _set = set;
                _restore = update;
                _startFrame = Time.frameCount;
                button.text = "Press a key…";
            };
            button.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (_listening == button)
                    Cancel();
            });

            control.Add(button);
            row.Add(control);
            update();
            refresh = update;
            return button;
        }

        public static void Cancel()
        {
            if (_listening == null)
                return;
            var restore = _restore;
            End();
            restore?.Invoke();
        }

        public static void Tick()
        {
            if (_listening == null)
                return;
            if (_listening.panel == null)
            {
                Cancel();
                return;
            }
            var keyboard = Keyboard.current;
            // Skip the frame the button was pressed, so the Enter/Space that pressed it isn't bound.
            if (keyboard == null || Time.frameCount == _startFrame)
                return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Cancel();
                return;
            }

            foreach (var key in keyboard.allKeys)
            {
                if (key == null || !key.wasPressedThisFrame)
                    continue;
                if (!Hotkey.TryGetKeyCode(key.keyCode, out var code) || KeyNames.IsModifier(code.ToString()))
                    continue;

                var set = _set;
                var restore = _restore;
                End();
                set(code == KeyCode.Backspace ? KeyboardShortcut.Empty : new KeyboardShortcut(code, HeldModifiers(keyboard)));
                restore();
                return;
            }
        }

        private static void End()
        {
            _listening = null;
            _set = null;
            _restore = null;
            _cancelledFrame = Time.frameCount;
        }

        private static KeyCode[] HeldModifiers(Keyboard keyboard)
        {
            var modifiers = new List<KeyCode>();
            if (keyboard.leftCtrlKey.isPressed) modifiers.Add(KeyCode.LeftControl);
            if (keyboard.rightCtrlKey.isPressed) modifiers.Add(KeyCode.RightControl);
            if (keyboard.leftShiftKey.isPressed) modifiers.Add(KeyCode.LeftShift);
            if (keyboard.rightShiftKey.isPressed) modifiers.Add(KeyCode.RightShift);
            if (keyboard.leftAltKey.isPressed) modifiers.Add(KeyCode.LeftAlt);
            if (keyboard.rightAltKey.isPressed) modifiers.Add(KeyCode.RightAlt);
            if (keyboard.leftMetaKey.isPressed) modifiers.Add(KeyCode.LeftCommand);
            if (keyboard.rightMetaKey.isPressed) modifiers.Add(KeyCode.RightCommand);
            return modifiers.ToArray();
        }
    }
}
