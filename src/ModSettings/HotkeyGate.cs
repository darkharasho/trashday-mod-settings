namespace ModSettings
{
    internal enum HotkeyAction
    {
        None,
        ShowModsTab,
        OpenOptions,
    }

    internal struct HotkeyState
    {
        /// <summary>An Options screen is enabled.</summary>
        public bool OptionsActive;
        public bool TextFieldFocused;
        public bool Rebinding;
        public bool Loading;
        /// <summary>MenuSceneManager.MenusSuppressed: the developer console or the Admin Menu owns input.</summary>
        public bool MenusSuppressed;
        /// <summary>A pause menu (in a run) or title screen exists to open Options from.</summary>
        public bool HasMenuHost;
        /// <summary>A menu other than the pause menu, title screen or Options is open (results screen, modal, …).</summary>
        public bool OtherMenuOpen;
    }

    internal static class HotkeyGate
    {
        public static HotkeyAction Decide(HotkeyState s)
        {
            if (s.TextFieldFocused || s.Rebinding || s.Loading || s.MenusSuppressed || s.OtherMenuOpen)
                return HotkeyAction.None;
            if (s.OptionsActive)
                return HotkeyAction.ShowModsTab;
            return s.HasMenuHost ? HotkeyAction.OpenOptions : HotkeyAction.None;
        }
    }
}
