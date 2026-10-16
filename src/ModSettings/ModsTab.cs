using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace ModSettings
{
    /// <summary>
    /// The Mods tab's contents: a "Mod" picker, then the selected mod's settings grouped by section, with
    /// advanced ones last. Rebuilt every time Options opens; the picked mod is remembered for the session.
    /// </summary>
    internal sealed class ModsTab
    {
        private static string _rememberedGuid;
        private static readonly HashSet<string> LoggedRows = new HashSet<string>();

        private readonly VisualElement _container;
        private readonly VisualElement _skinRoot;
        private readonly Action<VisualElement, VisualElement> _wireEdges;
        private readonly List<Action> _refreshers = new List<Action>();
        private List<ModInfo> _mods = new List<ModInfo>();
        private ModInfo _selected;
        private Button _picker;

        /// <param name="container">Where rows go (the tab's scroll view).</param>
        /// <param name="skinRoot">The Options document root, whose style sheets open dropdowns borrow.</param>
        /// <param name="wireEdges">Called after each render with the first and last focusable rows (null when empty).</param>
        public ModsTab(VisualElement container, VisualElement skinRoot, Action<VisualElement, VisualElement> wireEdges)
        {
            _container = container;
            _skinRoot = skinRoot;
            _wireEdges = wireEdges;
        }

        public void Rebuild()
        {
            _mods = SettingsRegistry.Collect();
            Render();
        }

        /// <summary>Reset Defaults on the Mods tab: every editable setting of the selected mod.</summary>
        public void ResetSelectedMod()
        {
            if (_selected == null)
                return;
            foreach (var setting in _selected.Settings)
            {
                if (!setting.Hidden && !setting.ReadOnly)
                    ConfigWriter.Set(setting.Entry, setting.Entry.DefaultValue);
            }
            RefreshAll();
        }

        private void Select(int index)
        {
            _rememberedGuid = _mods[Picker.Wrap(index, _mods.Count)].Guid;
            Render();
            _picker?.Focus();
        }

        private void Render()
        {
            KeyRebind.Cancel();
            _container.Clear();
            _refreshers.Clear();
            _picker = null;
            _selected = null;

            if (_mods.Count == 0)
            {
                Rows.Message(_container, "No installed mods have settings.");
                _wireEdges(null, null);
                return;
            }

            var index = Picker.IndexOf(_mods.Select(m => m.Guid).ToList(), _rememberedGuid);
            _selected = _mods[index];
            _rememberedGuid = _selected.Guid;

            var pickerRow = Rows.Row("Mod", null);
            var mod = _selected;
            _picker = Rows.Cycle(pickerRow, () => $"{mod.Name}  {mod.Version}", () => Select(index + 1), () => Select(index - 1), out _);
            _container.Add(pickerRow);

            foreach (var group in SettingOrder.Group(_selected.Settings, s => s.Facts))
            {
                Rows.Heading(_container, group.Heading);
                foreach (var setting in group.Items)
                    _refreshers.Add(BuildRow(setting));
            }

            var last = _container.Query<VisualElement>().Where(v => v.focusable && v.canGrabFocus).ToList().LastOrDefault();
            _wireEdges(_picker, last ?? _picker);
        }

        private Action BuildRow(SettingInfo setting)
        {
            var before = _container.childCount;
            try
            {
                return SettingEditors.Build(_container, setting, _skinRoot, RefreshAll);
            }
            catch (Exception e)
            {
                // Drop whatever the failed build added, then show a placeholder row.
                while (_container.childCount > before)
                    _container.RemoveAt(_container.childCount - 1);
                SettingEditors.BuildBroken(_container, setting);
                if (LoggedRows.Add($"{setting.Mod.Guid}/{setting.Entry.Definition}"))
                    Plugin.Log.LogWarning($"Can't show {setting.Mod.Name} / {setting.Entry.Definition} in the Mods tab. {e}");
                return () => { };
            }
        }

        private void RefreshAll()
        {
            foreach (var refresh in _refreshers)
                refresh();
        }
    }
}
