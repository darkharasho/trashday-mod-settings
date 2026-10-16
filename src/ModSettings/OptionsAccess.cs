using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Trash;
using UnityEngine.UIElements;

namespace ModSettings
{
    /// <summary>
    /// Every private member of Trash.OptionsUI the Mods tab uses, looked up once. If any is missing (a game
    /// update), <see cref="Ready"/> stays false and the tab is simply not added.
    /// </summary>
    internal static class OptionsAccess
    {
        private static Type _tabType;
        private static FieldInfo _tabButton;
        private static FieldInfo _tabContent;
        private static FieldInfo _tabReset;
        private static FieldInfo _tabs;
        private static FieldInfo _root;
        private static FieldInfo _currentTab;
        private static FieldInfo _resetButton;
        private static MethodInfo _clickTab;
        private static MethodInfo _tabButtonFocused;
        private static MethodInfo _setNav;

        public static bool Ready { get; private set; }

        public static bool Init()
        {
            var ui = typeof(OptionsUI);
            const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _tabType = ui.GetNestedType("Tab", BindingFlags.NonPublic | BindingFlags.Public);
            _tabButton = _tabType?.GetField("Button", any);
            _tabContent = _tabType?.GetField("Content", any);
            _tabReset = _tabType?.GetField("ResetDefaults", any);
            _tabs = ui.GetField("_tabs", any);
            _root = ui.GetField("_root", any);
            _currentTab = ui.GetField("_currentTab", any);
            _resetButton = ui.GetField("_resetDefaultsBtn", any);
            _clickTab = _tabType == null ? null : ui.GetMethod("ClickTab", any, null, new[] { _tabType }, null);
            _tabButtonFocused = _tabType == null ? null : ui.GetMethod("OnTabButtonFocused", any, null, new[] { _tabType }, null);
            _setNav = ui.GetMethod("SetDirectionalNavForElement", any);

            var missing = new List<string>();
            void Check(object member, string name)
            {
                if (member == null)
                    missing.Add(name);
            }
            Check(_tabType, "Tab");
            Check(_tabButton, "Tab.Button");
            Check(_tabContent, "Tab.Content");
            Check(_tabReset, "Tab.ResetDefaults");
            Check(_tabs, "_tabs");
            Check(_root, "_root");
            Check(_currentTab, "_currentTab");
            Check(_resetButton, "_resetDefaultsBtn");
            Check(_clickTab, "ClickTab");
            Check(_tabButtonFocused, "OnTabButtonFocused");
            Check(_setNav, "SetDirectionalNavForElement");

            Ready = missing.Count == 0;
            if (!Ready)
                Plugin.Log.LogWarning($"The Options screen changed ({string.Join(", ", missing)} not found); the Mods tab is unavailable.");
            return Ready;
        }

        public static VisualElement Root(OptionsUI ui) => (VisualElement)_root.GetValue(ui);

        public static Button ResetButton(OptionsUI ui) => (Button)_resetButton.GetValue(ui);

        /// <summary>A boxed OptionsUI.Tab.</summary>
        public static object MakeTab(Button button, VisualElement content, Action resetDefaults)
        {
            var tab = Activator.CreateInstance(_tabType);
            _tabButton.SetValue(tab, button);
            _tabContent.SetValue(tab, content);
            _tabReset.SetValue(tab, resetDefaults);
            return tab;
        }

        /// <summary>Appends to _tabs unless a tab with the same content is already there.</summary>
        public static void AppendTab(OptionsUI ui, object tab)
        {
            var tabs = (Array)_tabs.GetValue(ui);
            var content = _tabContent.GetValue(tab);
            foreach (var existing in tabs)
            {
                if (ReferenceEquals(_tabContent.GetValue(existing), content))
                    return;
            }
            var bigger = Array.CreateInstance(_tabType, tabs.Length + 1);
            Array.Copy(tabs, bigger, tabs.Length);
            bigger.SetValue(tab, tabs.Length);
            _tabs.SetValue(ui, bigger);
        }

        public static bool IsCurrent(OptionsUI ui, VisualElement content) =>
            ReferenceEquals(_tabContent.GetValue(_currentTab.GetValue(ui)), content);

        public static void ClickTab(OptionsUI ui, object tab) => _clickTab.Invoke(ui, new[] { tab });

        public static void TabButtonFocused(OptionsUI ui, object tab) => _tabButtonFocused.Invoke(ui, new[] { tab });

        public static void SetNav(OptionsUI ui, VisualElement element, NavigationMoveEvent.Direction direction, Func<VisualElement, VisualElement> next) =>
            _setNav.Invoke(ui, new object[] { element, direction, next });
    }
}
