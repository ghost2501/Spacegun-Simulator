namespace Spacegun_Simulator.UI.Theme
{
    /// <summary>
    /// Per-page UI behavior overrides ("chrome"). Defaults should work for most pages.
    /// </summary>
    public sealed record PageChrome(
        bool ShowStatusBar = true,
        bool ShowSidePanels = false,
        bool AutoSaveOnEnter = true,
        bool AutoSaveOnExit = true,
        string? LeftTitle = null,
        string? RightTitle = null,
        string? FooterHint = null
    );
}
