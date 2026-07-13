namespace PoorMansTSqlFormatterPluginShared
{
    /// <summary>
    /// Settings contract shared between plugin implementations (SSMS, VS) and the shared UI/utilities.
    /// </summary>
    public interface ISqlSettings
    {
        string OptionsSerialized { get; set; }
        string Hotkey { get; set; }
    }
}
