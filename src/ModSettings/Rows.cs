using System;
using System.Collections.Generic;
using Trash;
using UnityEngine;
using UnityEngine.UIElements;

namespace ModSettings
{
    /// <summary>
    /// Builds settings rows with the same elements and USS classes as Trash.OptionsUI's AddSlider/AddCycle/
    /// AddDropdown, so the Options screen's own style sheets give them the game's look.
    /// </summary>
    internal static class Rows
    {
        public static SettingRow Row(VisualElement parent, string label, string description)
        {
            var row = new SettingRow();
            var labels = new VisualElement();
            labels.AddToClassList("setting-label");
            labels.style.flexDirection = FlexDirection.Column;

            var name = new Label(label);
            name.AddToClassList("options-text");
            labels.Add(name);

            if (!string.IsNullOrEmpty(description))
            {
                var note = new Label(description);
                note.AddToClassList("options-text");
                note.style.whiteSpace = WhiteSpace.Normal;
                note.style.opacity = 0.7f;
                // Smaller than the name, whatever size the game's sheet gives it.
                name.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    var size = name.resolvedStyle.fontSize * 0.6f;
                    if (size > 0f && Math.Abs(note.resolvedStyle.fontSize - size) > 0.5f)
                        note.style.fontSize = size;
                });
                labels.Add(note);
            }
            row.Add(labels);
            parent.Add(row);
            return row;
        }

        public static Button Cycle(SettingRow row, Func<string> text, Action next, Action previous, out Action refresh)
        {
            var control = new VisualElement();
            control.AddToClassList("cycle-control");
            var button = new Button();
            button.AddToClassList("options-text");
            button.AddToClassList("cycle-btn");
            button.AddToClassList("cycle-btn-single");
            Action update = () => button.text = text();
            button.clicked += () =>
            {
                next();
                update();
            };
            button.RegisterCallback<NavigationMoveEvent>(evt =>
            {
                if (evt.direction != NavigationMoveEvent.Direction.Left && evt.direction != NavigationMoveEvent.Direction.Right)
                    return;
                if (evt.direction == NavigationMoveEvent.Direction.Left)
                    previous();
                else
                    next();
                update();
                evt.StopPropagation();
                button.panel?.focusController?.IgnoreEvent(evt);
            });
            control.Add(button);
            row.Add(control);
            update();
            refresh = update;
            return button;
        }

        public static Slider Slider(SettingRow row, float low, float high, bool integer, Func<float> get, Action<float> set, out Action refresh)
        {
            var control = new VisualElement();
            control.AddToClassList("setting-control");
            var valueLabel = new Label();
            valueLabel.AddToClassList("options-text");
            valueLabel.AddToClassList("slider-value-label");
            var slider = new Slider(low, high);
            slider.AddToClassList("setting-slider");
            slider.RegisterValueChangedCallback(e =>
            {
                var value = integer ? (float)ValueParsing.SnapInteger(e.newValue) : ValueParsing.RoundToStep(e.newValue, low, high);
                slider.SetValueWithoutNotify(value);
                valueLabel.text = ValueParsing.FormatSlider(value, integer);
                if (!Mathf.Approximately(value, get()))
                    set(value);
            });
            control.Add(valueLabel);
            control.Add(slider);
            row.Add(control);
            refresh = () =>
            {
                var value = get();
                slider.SetValueWithoutNotify(value);
                valueLabel.text = ValueParsing.FormatSlider(value, integer);
            };
            refresh();
            return slider;
        }

        public static DropdownField Dropdown(SettingRow row, List<string> labels, Func<int> getIndex, Func<string> currentLabel,
            Action<int> setIndex, VisualElement skinRoot, out Action refresh)
        {
            var dropdown = new DropdownField(labels, -1);
            dropdown.AddToClassList("options-text");
            dropdown.AddToClassList("setting-dropdown");
            dropdown.RegisterCallback<PointerDownEvent>(_ => skinRoot.schedule.Execute(() => SkinOpenDropdown(skinRoot)));
            dropdown.RegisterCallback<NavigationSubmitEvent>(_ => skinRoot.schedule.Execute(() => SkinOpenDropdown(skinRoot)));
            dropdown.RegisterValueChangedCallback(_ =>
            {
                var index = labels.IndexOf(dropdown.value);
                if (index >= 0)
                    setIndex(index);
            });
            row.Add(dropdown);
            refresh = () =>
            {
                var index = getIndex();
                dropdown.SetValueWithoutNotify(index >= 0 ? labels[index] : currentLabel());
            };
            refresh();
            return dropdown;
        }

        /// <summary>Same as OptionsUI.SkinOpenDropdownMenu: the open menu lives outside the document, so give it the screen's sheets.</summary>
        private static void SkinOpenDropdown(VisualElement skinRoot)
        {
            var menu = skinRoot.panel?.visualTree.Q(null, "unity-base-dropdown");
            if (menu == null || menu.ClassListContains("trash-dropdown-skinned"))
                return;
            menu.AddToClassList("trash-dropdown-skinned");
            for (var i = 0; i < skinRoot.styleSheets.count; i++)
                menu.styleSheets.Add(skinRoot.styleSheets[i]);
        }

        public static TextField Text(SettingRow row, Func<string> get, Func<string, bool> commit, out Action refresh)
        {
            var control = new VisualElement();
            control.AddToClassList("setting-control");
            var field = new TextField { isDelayed = true };
            field.AddToClassList("options-text");
            field.style.flexGrow = 1f;
            Action update = () => field.SetValueWithoutNotify(get());
            field.RegisterValueChangedCallback(e =>
            {
                if (!commit(e.newValue))
                    update();
            });
            control.Add(field);
            row.Add(control);
            update();
            refresh = update;
            return field;
        }

        public static Label ReadOnly(SettingRow row, Func<string> text, out Action refresh)
        {
            var control = new VisualElement();
            control.AddToClassList("setting-control");
            var label = new Label();
            label.AddToClassList("options-text");
            label.style.opacity = 0.7f;
            control.Add(label);
            row.Add(control);
            Action update = () => label.text = text();
            update();
            refresh = update;
            return label;
        }

        /// <summary>A short marker after the control, such as "restart needed". Hidden until shown.</summary>
        public static Label Note(SettingRow row, string text)
        {
            var note = new Label(text);
            note.AddToClassList("options-text");
            note.style.opacity = 0.7f;
            note.style.marginLeft = 12f;
            note.style.display = DisplayStyle.None;
            row.Add(note);
            return note;
        }

        public static void Heading(VisualElement parent, string text)
        {
            var heading = new Label(text);
            heading.AddToClassList("options-text");
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.marginTop = 18f;
            heading.style.marginBottom = 6f;
            parent.Add(heading);
        }

        public static void Message(VisualElement parent, string text)
        {
            var message = new Label(text);
            message.AddToClassList("options-text");
            message.style.marginTop = 24f;
            message.style.unityTextAlign = TextAnchor.MiddleCenter;
            parent.Add(message);
        }
    }
}
