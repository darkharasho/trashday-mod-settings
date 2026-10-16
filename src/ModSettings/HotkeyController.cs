using System;
using System.Collections.Generic;
using System.Reflection;
using Ox1;
using Trash;
using UnityEngine;
using UnityEngine.UIElements;

namespace ModSettings
{
    /// <summary>
    /// Per-frame work: key rebinding, batched saves and the open key. The key opens the pause menu's (in a run)
    /// or title screen's Options scene on the Mods tab, or switches to Mods when Options is already open.
    /// </summary>
    internal sealed class HotkeyController : MonoBehaviour
    {
        private static readonly FieldInfo OpenStacks =
            typeof(MenuSceneManager).GetField("_stack", BindingFlags.Static | BindingFlags.NonPublic);

        private static MenuScene _focusSwapped;
        private static string _originalFocusName;

        private bool _loggedError;

        private void Update()
        {
            try
            {
                KeyRebind.Tick();
                ConfigWriter.Tick(Time.unscaledTime);
                if (Hotkey.WasPressed(Plugin.OpenKey.Value))
                    OnOpenKey();
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Plugin.Log.LogError($"Mod settings update failed. {e}");
                }
            }
        }

        private void OnDestroy()
        {
            ConfigWriter.Flush();
            RestoreFocusName();
        }

        private static void OnOpenKey()
        {
            FindHost(out var stack, out var hostScene, out var optionsScene);
            var options = ModsTabInstaller.Active;
            var state = new HotkeyState
            {
                OptionsActive = options != null && options.isActiveAndEnabled,
                TextFieldFocused = TextFieldFocused(options),
                Rebinding = KeyRebind.Listening,
                Loading = LoadingScreenUI.IsVisible,
                MenusSuppressed = MenuSceneManager.MenusSuppressed,
                HasMenuHost = stack != null && optionsScene != null,
                OtherMenuOpen = OtherMenuOpen(stack, hostScene, optionsScene),
            };

            switch (HotkeyGate.Decide(state))
            {
                case HotkeyAction.ShowModsTab:
                    ModsTabInstaller.ShowMods(options);
                    break;
                case HotkeyAction.OpenOptions:
                    Open(stack, optionsScene);
                    break;
            }
        }

        /// <summary>The pause menu in a run, otherwise the title screen.</summary>
        private static void FindHost(out MenuSceneStack stack, out MenuScene hostScene, out MenuScene optionsScene)
        {
            stack = null;
            hostScene = null;
            optionsScene = null;
            var midgame = FindFirstObjectByType<MidgameUI>(FindObjectsInactive.Include);
            if (midgame != null)
            {
                optionsScene = midgame.OptionsScene;
                hostScene = midgame.GetComponentInParent<MenuScene>(true);
                stack = midgame.GetComponentInParent<MenuSceneStack>(true);
                return;
            }
            var title = FindFirstObjectByType<MainMenuUI>(FindObjectsInactive.Include);
            if (title != null)
            {
                optionsScene = title.OptionsScene;
                hostScene = title.GetComponentInParent<MenuScene>(true);
                stack = title.GetComponentInParent<MenuSceneStack>(true);
            }
        }

        /// <summary>Any open menu stack other than the host's, or the host's showing something besides itself or Options.</summary>
        private static bool OtherMenuOpen(MenuSceneStack stack, MenuScene hostScene, MenuScene optionsScene)
        {
            if (OpenStacks?.GetValue(null) is List<MenuSceneStack> open)
            {
                foreach (var other in open)
                {
                    if (other != stack)
                        return true;
                }
            }
            if (stack != null && MenuSceneManager.IsStackOpen(stack) && stack.TryGetActiveScene(out var active))
                return active != hostScene && active != optionsScene;
            return false;
        }

        private static bool TextFieldFocused(OptionsUI options)
        {
            if (options == null)
                return false;
            var focused = OptionsAccess.Ready ? OptionsAccess.Root(options)?.panel?.focusController?.focusedElement as VisualElement : null;
            return focused is TextField || focused?.GetFirstAncestorOfType<TextField>() != null;
        }

        /// <summary>
        /// Points the scene's deferred default focus at the Mods tab button: otherwise MenuScene focuses the Gameplay
        /// tab button a frame later, which switches back to Gameplay. Restored when Options closes.
        /// </summary>
        private static void Open(MenuSceneStack stack, MenuScene optionsScene)
        {
            RestoreFocusName();
            _focusSwapped = optionsScene;
            _originalFocusName = optionsScene.DefaultFocusElementName;
            optionsScene.DefaultFocusElementName = ModsTabInstaller.ButtonName;
            ModsTabInstaller.OpenOnModsPending = true;
            try
            {
                stack.PushActiveScene(optionsScene);
            }
            finally
            {
                ModsTabInstaller.OpenOnModsPending = false;
            }
        }

        internal static void RestoreFocusName()
        {
            if (_focusSwapped == null)
                return;
            _focusSwapped.DefaultFocusElementName = _originalFocusName;
            _focusSwapped = null;
            _originalFocusName = null;
        }
    }
}
