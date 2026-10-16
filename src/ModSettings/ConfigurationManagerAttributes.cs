namespace ModSettings
{
    /// <summary>
    /// Tag for a setting's <see cref="BepInEx.Configuration.ConfigDescription"/>, read by field name by settings
    /// menus (this mod and BepInEx.ConfigurationManager).
    /// </summary>
    internal sealed class ConfigurationManagerAttributes
    {
        public string DispName;
        public int? Order;
    }
}
