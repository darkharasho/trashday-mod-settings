using System;
using System.Linq;
using Trash;
using UnityEngine.UIElements;

namespace ModSettings
{
    /// <summary>
    /// Adds the Mods tab to an Options screen right after OptionsUI.OnEnable rebuilds it. The button and content are
    /// created once per Options document and found by name afterwards; the tab is re-appended to the freshly built
    /// _tabs array each time, and hidden, because OnEnable only resets the game's own four tabs.
    /// </summary>
    internal static class ModsTabInstaller
    {
        public const string ButtonName = "ModsTabBtn";
        public const string ContentName = "ModsTab";

        private static bool _warned;

        /// <summary>The enabled Options screen, if any.</summary>
        public static OptionsUI Active { get; private set; }

        /// <summary>Set by the hotkey around PushActiveScene so the tab opens on Mods.</summary>
        public static bool OpenOnModsPending;

        public static void Install(OptionsUI ui)
        {
            Active = ui;
            if (!OptionsAccess.Ready)
                return;

            var root = OptionsAccess.Root(ui);
            var contentArea = root?.Q<VisualElement>("OptionsPanel")?.Q<VisualElement>(null, "content-area") ?? root;
            var keybindsButton = root?.Q<Button>("KeybindsTabBtn");
            var audioTab = contentArea?.Q<VisualElement>("AudioTab");
            if (root == null || keybindsButton == null || audioTab == null)
            {
                if (!_warned)
                {
                    _warned = true;
                    Plugin.Log.LogWarning("The Options screen layout changed (KeybindsTabBtn or AudioTab not found); the Mods tab is unavailable.");
                }
                return;
            }

            var button = root.Q<Button>(ButtonName) ?? CreateButton(ui, keybindsButton);
            var content = contentArea.Q<VisualElement>(ContentName) ?? CreateContent(audioTab);
            if (button.userData == null)
            {
                button.userData = OptionsAccess.MakeTab(button, content, () => (content.userData as ModsTab)?.ResetSelectedMod());
                WireButton(ui, button, content);
            }
            var tab = button.userData;
            OptionsAccess.AppendTab(ui, tab);

            // OnEnable reset only the game's tabs; ours could still be showing from last time.
            content.style.display = DisplayStyle.None;
            button.RemoveFromClassList("tab-button--active");
            button.parent?.RemoveFromClassList("tab-button-container--active");

            if (!(content.userData is ModsTab model))
            {
                var scroll = content.Q<ScrollView>();
                model = new ModsTab(scroll, root, (first, last) =>
                {
                    if (first != null)
                        OptionsAccess.SetNav(ui, first, NavigationMoveEvent.Direction.Up, _ => button);
                    if (last != null)
                        OptionsAccess.SetNav(ui, last, NavigationMoveEvent.Direction.Down, _ => OptionsAccess.ResetButton(ui));
                });
                content.userData = model;
            }
            model.Rebuild();

            if (OpenOnModsPending)
            {
                OpenOnModsPending = false;
                OptionsAccess.ClickTab(ui, tab);
            }
        }

        public static void Closed(OptionsUI ui)
        {
            KeyRebind.Cancel();
            ConfigWriter.Flush();
            HotkeyController.RestoreFocusName();
            if (Active == ui)
                Active = null;
        }

        public static void ShowMods(OptionsUI ui)
        {
            var tab = OptionsAccess.Root(ui)?.Q<Button>(ButtonName)?.userData;
            if (tab != null)
                OptionsAccess.ClickTab(ui, tab);
        }

        private static Button CreateButton(OptionsUI ui, Button template)
        {
            var button = new Button { name = ButtonName };
            CopyClasses(template, button, "tab-button--active");
            if (!string.IsNullOrEmpty(template.text))
            {
                button.text = "Mods";
            }
            else
            {
                // The game's tab buttons may draw their caption with a child label instead of Button.text.
                var templateLabel = template.Q<Label>();
                var label = new Label("Mods");
                if (templateLabel != null)
                    CopyClasses(templateLabel, label);
                button.Add(label);
            }

            var container = template.parent;
            var ownContainer = container != null && container.parent != null && container.Query<Button>().ToList().Count == 1;
            if (ownContainer)
            {
                var newContainer = new VisualElement();
                CopyClasses(container, newContainer, "tab-button-container--active", "tab-button-container--focused");
                newContainer.Add(button);
                container.parent.Insert(container.parent.IndexOf(container) + 1, newContainer);
            }
            else if (container != null)
            {
                container.Insert(container.IndexOf(template) + 1, button);
            }
            return button;
        }

        private static void WireButton(OptionsUI ui, Button button, VisualElement content)
        {
            button.clicked += () => OptionsAccess.ClickTab(ui, button.userData);
            button.RegisterCallback<FocusInEvent>(_ =>
            {
                OptionsAccess.TabButtonFocused(ui, button.userData);
                button.parent?.AddToClassList("tab-button-container--focused");
            });
            button.RegisterCallback<FocusOutEvent>(_ => button.parent?.RemoveFromClassList("tab-button-container--focused"));
            OptionsAccess.SetNav(ui, button, NavigationMoveEvent.Direction.Up, _ => Focusables(content).LastOrDefault());
            OptionsAccess.SetNav(ui, button, NavigationMoveEvent.Direction.Down, _ => Focusables(content).FirstOrDefault());
        }

        private static VisualElement CreateContent(VisualElement template)
        {
            var content = new VisualElement { name = ContentName };
            CopyClasses(template, content);
            content.style.display = DisplayStyle.None;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.style.flexGrow = 1f;
            // The scrollers' sliders are focusable fields; left so, they'd be the tab's "last focusable" for the
            // game's Up/Down navigation. The view is still empty here, so everything inside it is scroller parts.
            foreach (var part in scroll.Query<VisualElement>().ToList())
            {
                if (part != scroll && part != scroll.contentContainer)
                    part.focusable = false;
            }
            // Keep the focused row on screen when moving with a controller or the keyboard.
            scroll.RegisterCallback<FocusInEvent>(e =>
            {
                if (e.target is VisualElement target && target != scroll && scroll.contentContainer.Contains(target))
                    scroll.ScrollTo(target);
            }, TrickleDown.TrickleDown);
            content.Add(scroll);

            template.parent.Insert(template.parent.IndexOf(template) + 1, content);
            return content;
        }

        private static System.Collections.Generic.List<VisualElement> Focusables(VisualElement container) =>
            container.Query<VisualElement>().Where(v => v.focusable && v.canGrabFocus).ToList();

        /// <summary>Copies USS classes except Unity's own (unity-*) and any listed state classes.</summary>
        private static void CopyClasses(VisualElement from, VisualElement to, params string[] except)
        {
            foreach (var cls in from.GetClasses())
            {
                if (cls.StartsWith("unity-", StringComparison.Ordinal) || Array.IndexOf(except, cls) >= 0)
                    continue;
                to.AddToClassList(cls);
            }
        }
    }
}
